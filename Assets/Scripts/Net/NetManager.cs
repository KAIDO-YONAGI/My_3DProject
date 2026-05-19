using System;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;

public enum MessageName
{
    Enter,
    Move,
    Leave,
}

public class NetManager
{
    static Socket socket;
    static byte[] readBuffer = new byte[1024];

    public delegate void MessageListener(string str);
    private Dictionary<MessageName, MessageListener> listenerList = new();
    List<string> messageList = new();

    public static NetManager Instance { get; } = new();

    public string GetDescribe()
    {
        if (socket == null || !socket.Connected) return "";
        return socket!.LocalEndPoint!.ToString()!;
    }

    public void Connect(string ip, int port)
    {
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Connect(ip, port);
        socket.BeginReceive(readBuffer, 0, readBuffer.Length, SocketFlags.None, ReceiveCallback, socket);
    }

    private static void ReceiveCallback(IAsyncResult ar)
    {
        try
        {
            Socket socket = (Socket)ar.AsyncState;
            int count = socket.EndReceive(ar);
            if (count <= 0) return;
            string recvStr = System.Text.Encoding.Default.GetString(readBuffer, 0, count);
            string[] split = recvStr.Split('\n');
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
        socket.Send(sendBytes);
    }

    public void Update()
    {
        if (messageList.Count <= 0) return;
        string messageStr = messageList[0];
        messageList.RemoveAt(0);

        string[] split = messageStr.Split('|');
        string messageNameStr = split[0];
        string messageArgs = split[1];
        if (!Enum.TryParse<MessageName>(messageNameStr, out MessageName messageName)) return;
        if (listenerList.ContainsKey(messageName))
        {
            listenerList[messageName](messageArgs);
        }
    }

    public void AddListener(MessageName messageName, MessageListener listener)
    {
        listenerList[messageName] = listener;
    }
}
