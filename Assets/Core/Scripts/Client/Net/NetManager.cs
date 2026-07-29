using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using ClientProtocol;

// NetManager 只处理 Unity 生命周期、事件分发和当前连接的协调。
// Socket 的连接生命周期与收发逻辑由 TcpConnection 独立管理。
public class NetManager : MonoBehaviour
{
    public delegate void MessageListener(ParsedMessage msg);
    private readonly Dictionary<ClientMessageType, MessageListener> listenerList = new();

    // 跨线程消息队列：线程池线程 Enqueue，Unity 主线程 TryDequeue，无需加锁
    private readonly ConcurrentQueue<ReceivedMessage> messageList = new();
    // 跨线程连接结果队列：异步连接完成后 Enqueue，主线程 Update 中消费
    private readonly ConcurrentQueue<ConnectionResult> connectResultList = new();
    private readonly ConcurrentQueue<string> logList = new();
    private TcpConnection connection;
    private int lastConnectionId;
    private int activeConnectionId;

    public static NetManager Instance { get; private set; }
    public bool Connected => connection != null && connection.IsConnected;

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
        CloseCurrentConnection();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    void Update()
    {
        while (logList.TryDequeue(out string logMessage))
        {
            Debug.Log(logMessage);
        }

        // 消费连接结果队列：把每次连接的成功/失败通过事件通道发出去
        while (connectResultList.TryDequeue(out ConnectionResult connectResult))
        {
            if (connectResult.connectionId != activeConnectionId)
            {
                continue;
            }

            connectResultChannel.Raise(connectResult.success);
        }

        // 消费消息队列：逐条解析并分发给对应类型的监听器
        while (messageList.TryDequeue(out ReceivedMessage receivedMessage))
        {
            if (receivedMessage.connectionId != activeConnectionId)
            {
                continue;
            }

            //TODO解析Enter，注册并且更新新加入用户
            if (!Protocol.Unpack(receivedMessage.message, out ParsedMessage msg)) continue;

            //调用对应事务
            if (listenerList.TryGetValue(msg.clientMessageType, out MessageListener listener))
            {
                listener(msg);
            }
        }
    }

    public void Connect(string ip, int port)
    {
        CloseCurrentConnection();

        int connectionId = ++lastConnectionId;
        activeConnectionId = connectionId;
        TcpConnection newConnection = new TcpConnection(
            ip,
            port,
            message => messageList.Enqueue(new ReceivedMessage(connectionId, message)),
            success => connectResultList.Enqueue(new ConnectionResult(connectionId, success)),
            logMessage => logList.Enqueue(logMessage));

        connection = newConnection;
        newConnection.Connect();
    }

    public void Disconnect()
    {
        TcpConnection connectionToClose = connection;
        if (connectionToClose == null) return;

        connection = null;
        activeConnectionId = 0;
        _ = DisconnectAsync(connectionToClose);
    }

    private async Task DisconnectAsync(TcpConnection connectionToClose)
    {
        // 断开前先发 Leave 通知服务端，再关 socket
        await connectionToClose.DisconnectAsync(Protocol.PackLeave()).ConfigureAwait(false);
    }

    public void Send(string sendStr)
    {
        TcpConnection currentConnection = connection;
        if (currentConnection == null) return;

        currentConnection.Send(sendStr);
    }

    public string GetDescribe()
    {
        TcpConnection currentConnection = connection;
        return currentConnection == null ? string.Empty : currentConnection.GetLocalEndPoint();
    }

    public void AddListenerIntoList(ClientMessageType messageName, MessageListener listener)
    {
        listenerList[messageName] = listener;
    }

    private void CloseCurrentConnection()
    {
        TcpConnection connectionToClose = connection;
        connection = null;
        activeConnectionId = 0;
        connectionToClose?.Close();
    }

    private sealed class ConnectionResult
    {
        public readonly int connectionId;
        public readonly bool success;

        public ConnectionResult(int connectionId, bool success)
        {
            this.connectionId = connectionId;
            this.success = success;
        }
    }

    private sealed class ReceivedMessage
    {
        public readonly int connectionId;
        public readonly string message;

        public ReceivedMessage(int connectionId, string message)
        {
            this.connectionId = connectionId;
            this.message = message;
        }
    }
}
