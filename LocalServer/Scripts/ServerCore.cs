using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MultiplayerServer
{
    // 每个客户端对应一个状态对象，保存该客户端的 socket 和接收缓冲区
    class ClientState
    {
        public Socket socket;
        public byte[] readBuffer;
        public int modelID;
        public int health;
        public int damage;
        public bool entered;
        public StringBuilder pendingMessages;
        public SemaphoreSlim sendLock;

        // 显式构造函数：所有字段在这里赋值，不依赖字段的隐式默认值或 null! 抑制
        public ClientState(Socket socket)
        {
            this.socket = socket;
            this.readBuffer = new byte[1024];
            this.modelID = 0;
            this.health = 0;
            this.damage = 0;
            this.entered = false;
            this.pendingMessages = new StringBuilder();
            this.sendLock = new SemaphoreSlim(1, 1);
        }
    }

    class ServerCore
    {
        static Socket listenfd;

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

            // 异步等待客户端连接，连接成功后进入接受连接循环
            await AcceptLoopAsync();
        }

        /// <summary>
        /// 接受连接循环。负责：接受连接 → 建档 → 注册接收 → 继续等待下一个连接
        /// </summary>
        public static async Task AcceptLoopAsync()
        {
            while (true)
            {
                try
                {
                    Socket clientfd = await listenfd.AcceptAsync();
                    Console.WriteLine("Client Connected: " + clientfd.RemoteEndPoint!.ToString());

                    ClientState clientState = new ClientState(clientfd);
                    if (!ServerNetHandler.TryAddClient(clientState))
                    {
                        clientfd.Close();
                        continue;
                    }

                    // 专门写法：丢弃返回值（_ =）。
                    // 这里故意不 await：每个客户端的接收循环必须并发独立运行，
                    // 不能让一个客户端的接收阻塞下一个连接的接受。
                    // 代价是这个 Task 抛出的异常不会被本循环捕获，需在 ReceiveLoopAsync 内部自行处理。
                    _ = ReceiveLoopAsync(clientState);
                }
                catch (ObjectDisposedException)
                {
                    // 监听 socket 被关闭时退出循环
                    return;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        // 从 pendingMessages 中切出第一条以 LineEnd('\n') 结尾的完整消息。
        // out message：找到时为去掉 '\n' 的消息内容；找不到时为 string.Empty。
        // 副作用：找到时会把该消息及其末尾 '\n' 从 pendingMessages 移除。
        static bool TryReadMessage(StringBuilder pendingMessages, out string message)
        {
            for (int i = 0; i < pendingMessages.Length; i++)
            {
                if (pendingMessages[i] != ServerProtocol.LineEnd)
                {
                    continue;
                }

                message = pendingMessages.ToString(0, i);
                pendingMessages.Remove(0, i + 1);
                return true;
            }

            message = string.Empty;
            return false;
        }

        /// <summary>
        /// 某个客户端的接收循环。负责：读取数据 → 拼接缓冲 → 切分消息 → 分发 → 继续接收
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

                    // 对端正常关闭连接时，ReceiveAsync 返回 0
                    if (bytesRead == 0)
                    {
                        Console.WriteLine("Client Disconnected: " + clientfd.RemoteEndPoint!.ToString());
                        await ServerNetHandler.RemoveClient(clientfd);
                        return;
                    }

                    // 显式指定 UTF-8：不依赖系统区域设置，避免跨机器/英文系统乱码
                    string receiveStr = Encoding.UTF8.GetString(clientState.readBuffer, 0, bytesRead);
                    Console.WriteLine("Received from " + clientfd.RemoteEndPoint!.ToString() + ": " + receiveStr);

                    // 把本次收到的字节追加到待处理缓冲，再尝试切出所有完整消息
                    clientState.pendingMessages.Append(receiveStr);
                    while (TryReadMessage(clientState.pendingMessages, out string msg))
                    {
                        if (string.IsNullOrEmpty(msg))
                        {
                            continue;
                        }

                        // HandleMessage 返回 false 表示该连接的消息流应终止（如收到 Leave）
                        bool keepReceiving = await ServerNetHandler.HandleMessage(msg, clientfd);
                        if (!keepReceiving)
                        {
                            return;
                        }
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
