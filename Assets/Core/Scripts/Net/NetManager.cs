using System;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;

public class NetManager : MonoBehaviour
{
    static Socket socket;
    static byte[] readBuffer = new byte[1024];

    public delegate void MessageListener(string str);
    private Dictionary<ClientMessageName, MessageListener> listenerList = new();
    List<string> messageList = new();

    public static NetManager Instance { get; private set; }

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

    private static void ConnectCallback(IAsyncResult ar)
    {
        try
        {
            Socket socket = (Socket)ar.AsyncState;
            socket.EndConnect(ar);
            Debug.Log("Connected to server");
            socket.BeginReceive(readBuffer, 0, readBuffer.Length, SocketFlags.None, ReceiveCallback, socket);
        }
        catch (SocketException e)
        {
            Debug.Log("Socket Connect failed" + e.ToString());
        }
    }

    private static void ReceiveCallback(IAsyncResult ar)
    {
        try
        {
            Socket socket = (Socket)ar.AsyncState;
            int count = socket.EndReceive(ar);
            if (count <= 0) return;
            string recvStr = System.Text.Encoding.Default.GetString(readBuffer, 0, count);
            string[] split = recvStr.Split(ClientProtocol.LineEnd);
            for (int i = 0; i < split.Length - 1; i++)
            {
                Instance.messageList.Add(split[i]);
            }
            socket.BeginReceive(readBuffer, 0, readBuffer.Length, SocketFlags.None, ReceiveCallback, socket);
        }
        catch (SocketException e)
        {
            Debug.Log("Socket Receive failed" + e.ToString());
        }
    }

    public void Send(string sendStr)
    {
        if (socket == null || !socket.Connected) return;
        byte[] sendBytes = System.Text.Encoding.Default.GetBytes(sendStr);
        socket.BeginSend(sendBytes, 0, sendBytes.Length, SocketFlags.None, SendCallback, socket);
    }

    private static void SendCallback(IAsyncResult ar)
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

        string[] split = messageStr.Split(ClientProtocol.Separator);
        string messageNameStr = split[0];
        string messageArgs = split[1];

        ClientMessageName messageName;
        if (!Enum.TryParse(messageNameStr, out messageName)) return;
        
        if (listenerList.ContainsKey(messageName))
            listenerList[messageName](messageArgs);//索引对应的listener，圆括号传参调用事务
    }

    public void AddListener(ClientMessageName messageName, MessageListener listener)
    {
        listenerList[messageName] = listener;
    }
}
