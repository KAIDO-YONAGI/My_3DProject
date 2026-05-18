using System;
using System.Net.Sockets;
using UnityEngine;

public class Client : MonoBehaviour
{
    private Socket clientfd = null!;
    private byte[] readBuffer = new byte[1024];
    private string receiveStr = "";
    void Start()
    {
        // 创建 TCP socket
        clientfd = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        // 连接服务器
        clientfd.Connect("127.0.0.1", 8888);
    }
    void StartReceive()
    {
        // 开始异步接收数据，接收成功后回调 ReceiveCallback
        // clientfd 作为 AsyncState 传给回调，方便回调中拿到通信 socket
        clientfd.BeginReceive(readBuffer, 0, 1024, SocketFlags.None, ReceiveCallback, clientfd);
    }

    private void ReceiveCallback(IAsyncResult ar)
    {
        if(clientfd.EndReceive(ar) > 0)
        {
            // 接收成功，readBuffer 中是收到的数据，长度为 EndReceive 的返回值
            // 把字节数组转换成字符串
            receiveStr += System.Text.Encoding.UTF8.GetString(readBuffer);
            Debug.Log("Received: " + receiveStr);
            // 继续等待接收下一个数据包
            StartReceive();
        }
        else
        {
            // 接收失败，说明服务器断开了连接
            Debug.Log("Server Disconnected");
            clientfd.Close();
        }
    }
}