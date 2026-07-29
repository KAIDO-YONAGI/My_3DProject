using System;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

// 一个 TcpConnection 只对应一次连接尝试及其后续收发。
// 它不依赖 Unity API，所有回调都由 NetManager 排队后在主线程处理。
internal sealed class TcpConnection
{
    const int BufferSize = 1024;

    private readonly Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    private readonly string ip;
    private readonly int port;
    private readonly Action<string> messageReceived;
    private readonly Action<bool> connectCompleted;
    private readonly Action<string> logReceived;
    // 异步发送锁：SemaphoreSlim 支持跨 await 持有，保证同一 socket 不会并发发送
    private readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
    private readonly CancellationTokenSource receiveCancellationTokenSource = new CancellationTokenSource();
    private readonly LineMessageBuffer messageBuffer = new LineMessageBuffer();

    private int closed;
    private int connected;

    public bool IsConnected =>
        Volatile.Read(ref connected) == 1 && Volatile.Read(ref closed) == 0;

    public TcpConnection(
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
        // 专门写法：丢弃返回值（_ =），fire-and-forget。
        // Connect 是同步签名供外部调用，内部走异步连接，不阻塞调用方。
        _ = ConnectAsync();
    }

    private async Task ConnectAsync()
    {
        try
        {
            await socket.ConnectAsync(ip, port).ConfigureAwait(false);
            if (IsClosed) return;

            Volatile.Write(ref connected, 1);
            Log("Connected to server");
            connectCompleted(true);

            // 故意不 await：接收循环需独立长跑，不阻塞连接流程
            _ = ReceiveLoopAsync(receiveCancellationTokenSource.Token);
        }
        catch (ObjectDisposedException)
        {
            if (IsClosed) return;

            connectCompleted(false);
            Close();
        }
        catch (Exception e)
        {
            if (IsClosed) return;

            // SocketException 和其他异常的处理逻辑完全一致，合并为一个 catch
            Log("Socket Connect failed" + e);
            connectCompleted(false);
            Close();
        }
    }

    public void Send(string sendStr)
    {
        // 专门写法：丢弃返回值（_ =），fire-and-forget。
        // 调用方（如 Unity 事件）不阻塞等待发送完成，异常在 SendAllAsync 内部处理。
        _ = SendAllAsync(sendStr);
    }

    public async Task DisconnectAsync(string leaveMessage)
    {
        if (IsConnected)
        {
            await SendAllAsync(leaveMessage).ConfigureAwait(false);
        }

        Close();
    }

    public string GetLocalEndPoint()
    {
        if (!IsConnected) return string.Empty;

        try
        {
            return socket.LocalEndPoint?.ToString() ?? string.Empty;
        }
        catch (ObjectDisposedException)
        {
            return string.Empty;
        }
        catch (SocketException)
        {
            return string.Empty;
        }
    }

    private async Task SendAllAsync(string sendStr)
    {
        if (!IsConnected)
        {
            return;
        }

        // 显式指定 UTF-8：与服务端解码端保持一致
        byte[] sendBytes = Encoding.UTF8.GetBytes(sendStr);

        // 专门写法：lockTaken 标志位 + try/finally。
        // WaitAsync 可能在拿到锁之前就抛异常（如被取消），此时不能 Release，
        // 否则 SemaphoreSlim 计数会失衡。用标志位记录"是否真的拿到了锁"。
        bool lockTaken = false;

        try
        {
            await sendLock.WaitAsync().ConfigureAwait(false);
            lockTaken = true;

            if (!IsConnected) return;

            int totalSent = 0;
            // TCP 是字节流，SendAsync 不保证一次发完全部字节，需循环直到 totalSent == Length
            while (totalSent < sendBytes.Length)
            {
                int count = await socket.SendAsync(
                    new ArraySegment<byte>(sendBytes, totalSent, sendBytes.Length - totalSent),
                    SocketFlags.None).ConfigureAwait(false);
                if (count <= 0)
                {
                    throw new SocketException((int)SocketError.ConnectionReset);
                }
                totalSent += count;
            }

            Log("Sent " + totalSent + " bytes");
        }
        catch (ObjectDisposedException)
        {
            // CloseSocket 关闭 socket 时触发，属正常情况，记日志即可
            Log("Send ended: socket disposed");
        }
        catch (SocketException e)
        {
            Log("Socket Send failed" + e);
            Close();
        }
        catch (Exception e)
        {
            Log("Socket Send failed" + e);
            Close();
        }
        finally
        {
            // 只有真正拿到锁才释放，保证计数平衡
            if (lockTaken)
            {
                sendLock.Release();
            }
        }
    }

    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        byte[] readBuffer = new byte[BufferSize];

        try
        {
            // 协作式取消：检查 Token 标志位，Cancel() 调用后变为 true 退出循环
            while (!cancellationToken.IsCancellationRequested)
            {
                int count = await socket.ReceiveAsync(
                    new ArraySegment<byte>(readBuffer),
                    SocketFlags.None).ConfigureAwait(false);
                // 不回到 Unity 主线程：默认 await 会通过 UnitySynchronizationContext
                // 在下一帧 PlayerLoop 阶段回到主线程恢复执行，这里用 false 跳过这个调度，
                // 直接在完成异步操作的线程池线程上继续，因为后续 AppendMessages 只操作
                // lock 和 ConcurrentQueue，不需要主线程，避免每帧排队开销

                if (count <= 0)
                {
                    // 对端关闭连接（返回 0）或出错（返回负），退出接收循环
                    return;
                }

                messageBuffer.Append(readBuffer, count, messageReceived);
            }
        }
        catch (ObjectDisposedException)
        {
            // CloseSocket 关闭 socket 时会触发，属正常退出，记日志即可
            Log("ReceiveLoop ended: socket disposed");
        }
        catch (SocketException e)
        {
            Log("Socket Receive failed" + e);
        }
        catch (Exception e)
        {
            Log("Socket Receive failed" + e);
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

        // 1. 取消接收循环：通知 ReceiveLoopAsync 退出 while 循环
        receiveCancellationTokenSource.Cancel();

        // 2. 发送 FIN 包通知对方"我不再收发了"，避免对方收到连接重置异常
        try
        {
            socket.Shutdown(SocketShutdown.Both);
        }
        catch (SocketException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        finally
        {
            // 3. 释放底层资源（端口、缓冲区、OS 句柄）
            socket.Close();
        }
    }

    private bool IsClosed => Volatile.Read(ref closed) == 1;

    private void Log(string message)
    {
        logReceived(message);
    }
}
