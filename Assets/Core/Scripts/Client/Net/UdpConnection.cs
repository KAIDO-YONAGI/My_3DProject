using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// 一个 UdpConnection 只对应一次 UDP 端点及其后续收发。
// 它不依赖 Unity API，所有回调都由 NetManager 排队后在主线程处理。
internal sealed class UdpConnection
{
    private readonly string ip;
    private readonly int port;
    private readonly Action<string> messageReceived;
    private readonly Action<bool> connectCompleted;
    private readonly Action<string> logReceived;
    private readonly UdpClient client = new UdpClient();
    private readonly CancellationTokenSource receiveCancellationTokenSource = new CancellationTokenSource();
    private readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);

    private int closed;
    private int connected;

    public bool IsConnected =>
        Volatile.Read(ref connected) == 1 && Volatile.Read(ref closed) == 0;

    public UdpConnection(
        string ip,
        int port,
        Action<string> messageReceived,
        Action<bool> connectCompleted,
        Action<string> logReceived)
    {
        this.ip = ip;
        this.port = port;
        this.messageReceived = messageReceived;
        this.connectCompleted = connectCompleted;
        this.logReceived = logReceived;
    }

    public void Connect()
    {
        try
        {
            // UDP 没有握手；Connect 只设置默认目标端点，后续收发通过数据报完成。
            client.Connect(ip, port);
            if (IsClosed) return;

            Volatile.Write(ref connected, 1);
            Log("UDP endpoint ready");
            connectCompleted(true);
            _ = ReceiveLoopAsync(receiveCancellationTokenSource.Token);
        }
        catch (Exception e)
        {
            if (IsClosed) return;

            Log("UDP Connect failed" + e);
            connectCompleted(false);
            Close();
        }
    }

    public void Send(string sendStr)
    {
        _ = SendDatagramAsync(sendStr);
    }

    public async Task DisconnectAsync(string leaveMessage)
    {
        if (IsConnected)
        {
            await SendDatagramAsync(leaveMessage).ConfigureAwait(false);
        }

        Close();
    }

    public string GetLocalEndPoint()
    {
        if (!IsConnected) return string.Empty;

        try
        {
            return client.Client.LocalEndPoint?.ToString() ?? string.Empty;
        }
        catch (ObjectDisposedException)
        {
            return string.Empty;
        }
    }

    private async Task SendDatagramAsync(string sendStr)
    {
        if (!IsConnected) return;

        byte[] sendBytes = Encoding.UTF8.GetBytes(sendStr);
        bool lockTaken = false;
        try
        {
            await sendLock.WaitAsync().ConfigureAwait(false);
            lockTaken = true;
            if (!IsConnected) return;

            await client.SendAsync(sendBytes, sendBytes.Length).ConfigureAwait(false);
            Log("Sent UDP datagram: " + sendBytes.Length + " bytes");
        }
        catch (ObjectDisposedException)
        {
            Log("UDP send ended: client disposed");
        }
        catch (Exception e)
        {
            Log("UDP Send failed" + e);
            Close();
        }
        finally
        {
            if (lockTaken)
            {
                sendLock.Release();
            }
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                UdpReceiveResult result = await client.ReceiveAsync().ConfigureAwait(false);
                if (result.Buffer.Length == 0) continue;

                string message = Encoding.UTF8.GetString(result.Buffer).TrimEnd('\r', '\n');
                if (!string.IsNullOrEmpty(message))
                {
                    messageReceived(message);
                }
            }
        }
        catch (ObjectDisposedException)
        {
            Log("UDP receive ended: client disposed");
        }
        catch (SocketException e)
        {
            if (!IsClosed)
            {
                Log("UDP Receive failed" + e);
            }
        }
        catch (Exception e)
        {
            if (!IsClosed)
            {
                Log("UDP Receive failed" + e);
            }
        }
        finally
        {
            if (!IsClosed)
            {
                Close();
            }
        }
    }

    public void Close()
    {
        if (Interlocked.Exchange(ref closed, 1) != 0) return;

        Volatile.Write(ref connected, 0);
        receiveCancellationTokenSource.Cancel();
        client.Close();
    }

    private bool IsClosed => Volatile.Read(ref closed) == 1;

    private void Log(string message)
    {
        logReceived(message);
    }
}
