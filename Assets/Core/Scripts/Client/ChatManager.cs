using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ChatManager : MonoBehaviour
{
    Socket socket;
    // UI 组件：输入框、显示文本、连接按钮、发送按钮（在 Inspector 中拖拽赋值）
    public TMP_InputField inputField;
    public TMP_Text text;

    public Button connectButton;
    public Button sendButton;
    // 接收缓冲区，内核收到数据后直接写入这里
    const int BufferSize = 1024;
    readonly ConcurrentQueue<string> receivedMessages = new();
    readonly SemaphoreSlim sendLock = new(1, 1);
    readonly object pendingReceiveLock = new();
    CancellationTokenSource receiveCancellationTokenSource;
    string pendingReceive = "";
    // 累积收到的所有服务端回传字符串，用于界面显示
    string receiveStr = "";

    /// <summary>
    /// Unity 生命周期，在游戏开始时调用一次。注册按钮点击事件。
    /// </summary>
    void Start()
    {
        // onClick.AddListener 注册的是回调，点击时由 Unity 事件系统调用
        connectButton.onClick.AddListener(OnClickConnectButton);
        sendButton.onClick.AddListener(OnClickSendButton);
    }

    /// <summary>
    /// 每帧调用，将收到的内容刷新到 UI 文本上。
    /// receiveStr 在异步回调（子线程）中修改，这里（主线程）读取显示，实现跨线程数据传递。
    /// </summary>
    private void Update()
    {
        while (receivedMessages.TryDequeue(out string msg))
        {
            receiveStr += '\n' + msg;
        }

        text.text = "\nReceived: " + receiveStr;
    }

    void OnDestroy()
    {
        CloseSocket();
    }

    /// <summary>
    /// 点击连接按钮时调用。创建 TCP socket 并发起异步连接。
    /// </summary>
    public void OnClickConnectButton()
    {
        CloseSocket();
        receiveStr = "";
        lock (pendingReceiveLock)
        {
            pendingReceive = "";
        }

        // 创建 TCP socket（IPv4, 流式, TCP）
        socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        // 异步连接服务端 127.0.0.1:8888，连接成功后回调 ConnectCallback
        // socket 作为 AsyncState 传入，回调中可通过 ar.AsyncState 取回
        _ = ConnectAsyncInternal(socket);
    }

    /// <summary>
    /// 点击发送按钮时调用。将输入框内容编码后异步发送给服务端。
    /// </summary>
    public void OnClickSendButton()
    {
        if (socket == null || !socket.Connected) return;

        string sendStr = inputField.text;
        byte[] sendBytes = Encoding.Default.GetBytes(sendStr);
        // 异步发送，发送完成后回调 SendCallback
        // 不能在发送后马上 Close()，否则异步操作还没完成就被终止
        _ = SendAllAsync(sendBytes, socket);
    }

    /// <summary>
    /// 发送完成后被调用。确认发送了多少字节。
    /// </summary>
    private async Task SendAllAsync(byte[] sendBytes, Socket currentSocket)
    {
        bool lockTaken = false;

        try
        {
            if (currentSocket == null || !currentSocket.Connected) return;

            // 取出 BeginSend 时传入的 socket
            await sendLock.WaitAsync().ConfigureAwait(false);
            lockTaken = true;

            // EndSend 完成发送操作，返回实际发出的字节数
            int totalSent = 0;
            while (totalSent < sendBytes.Length)
            {
                int bytesSent = await currentSocket.SendAsync(
                    new ArraySegment<byte>(sendBytes, totalSent, sendBytes.Length - totalSent),
                    SocketFlags.None).ConfigureAwait(false);
                if (bytesSent <= 0)
                    throw new SocketException((int)SocketError.ConnectionReset);
                totalSent += bytesSent;
            }

            Debug.Log("Sent " + totalSent + " bytes to server.");
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception e)
        {
            Debug.Log(e);
        }
        finally
        {
            if (lockTaken)
                sendLock.Release();
        }
    }

    /// <summary>
    /// 连接成功后被调用。完成连接并开始异步接收服务端数据。
    /// </summary>
    private async Task ConnectAsyncInternal(Socket currentSocket)
    {
        try
        {
            // EndConnect 完成连接握手
            await currentSocket.ConnectAsync("127.0.0.1", 8888).ConfigureAwait(false);
            if (!ReferenceEquals(socket, currentSocket))
            {
                currentSocket.Close();
                return;
            }

            Debug.Log("Connected to server");
            // 连接成功后立即注册异步接收，等待服务端回传数据
            // readBuffer 作为缓冲区，收到数据后内核直接写入
            receiveCancellationTokenSource = new CancellationTokenSource();
            _ = ReceiveLoopAsync(currentSocket, receiveCancellationTokenSource.Token);
        }
        catch (Exception e)
        {
            Debug.Log(e);
        }
    }

    void AppendMessages(string recvStr)
    {
        lock (pendingReceiveLock)
        {
            pendingReceive += recvStr;
            string[] split = pendingReceive.Split('\n');
            for (int i = 0; i < split.Length - 1; i++)
            {
                string msg = split[i];
                if (string.IsNullOrEmpty(msg)) continue;
                receivedMessages.Enqueue(msg);
            }

            pendingReceive = split[^1];
        }
    }

    /// <summary>
    /// 收到服务端数据时被调用。读取内容拼接到 receiveStr，然后继续注册接收。
    /// </summary>
    private async Task ReceiveLoopAsync(Socket currentSocket, CancellationToken cancellationToken)
    {
        byte[] readBuffer = new byte[BufferSize];

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // EndReceive 完成接收，返回实际读取的字节数
                int bytesRead = await currentSocket.ReceiveAsync(
                    new ArraySegment<byte>(readBuffer),
                    SocketFlags.None).ConfigureAwait(false);
                if (bytesRead <= 0) return;

                // 将字节解码为字符串，拼接到累积字符串中
                // 这里用 += 是因为 TCP 是字节流，一条消息可能分多次到达（拆包）
                AppendMessages(Encoding.Default.GetString(readBuffer, 0, bytesRead));
                // 继续异步接收下一段数据（形成循环）
            }
            // bytesRead == 0 表示服务端主动断开连接（TCP FIN），这里不做处理
        }
        catch (ObjectDisposedException)
        {
        }
        catch (Exception e)
        {
            Debug.Log(e);
        }
        finally
        {
            if (ReferenceEquals(socket, currentSocket))
                CloseSocket();
        }
    }

    void CloseSocket()
    {
        if (receiveCancellationTokenSource != null)
        {
            receiveCancellationTokenSource.Cancel();
            receiveCancellationTokenSource.Dispose();
            receiveCancellationTokenSource = null;
        }

        if (socket == null) return;

        try
        {
            if (socket.Connected)
                socket.Shutdown(SocketShutdown.Both);
        }
        catch
        {
        }

        try
        {
            socket.Close();
        }
        catch
        {
        }

        socket = null;
    }
}
