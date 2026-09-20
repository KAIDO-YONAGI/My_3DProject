using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MultiplayerServer
{
    // 每个客户端对应一个状态对象，保存该客户端的 UDP 端点和玩家数据
    class ClientState
    {
        public IPEndPoint endPoint;
        public int modelID;
        public int health;
        public int damage;
        public bool entered;

        // 显式构造函数：所有字段在这里赋值，不依赖字段的隐式默认值或 null! 抑制
        public ClientState(IPEndPoint endPoint)
        {
            this.endPoint = endPoint;
            this.modelID = 0;
            this.health = 0;
            this.damage = 0;
            this.entered = false;
        }
    }

    class ServerCore
    {
        public static async Task Main()
        {
            // 创建 UDP 服务端并绑定到本地 127.0.0.1:8888
            IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 8888);
            using UdpClient server = new UdpClient(localEndPoint);
            ServerNetHandler.Initialize(server);

            Console.WriteLine("UDP Server ON");
            await ReceiveLoopAsync(server);
        }

        /// <summary>
        /// UDP 接收循环。每次 ReceiveAsync 返回一个完整数据报及其发送端点。
        /// </summary>
        public static async Task ReceiveLoopAsync(UdpClient server)
        {
            while (true)
            {
                try
                {
                    UdpReceiveResult result = await server.ReceiveAsync();
                    if (result.Buffer.Length == 0)
                    {
                        continue;
                    }

                    string receiveStr = Encoding.UTF8.GetString(result.Buffer);
                    Console.WriteLine("Received from " + result.RemoteEndPoint + ": " + receiveStr);

                    string[] messages = receiveStr.Split(
                        ServerProtocol.LineEnd,
                        StringSplitOptions.RemoveEmptyEntries);
                    foreach (string rawMessage in messages)
                    {
                        string message = rawMessage.TrimEnd('\r');
                        if (string.IsNullOrEmpty(message))
                        {
                            continue;
                        }

                        await ServerNetHandler.HandleMessage(message, result.RemoteEndPoint);
                    }
                }
                catch (ObjectDisposedException)
                {
                    return;
                }
                catch (SocketException e)
                {
                    Console.WriteLine(e);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }
    }
}
