using System;
using System.Collections.Generic;
using System.Net.Sockets;
using UnityEngine;
public class NetManager
{
    static Socket socket;
    static byte[] readBuffer = new byte[1024];

    public delegate void MessageListener(string str);
    private static Dictionary<string, MessageListener> listenerList = new();
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

            
        }catch(SocketException sx)
        {
            Debug.Log("Socket Receive failed"+sx.ToString());
        }
    }
}