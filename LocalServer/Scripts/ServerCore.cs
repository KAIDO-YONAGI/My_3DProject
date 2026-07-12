using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MultiplayerServer
{
    // 每个客户端对应一个状态对象，保存该客户端的 socket 和接收缓冲区
    class ClientState
    {
        public Socket socket = null!;
        public byte[] readBuffer = new byte[1024];
        public int modelID;
        public int health;
        public int damage;
        public bool entered = false;
        public readonly StringBuilder pendingMessages = new();
        public readonly SemaphoreSlim sendLock = new(1, 1);
    }

    class ServerCore
    {
        static Socket listenfd = null!;

        public static async Task Main()
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
            await AcceptLoopAsync();
        }

        /// <summary>
        /// 有新客户端连接时被调用。负责：接受连接 → 建档 → 注册接收 → 继续等待下一个连接
        /// </summary>
        public static async Task AcceptLoopAsync()
        {
            while (true)
            {
                try
                {
                    Socket clientfd = await listenfd.AcceptAsync();
                    Console.WriteLine("Client Connected: " + clientfd.RemoteEndPoint!.ToString());

                    ClientState clientState = new ClientState();
                    clientState.socket = clientfd;
                    if (!ServerNetHandler.clients.TryAdd(clientfd, clientState))
                    {
                        clientfd.Close();
                        continue;
                    }

                    _ = ReceiveLoopAsync(clientState);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        static bool TryReadMessage(StringBuilder pendingMessages, out string message)
        {
            for (int i = 0; i < pendingMessages.Length; i++)
            {
                if (pendingMessages[i] != ServerProtocol.LineEnd) continue;
                message = pendingMessages.ToString(0, i);
                pendingMessages.Remove(0, i + 1);
                return true;
            }

            message = string.Empty;
            return false;
        }

        /// <summary>
        /// 某个客户端发来数据时被调用。负责：读取数据 → 解析 → 广播 → 继续监听
        /// </summary>
        public static async Task ReceiveLoopAsync(ClientState clientState)
        {
            Socket clientfd = clientState.socket;
            try
            {
                while (true)
                {
                    int bytesRead = await clientfd.ReceiveAsync(
                        new ArraySegment<byte>(clientState.readBuffer),
                        SocketFlags.None);

                    if (bytesRead == 0)
                    {
                        Console.WriteLine("Client Disconnected: " + clientfd.RemoteEndPoint!.ToString());
                        await ServerNetHandler.RemoveClient(clientfd);
                        return;
                    }

                    string receiveStr = Encoding.Default.GetString(clientState.readBuffer, 0, bytesRead);
                    Console.WriteLine("Received from " + clientfd.RemoteEndPoint!.ToString() + ": " + receiveStr);

                    clientState.pendingMessages.Append(receiveStr);
                    while (TryReadMessage(clientState.pendingMessages, out string msg))
                    {
                        if (string.IsNullOrEmpty(msg)) continue;
                        if (!await ServerNetHandler.HandleMessage(msg, clientfd)) return;//消费消息
                    }
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine(e);
                await ServerNetHandler.RemoveClient(clientfd);
            }
            catch (ObjectDisposedException)
            {
                await ServerNetHandler.RemoveClient(clientfd);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await ServerNetHandler.RemoveClient(clientfd);
            }
        }
    }
}
