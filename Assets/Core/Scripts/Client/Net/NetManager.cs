using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using ClientProtocol;

public class NetManager : MonoBehaviour
{
    static Socket socket;
    const int BufferSize = 1024;

    public delegate void MessageListener(ParsedMessage msg);
    private Dictionary<ClientMessageType, MessageListener> listenerList = new();


    // 跨线程消息队列：线程池线程 Enqueue，Unity 主线程 TryDequeue，无需加锁
    private readonly ConcurrentQueue<string> messageList = new();
    // 跨线程连接结果队列：异步连接完成后 Enqueue，主线程 Update 中消费
    private readonly ConcurrentQueue<bool> connectResultList = new();


    // 异步发送锁：SemaphoreSlim 支持跨 await 持有，保证同一 socket 不会并发发送
    private readonly SemaphoreSlim sendLock = new(1, 1);
    
    private readonly object pendingReceiveLock = new();
    private CancellationTokenSource receiveCancellationTokenSource;
    private string pendingReceive = string.Empty;

    public static NetManager Instance { get; private set; }
    public bool Connected { get; private set; } = false;

    [SerializeField] BoolEventChannelSO connectResultChannel;
    //目前用来处理断线问题，会在Sync里更新Bool变量

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        CloseSocket();
    }

    public string GetDescribe()
    {
        if (socket == null || !socket.Connected) return "";
        return socket.LocalEndPoint!.ToString()!;
    }

    public void Connect(string ip, int port)
    {
        _ = ConnectAsyncInternal(ip, port);
    }

    private async Task ConnectAsyncInternal(string ip, int port)
    {
        CloseSocket();
        lock (pendingReceiveLock)
        {
            pendingReceive = string.Empty;
        }

        Socket currentSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket = currentSocket;

        try
        {
            await currentSocket.ConnectAsync(ip, port).ConfigureAwait(false);
            if (!ReferenceEquals(socket, currentSocket))
            {
                currentSocket.Close();
                return;
            }

            Debug.Log("Connected to server");
            Connected = true;
            connectResultList.Enqueue(true);
            receiveCancellationTokenSource = new CancellationTokenSource(); // 持有 Source：可 Cancel（下命令）
            _ = ReceiveLoopAsync(currentSocket, receiveCancellationTokenSource.Token); // 传 Token：只能检查（听命令），不能 Cancel
        }
        catch (SocketException e)
        {
            Debug.Log("Socket Connect failed" + e.ToString());
            if (!ReferenceEquals(socket, currentSocket))
            {
                currentSocket.Close();
                return;
            }

            Connected = false;
            connectResultList.Enqueue(false);
            CloseSocket();
        }
        catch (Exception e)
        {
            Debug.Log("Socket Connect failed" + e.ToString());
            if (!ReferenceEquals(socket, currentSocket))
            {
                currentSocket.Close();
                return;
            }

            Connected = false;
            connectResultList.Enqueue(false);
            CloseSocket();
        }
    }

    private void AppendMessages(string recvStr)
    {
        lock (pendingReceiveLock)
        {
            pendingReceive += recvStr;
            string[] split = pendingReceive.Split(Protocol.LineEnd);//得到协议条目

            //TODO解析Enter，注册并且更新新加入用户

            for (int i = 0; i < split.Length - 1; i++)
            //范围表达式，表示从索引零到倒数，跳过最后一个元素
            //因为如果末尾有end标记，那split得到的最后一个元素就是空的
            {
                string msg = split[i];
                if (string.IsNullOrEmpty(msg)) continue;
                messageList.Enqueue(msg);//会在update中消费消息队列
            }

            pendingReceive = split[^1];
        }
    }

    private async Task ReceiveLoopAsync(Socket currentSocket, CancellationToken cancellationToken)
    {
        byte[] readBuffer = new byte[BufferSize];

        try
        {
            while (!cancellationToken.IsCancellationRequested) // 协作式取消：检查 Token 标志位，Cancel() 调用后变为 true 退出循环
            {
                int count = await currentSocket.ReceiveAsync
                (
                    new ArraySegment<byte>(readBuffer),
                    SocketFlags.None
                ).ConfigureAwait(false);
                // 不回到 Unity 主线程：默认 await 会通过 UnitySynchronizationContext
                // 在下一帧 PlayerLoop 阶段回到主线程恢复执行，这里用 false 跳过这个调度，
                // 直接在完成异步操作的线程池线程上继续，因为后续 AppendMessages 只操作
                // lock 和 ConcurrentQueue，不需要主线程，避免每帧排队开销

                if (count <= 0) return;

                string recvStr = Encoding.Default.GetString(readBuffer, 0, count);
                AppendMessages(recvStr);
            }
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SocketException e)
        {
            Debug.Log("Socket Receive failed" + e.ToString());
        }
        catch (Exception e)
        {
            Debug.Log("Socket Receive failed" + e.ToString());
        }
        finally
        {
            if (ReferenceEquals(socket, currentSocket))
            {
                Connected = false;
                CloseSocket();
            }
        }
    }

    public void Disconnect()
    {
        _ = DisconnectAsync();//fire-and-forget模式，丢弃Task，不等
    }

    private async Task DisconnectAsync()
    {
        if (socket == null) return;

        if (socket.Connected)
            await SendAllAsync(Protocol.PackLeave()).ConfigureAwait(false);

        Connected = false;
        CloseSocket();
    }

    public void Send(string sendStr)
    {
        _ = SendAllAsync(sendStr);
    }

    private async Task SendAllAsync(string sendStr)
    {
        if (socket == null || !socket.Connected) return;

        byte[] sendBytes = Encoding.Default.GetBytes(sendStr);
        bool lockTaken = false;

        try
        {
            await sendLock.WaitAsync().ConfigureAwait(false);
            lockTaken = true;

            int totalSent = 0;
            while (totalSent < sendBytes.Length)
            //由于TCP是字节流，可能不完整发包，所以要while
            {
                int count = await socket.SendAsync(
                    new ArraySegment<byte>(sendBytes, totalSent, sendBytes.Length - totalSent),
                    SocketFlags.None).ConfigureAwait(false);
                if (count <= 0)
                    throw new SocketException((int)SocketError.ConnectionReset);
                totalSent += count;
            }

            Debug.Log("Sent " + totalSent + " bytes");
        }
        catch (ObjectDisposedException)
        {
        }
        catch (SocketException e)
        {
            Debug.Log("Socket Send failed" + e.ToString());
            Connected = false;
            CloseSocket();
        }
        catch (Exception e)
        {
            Debug.Log("Socket Send failed" + e.ToString());
            Connected = false;
            CloseSocket();
        }
        finally
        {
            if (lockTaken)
                sendLock.Release();
        }
    }

    void Update()
    {
        while (connectResultList.TryDequeue(out bool connectResult))
        {
            connectResultChannel.Raise(connectResult);
        }

        while (messageList.TryDequeue(out string messageStr))
        {
            if (!Protocol.Unpack(messageStr, out ParsedMessage msg)) continue;

            if (listenerList.ContainsKey(msg.clientMessageType))//调用对应事务
                listenerList[msg.clientMessageType](msg);
        }
    }

    void CloseSocket()
    {
        // 1. 取消接收循环：通知 ReceiveLoopAsync 退出 while 循环
        if (receiveCancellationTokenSource != null)
        {
            receiveCancellationTokenSource.Cancel();  // 将 Token.IsCancellationRequested 置为 true
            receiveCancellationTokenSource.Dispose(); // 释放 Token 内部资源
            receiveCancellationTokenSource = null;    // 防止重复操作已释放的对象
        }

        if (socket == null) return;

        // 2. 发送 FIN 包通知对方"我不再收发了"，避免对方收到连接重置异常

        if (socket.Connected)
            socket.Shutdown(SocketShutdown.Both);

        // 3. 释放底层资源（端口、缓冲区、OS 句柄）
        socket.Close();

        socket = null; // 回到初始状态，为下次 Connect 做准备
    }

    public void AddListenerIntoList(ClientMessageType messageName, MessageListener listener)
    {
        listenerList[messageName] = listener;
    }
}
