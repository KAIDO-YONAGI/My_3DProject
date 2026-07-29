using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MultiplayerServer
{
    // ServerSocketSender 负责一对一发送。
    // SendTo 负责编码 + 转发；SendAllAsync 负责加锁循环发送直到字节发完。
    internal sealed class ServerSocketSender
    {
        private readonly ServerClientRegistry clientRegistry;
        private readonly Func<Socket, Task> removeClient;

        public ServerSocketSender(
            ServerClientRegistry clientRegistry,
            Func<Socket, Task> removeClient)
        {
            this.clientRegistry = clientRegistry;
            this.removeClient = removeClient;
        }

        /// <summary>
        /// 向单个客户端发送消息
        /// </summary>
        public async Task SendTo(string sendStr, Socket target)
        {
            // 显式指定 UTF-8：与 ReceiveLoopAsync 的解码端保持一致，避免依赖系统区域设置
            byte[] sendBytes = Encoding.UTF8.GetBytes(sendStr);
            await SendAllAsync(sendBytes, target);
        }

        /// <summary>
        /// 把 sendBytes 完整发送给 target（循环发送直到全部发完）。
        /// 加锁是为了让同一 socket 上的多次 SendAsync 串行化，
        /// 避免并发发送导致字节交错（TCP 流被多个发送者交叉写入）。
        /// </summary>
        private async Task SendAllAsync(byte[] sendBytes, Socket target)
        {
            if (!clientRegistry.TryGet(target, out ClientState? state))
            {
                return;
            }

            // 专门写法：lockTaken 标志位 + try/finally。
            // WaitAsync 可能在拿到锁之前就抛异常（如被取消），此时不能 Release，
            // 否则 SemaphoreSlim 的计数会失衡。所以用标志位记录"是否真的拿到了锁"。
            bool lockTaken = false;
            try
            {
                await state.sendLock.WaitAsync();
                lockTaken = true;

                // SendAsync 不保证一次发完全部字节，需要循环直到 totalSent == Length
                int totalSent = 0;
                while (totalSent < sendBytes.Length)
                {
                    int bytesSent = await target.SendAsync(
                        new ArraySegment<byte>(sendBytes, totalSent, sendBytes.Length - totalSent),
                        SocketFlags.None);
                    if (bytesSent <= 0)
                    {
                        // 返回 0 或负数视为连接已断开
                        throw new SocketException((int)SocketError.ConnectionReset);
                    }
                    totalSent += bytesSent;
                }

                Console.WriteLine("Sent " + totalSent + " bytes to client.");
            }
            catch (SocketException)
            {
                // 发送失败说明客户端已断开，广播 Leave 并清理
                Console.WriteLine("Client force closed: " + ServerClientRegistry.GetRemoteAddress(target));
                await removeClient(target);
            }
            catch (ObjectDisposedException)
            {
                await removeClient(target);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await removeClient(target);
            }
            finally
            {
                // 只有真正拿到锁才释放，保证计数平衡
                if (lockTaken)
                {
                    state.sendLock.Release();
                }
            }
        }
    }
}
