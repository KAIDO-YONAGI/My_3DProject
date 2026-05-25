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
    private readonly ConcurrentQueue<string> messageList = new();
    private readonly ConcurrentQueue<bool> connectResultList = new();
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
        _ = ConnectCallback(ip, port);
    }

    private async Task ConnectCallback(string ip, int port)
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
            receiveCancellationTokenSource = new CancellationTokenSource();
            _ = ReceiveCallback(currentSocket, receiveCancellationTokenSource.Token);
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

    private async Task ReceiveCallback(Socket currentSocket, CancellationToken cancellationToken)
    {
        byte[] readBuffer = new byte[BufferSize];

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int count = await currentSocket.ReceiveAsync(
                    new ArraySegment<byte>(readBuffer),
                    SocketFlags.None).ConfigureAwait(false);
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
        _ = DisconnectAsync();
    }

    private async Task DisconnectAsync()
    {
        if (socket == null) return;

        if (socket.Connected)
            await SendCallback(Protocol.PackLeave()).ConfigureAwait(false);

        Connected = false;
        CloseSocket();
    }

    public void Send(string sendStr)
    {
        _ = SendCallback(sendStr);
    }

    private async Task SendCallback(string sendStr)
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

    public void AddListenerIntoList(ClientMessageType messageName, MessageListener listener)
    {
        listenerList[messageName] = listener;
    }
}
