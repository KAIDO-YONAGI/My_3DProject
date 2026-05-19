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
    private static Dictionary<MessageName, MessageListener> listenerList = new();
    static List<string> messageList = new();

    public static string GetDescribe()
    {
        if(socket==null||!socket.Connected)return"";
        return socket!.LocalEndPoint!.ToString()!;

    }
    public static void Connect(string ip,int port)
    {
        socket=new Socket(AddressFamily.InterNetwork,SocketType.Stream,ProtocolType.Tcp);

        socket.Connect(ip,port);//暂时使用同步方法

        //开始接收
        socket.BeginReceive(readBuffer,0,readBuffer.Length,SocketFlags.None,ReceiveCallback,socket);
    }

    private static void ReceiveCallback(IAsyncResult ar)
    {
        try
        {
            Socket socket=(Socket )ar.AsyncState;
            int count =socket.EndReceive(ar);

            
        }catch(SocketException e)
        {
            Debug.Log("Socket Receive failed"+e.ToString());
        }
    }
    public static void Send(string sendStr)
    {
        if(socket==null||!socket.Connected)return;
        byte[] sendBytes=System.Text.Encoding.Default.GetBytes(sendStr);
        socket.Send(sendBytes);
    }
    public static void Update()
    {
        if(messageList.Count<=0)return;
        string messageStr=messageList[0];
        messageList.RemoveAt(0);
        
        string[] split=messageStr.Split('|');
        string messageNameStr=split[0];
        string messageArgs=split[1];
        if(!Enum.TryParse<MessageName>(messageNameStr,out MessageName messageName))return;
        //监听回调
        if (listenerList.ContainsKey(messageName))
        {
            listenerList[messageName](messageArgs);//找到对应的delegate并且传参
        }

    }

    public static void AddListener(MessageName messageName, MessageListener listener)
    {
        listenerList[messageName] = listener;
    }
}