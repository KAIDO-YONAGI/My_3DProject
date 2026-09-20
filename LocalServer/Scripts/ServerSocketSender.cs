using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MultiplayerServer
{
    // ServerSocketSender 负责通过服务端 UDP socket 向单个客户端端点发送数据报。
    internal sealed class ServerSocketSender
    {
        private readonly UdpClient server;
        private readonly ServerClientRegistry clientRegistry;
        private readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);

        public ServerSocketSender(
            UdpClient server,
            ServerClientRegistry clientRegistry)
        {
            this.server = server;
            this.clientRegistry = clientRegistry;
        }

        /// <summary>
        /// 向单个客户端发送一个完整 UDP 数据报
        /// </summary>
        public async Task SendTo(string sendStr, IPEndPoint target)
        {
            if (!clientRegistry.TryGet(target, out _))
            {
                return;
            }

            byte[] sendBytes = Encoding.UTF8.GetBytes(sendStr);
            bool lockTaken = false;
            try
            {
                await sendLock.WaitAsync();
                lockTaken = true;
                await server.SendAsync(sendBytes, target);
                Console.WriteLine("Sent " + sendBytes.Length + " UDP bytes to client.");
            }
            catch (ObjectDisposedException)
            {
            }
            catch (SocketException e)
            {
                // UDP 发送错误不等同于客户端离线，避免因瞬时网络错误误删玩家。
                Console.WriteLine("UDP send failed to " + target + ": " + e.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                if (lockTaken)
                {
                    sendLock.Release();
                }
            }
        }
    }
}
