using System;
using System.Net;
using System.Net.Sockets;

namespace EchoServer
{
    // 每个客户端对应一个状态对象，保存该客户端的 socket、接收缓冲区、已接收字符串
    class ClientState
    {
        public Socket socket = null!;
        public byte[] readBuffer = new byte[1024];
    }
    class MainClass
    {
        // 监听 socket，负责等待新客户端连接
        static Socket listenfd = null!;
        // 所有已连接的客户端集合。
        //   - key（clientfd）  ：用于快速查找/删除，比如客户端断开时 clients.Remove(clientfd)
        //   - value（ClientState）：把 socket 和 readBuffer 打包在一起，方便在回调间通过 AsyncState 传递
        static Dictionary<Socket, ClientState> clients =
                new Dictionary<Socket, ClientState>();
        public static void Main()
        {
            // 创建 TCP 监听 socket（IPv4, 流式, TCP）
            listenfd = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            // 绑定到本地 127.0.0.1:8888
            IPAddress iPAddress = IPAddress.Parse("127.0.0.1");
            IPEndPoint iPEndPoint = new IPEndPoint(iPAddress, 8888);
            listenfd.Bind(iPEndPoint);

            // 开始监听，backlog=0 由操作系统决定等待队列长度
            listenfd.Listen(0);
            Console.WriteLine("Server ON");

            // 异步等待客户端连接，连接成功后回调 AcceptCallback
            // listenfd 作为 AsyncState 传给回调，方便回调中拿到监听 socket
            listenfd.BeginAccept(AcceptCallback, listenfd);
            // 阻塞主线程，防止程序退出
            Console.ReadLine();
        }

        /// <summary>
        /// 有新客户端连接时被调用。负责：接受连接 → 建档 → 注册接收 → 继续等待下一个连接
        /// </summary>
        public static void AcceptCallback(IAsyncResult ar)
        {
            try
            {
                // 取出 BeginAccept 时传入的监听 socket
                Socket? listenfd = (Socket?)ar.AsyncState;
                // EndAccept 完成连接接纳，返回与该客户端通信的新 socket
                Socket clientfd = listenfd!.EndAccept(ar);
                Console.WriteLine("Client Connected: " + clientfd.RemoteEndPoint!.ToString());

                // 为该客户端创建状态对象并存入字典，即注册
                ClientState clientState = new ClientState();
                clientState.socket = clientfd;
                clients.Add(clientfd, clientState);

                // 广播 Enter 消息给所有客户端
                Broadcast("Enter|" + clientfd.RemoteEndPoint!.ToString() + "\n");

                // 异步接收该客户端的数据，数据到达后回调 ReceiveCallback
                // clientState 作为 AsyncState 传入，回调中可取出缓冲区和 socket
                clientfd.BeginReceive
                (
                    clientState.readBuffer,
                    0,
                    clientState.readBuffer.Length,
                    SocketFlags.None,
                    ReceiveCallback,
                    clientState);

                // 继续异步等待下一个客户端连接（形成循环，即再注册）
                listenfd!.BeginAccept(AcceptCallback, listenfd);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        /// <summary>
        /// 某个客户端发来数据时被调用。负责：读取数据 → 打印 → Echo 回传 → 继续监听
        /// </summary>
        public static void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                // 取出 BeginReceive 时传入的客户端状态对象
                ClientState? clientState = (ClientState?)ar.AsyncState;
                Socket clientfd = clientState!.socket;
                // EndReceive 完成接收，返回实际读取的字节数
                int bytesRead = clientfd.EndReceive(ar);

                // bytesRead==0 表示客户端主动断开连接，属于tcp协议规定，也导致这些操作需要放在try-catch里
                if (bytesRead == 0)
                {
                    Console.WriteLine("Client Disconnected: " + clientfd.RemoteEndPoint!.ToString());
                    Broadcast("Leave|" + clientfd.RemoteEndPoint!.ToString() + "\n");
                    clients.Remove(clientfd);
                    clientfd.Close();
                    return;
                }

                // 将收到的字节解码为字符串并打印
                string receiveStr = System.Text.Encoding.Default.GetString(clientState.readBuffer, 0, bytesRead);
                Console.WriteLine("Received from " + clientfd.RemoteEndPoint!.ToString() + ": " + receiveStr);

                // Echo：将收到的内容原样发回客户端
                // byte[] sendBytes = System.Text.Encoding.Default.GetBytes(receiveStr);
                // clientfd.BeginSend
                // (
                //     sendBytes,
                //     0,
                //     sendBytes.Length,
                //     SocketFlags.None,
                //     SendCallback,
                //     clientfd);

                // 广播：发给所有已连接的客户端
                byte[] sendBytes = System.Text.Encoding.Default.GetBytes(receiveStr);
                foreach (var pair in clients)
                {
                    pair.Value.socket.BeginSend(sendBytes, 0, sendBytes.Length, SocketFlags.None, SendCallback, pair.Value.socket);
                }

                // 继续异步接收该客户端的下一条消息（形成循环）
                clientfd.BeginReceive(clientState.readBuffer, 0,
                clientState.readBuffer.Length, SocketFlags.None, ReceiveCallback, clientState);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public static void Broadcast(string sendStr)
        {
            byte[] sendBytes = System.Text.Encoding.Default.GetBytes(sendStr);
            foreach (var pair in clients)
            {
                pair.Value.socket.BeginSend(sendBytes, 0, sendBytes.Length, SocketFlags.None, SendCallback, pair.Value.socket);
            }
        }

        /// <summary>
        /// Echo 数据发送完成后被调用，仅用于确认发送字节数
        /// </summary>
        public static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // 取出 BeginSend 时传入的客户端 socket
                Socket? clientfd = (Socket?)ar.AsyncState;
                // EndSend 完成发送，返回实际发出的字节数
                int bytesSent = clientfd!.EndSend(ar);
                Console.WriteLine("Echoed " + bytesSent + " bytes to client.");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }

}

