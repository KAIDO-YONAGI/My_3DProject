using System;
using System.Net;
using System.Net.Sockets;

namespace MultiplayerServer
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

                // 广播新客户端的 Enter 给除自己以外的所有人
                BroadcastExcept(ServerProtocol.PackEnter(clientfd.RemoteEndPoint!.ToString()!), clientfd);
                // 将所有已在线客户端（含自己）的 Enter 发送给新客户端，使其能实例化老客户端
                SyncExistingClientsTo(clientfd);

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
                    Broadcast(ServerProtocol.PackLeave(clientfd.RemoteEndPoint!.ToString()!));
                    clients.Remove(clientfd);
                    clientfd.Close();
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
                    if (parts[0] == ServerMessageName.Move.ToString())
                        // 客户端发来位置更新，附加发送者地址后广播给所有人
                        Broadcast(ServerProtocol.PackMove(clientfd.RemoteEndPoint!.ToString()!, parts[1]));
                    else if (parts[0] == ServerMessageName.Leave.ToString())
                    {
                        // 客户端主动发送 Leave，广播给其他人后清理连接
                        Broadcast(ServerProtocol.PackLeave(clientfd.RemoteEndPoint!.ToString()!));
                        clients.Remove(clientfd);
                        clientfd.Close();
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

        /// <summary>
        /// 将所有已在线客户端的 Enter 消息发送给指定目标（新连接的客户端）
        /// </summary>
        public static void SyncExistingClientsTo(Socket target)
        {
            foreach (var pair in clients)
            {
                SendTo(ServerProtocol.PackEnter(pair.Key.RemoteEndPoint!.ToString()!), target);
            }
        }

        /// <summary>
        /// 向单个客户端发送消息
        /// </summary>
        public static void SendTo(string sendStr, Socket target)
        {
            byte[] sendBytes = System.Text.Encoding.Default.GetBytes(sendStr);
            target.BeginSend(sendBytes, 0, sendBytes.Length, SocketFlags.None, SendCallback, target);
        }

        /// <summary>
        /// 向所有已连接客户端广播消息
        /// </summary>
        public static void Broadcast(string sendStr)
        {
            Console.WriteLine("[Broadcast] " + sendStr.TrimEnd(ServerProtocol.LineEnd));
            byte[] sendBytes = System.Text.Encoding.Default.GetBytes(sendStr);
            //TODO检查已清除、异常终止的socket
            foreach (var pair in clients)
            {
                pair.Value.socket.BeginSend(sendBytes, 0, sendBytes.Length, SocketFlags.None, SendCallback, pair.Value.socket);
            }
        }
        /// <summary>
        /// 向除指定客户端外的所有人广播消息
        /// </summary>
        public static void BroadcastExcept(string sendStr, Socket exceptSocket)
        {
            Console.WriteLine("[BroadcastExcept] to " + (clients.Count - 1) + " clients: " + sendStr.TrimEnd(ServerProtocol.LineEnd));
            byte[] sendBytes = System.Text.Encoding.Default.GetBytes(sendStr);

            foreach (var pair in clients)
            {
                if (pair.Key == exceptSocket) continue;

                pair.Value.socket.BeginSend(
                    sendBytes,
                    0,
                    sendBytes.Length,
                    SocketFlags.None,
                    SendCallback,
                    pair.Value.socket);
            }
        }
        /// <summary>
        /// Echo 数据发送完成后被调用，仅用于确认发送字节数
        /// </summary>
        public static void SendCallback(IAsyncResult ar)
        {
            try
            {
                Socket? clientfd = (Socket?)ar.AsyncState;
                int bytesSent = clientfd!.EndSend(ar);
                Console.WriteLine("Sent " + bytesSent + " bytes to client.");
            }
            catch (SocketException)
            {
                // 发送失败说明客户端已断开，广播 Leave 并清理
                Socket? clientfd = (Socket?)ar.AsyncState;
                Console.WriteLine("Client force closed: " + clientfd!.RemoteEndPoint);
                Broadcast(ServerProtocol.PackLeave(clientfd!.RemoteEndPoint!.ToString()!));
                clients.Remove(clientfd!);
                clientfd!.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }

}

