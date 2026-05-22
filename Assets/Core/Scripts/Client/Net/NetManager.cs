using System;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
using ClientProtocol;

public class NetManager : MonoBehaviour
{
    static Socket socket;
    static byte[] readBuffer = new byte[1024];

    public delegate void MessageListener(ParsedMessage msg);
    private Dictionary<ClientMessageType, MessageListener> listenerList = new();
    private List<string> messageList = new();

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

    public string GetDescribe()
    {
        if (socket == null || !socket.Connected) return "";
        return socket!.LocalEndPoint!.ToString()!;
    }

    public void Connect(string ip, int port)
    {
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.BeginConnect(ip, port, ConnectCallback, socket);
    }

    private void ConnectCallback(IAsyncResult ar)
    {
        try
        {
            Socket socket = (Socket)ar.AsyncState;
            socket.EndConnect(ar);
            Debug.Log("Connected to server");
            Connected = true;
            connectResultChannel.Raise(true);
            socket.BeginReceive(readBuffer, 0, readBuffer.Length, SocketFlags.None, ReceiveCallback, socket);
            return;
        }
        catch (SocketException e)
        {
            Debug.Log("Socket Connect failed" + e.ToString());
            Connected = false;
            connectResultChannel.Raise(false);
        }
    }

    private void ReceiveCallback(IAsyncResult ar)
    {
        try
        {
            Socket socket = (Socket)ar.AsyncState;
            int count = socket.EndReceive(ar);
            if (count <= 0) return;
            string recvStr = System.Text.Encoding.Default.GetString(readBuffer, 0, count);
            string[] split = recvStr.Split(Protocol.LineEnd);//得到协议条目

            //TODO解析Enter，注册并且更新新加入用户

            foreach (string msg in split[0..^1])
            //范围表达式，表示从索引零到倒数，跳过最后一个元素
            //因为如果末尾有end标记，那split得到的最后一个元素就是空的
            {
                Instance.messageList.Add(msg);//会在update中消费消息队列
            }
            socket.BeginReceive(readBuffer, 0, readBuffer.Length, SocketFlags.None, ReceiveCallback, socket);
        }
        catch (SocketException e)
        {
            Debug.Log("Socket Receive failed" + e.ToString());
        }
    }

    public void Disconnect()
    {
        if (socket == null || !socket.Connected) return;
        Send(Protocol.PackLeave());
        socket.Close();
        Connected = false;
    }

    public void Send(string sendStr)
    {
        if (socket == null || !socket.Connected) return;
        byte[] sendBytes = System.Text.Encoding.Default.GetBytes(sendStr);
        socket.BeginSend(sendBytes, 0, sendBytes.Length, SocketFlags.None, SendCallback, socket);
    }

    private void SendCallback(IAsyncResult ar)
    {
        try
        {
            Socket socket = (Socket)ar.AsyncState;
            int count = socket.EndSend(ar);
            Debug.Log("Sent " + count + " bytes");
        }
        catch (SocketException e)
        {
            Debug.Log("Socket Send failed" + e.ToString());
        }
    }

    void Update()
    {
        if (messageList.Count <= 0) return;
        string messageStr = messageList[0];
        messageList.RemoveAt(0);

        if (!Protocol.Unpack(messageStr, out ParsedMessage msg)) return;

        if (listenerList.ContainsKey(msg.clientMessageType))//调用对应事务
            listenerList[msg.clientMessageType](msg);
    }

    public void AddListenerIntoList(ClientMessageType messageName, MessageListener listener)
    {
        listenerList[messageName] = listener;
    }
}
