using System;
using System.Net;
using System.Net.Sockets;

namespace MultiplayerServer
{
    // 每个客户端对应一个状态对象，保存该客户端的 socket 和接收缓冲区
    class ClientState
    {
        public Socket socket = null!;
        public byte[] readBuffer = new byte[1024];
    }

    class ServerCore
    {
        static Socket listenfd = null!;

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
                Socket? listenfd = (Socket?)ar.AsyncState;
                Socket clientfd = listenfd!.EndAccept(ar);
                Console.WriteLine("Client Connected: " + clientfd.RemoteEndPoint!.ToString());

                // 为该客户端创建状态对象并存入字典，即注册
                ClientState clientState = new ClientState();
                clientState.socket = clientfd;
                ServerNetHandler.clients.Add(clientfd, clientState);

                // 广播新客户端的 Enter 给除自己以外的所有人
                ServerNetHandler.BroadcastExcept(ServerProtocol.PackEnter(clientfd.RemoteEndPoint!.ToString()!), clientfd);
                // 将所有已在线客户端的 Enter 发送给新客户端，使其能实例化老客户端
                ServerNetHandler.SyncExistingClientsTo(clientfd);

                // 异步接收该客户端的数据，数据到达后回调 ReceiveCallback
                clientfd.BeginReceive
                (
                    clientState.readBuffer,
                    0,
                    clientState.readBuffer.Length,
                    SocketFlags.None,
                    ReceiveCallback,
                    clientState);

                // 继续异步等待下一个客户端连接（形成循环）
                listenfd!.BeginAccept(AcceptCallback, listenfd);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        /// <summary>
        /// 某个客户端发来数据时被调用。负责：读取数据 → 解析 → 广播 → 继续监听
        /// </summary>
        public static void ReceiveCallback(IAsyncResult ar)
        {
            try
            {
                ClientState? clientState = (ClientState?)ar.AsyncState;
                Socket clientfd = clientState!.socket;
                int bytesRead = clientfd.EndReceive(ar);

                // bytesRead==0 表示客户端主动断开连接
                if (bytesRead == 0)
                {
                    Console.WriteLine("Client Disconnected: " + clientfd.RemoteEndPoint!.ToString());
                    ServerNetHandler.RemoveClient(clientfd);
                    return;
                }

                // 将收到的字节解码为字符串并打印
                string receiveStr = System.Text.Encoding.Default.GetString(clientState.readBuffer, 0, bytesRead);
                Console.WriteLine("Received from " + clientfd.RemoteEndPoint!.ToString() + ": " + receiveStr);

                // 按 LineEnd 拆分后逐条处理，避免 TCP 粘包/半包问题
                string[] messages = receiveStr.Split(ServerProtocol.LineEnd);
                for (int i = 0; i < messages.Length - 1; i++)
                {
                    string msg = messages[i];
                    if (string.IsNullOrEmpty(msg)) continue;

                    string[] parts = msg.Split(ServerProtocol.Separator);
                    if (parts[0] == ServerMessageType.Move.ToString())
                        // 客户端发来位置更新，附加发送者地址后广播给所有人
                        ServerNetHandler.Broadcast(ServerProtocol.PackMove(clientfd.RemoteEndPoint!.ToString()!, parts[1]));
                    else if (parts[0] == ServerMessageType.Leave.ToString())
                    {
                        // 客户端主动发送 Leave，广播给其他人后清理连接
                        ServerNetHandler.RemoveClient(clientfd);
                        return;
                    }
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
    }
}
