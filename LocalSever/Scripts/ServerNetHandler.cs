using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace MultiplayerServer
{
    /// <summary>
    /// 网络广播与客户端集合管理
    /// </summary>
    class ServerNetHandler
    {
        // 所有已连接的客户端集合
        public static ConcurrentDictionary<Socket, ClientState> clients = new();

        static string GetRemoteAddress(Socket socket)
        {
            try
            {
                return socket.RemoteEndPoint?.ToString() ?? string.Empty;
            }
            catch (ObjectDisposedException)
            {
                return string.Empty;
            }
        }

        /// <summary>
        /// 广播 Leave 并移除客户端连接
        /// </summary>
        public static async Task RemoveClient(Socket clientfd)
        {
            if (!clients.TryRemove(clientfd, out _)) return;

            string address = GetRemoteAddress(clientfd);
            try
            {
                clientfd.Shutdown(SocketShutdown.Both);
            }
            catch
            {
            }

            try
            {
                clientfd.Close();
            }
            catch
            {
            }

            if (!string.IsNullOrEmpty(address))
                await Broadcast(ServerProtocol.PackLeave(address));
        }

        /// <summary>
        /// 将所有已在线客户端的 Enter 消息发送给指定目标（新连接的客户端）
        /// </summary>
        public static async Task SyncExistingClientsTo(Socket target)
        {
            List<Task> sendTasks = new();
            foreach (var pair in clients)
            {
                if (pair.Key == target) continue;
                if (!pair.Value.entered) continue;
                sendTasks.Add(SendTo(ServerProtocol.PackEnter(
                    pair.Key.RemoteEndPoint!.ToString()!,
                    pair.Value.modelID, pair.Value.health, pair.Value.damage), target));
            }

            await Task.WhenAll(sendTasks);
        }

        /// <summary>
        /// 向单个客户端发送消息
        /// </summary>
        public static async Task SendTo(string sendStr, Socket target)
        {
            byte[] sendBytes = Encoding.Default.GetBytes(sendStr);
            await SendAllAsync(sendBytes, target);
        }

        /// <summary>
        /// 向所有已连接客户端广播消息
        /// </summary>
        public static async Task Broadcast(string sendStr)
        {
            Console.WriteLine("[Broadcast] " + sendStr.TrimEnd(ServerProtocol.LineEnd));
            List<Task> sendTasks = new();
            foreach (var pair in clients)
            {
                sendTasks.Add(SendTo(sendStr, pair.Value.socket));
            }

            await Task.WhenAll(sendTasks);
        }

        /// <summary>
        /// 向除指定客户端外的所有人广播消息
        /// </summary>
        public static async Task BroadcastExcept(string sendStr, Socket exceptSocket)
        {
            Console.WriteLine("[BroadcastExcept] to " + (clients.Count - 1) + " clients: " + sendStr.TrimEnd(ServerProtocol.LineEnd));
            List<Task> sendTasks = new();
            foreach (var pair in clients)
            {
                if (pair.Key == exceptSocket) continue;
                sendTasks.Add(SendTo(sendStr, pair.Value.socket));
            }

            await Task.WhenAll(sendTasks);
        }

        /// <summary>
        /// 处理客户端发来的消息，根据消息类型进行分发
        /// </summary>
        public static async Task<bool> HandleMessage(string msg, Socket clientfd)
        {
            string[] parts = msg.Split(ServerProtocol.Separator);
            if (parts.Length != 2) return true;

            if (parts[0] == ServerMessageType.Move.ToString())
                // 客户端发来位置更新，附加发送者地址后广播给所有人
                await Broadcast(ServerProtocol.PackMove(clientfd.RemoteEndPoint!.ToString()!, parts[1]));
            else if (parts[0] == ServerMessageType.Leave.ToString())
            {
                // 客户端主动发送 Leave，广播给其他人后清理连接
                await RemoveClient(clientfd);
                return false;//表示消息流结束了
            }
            else if(parts[0] == ServerMessageType.Enter.ToString())
            {
                string[] args = parts[1].Split(ServerProtocol.ArgSeparator);
                if (args.Length != 3) return true;
                if (!clients.TryGetValue(clientfd, out var state)) return true;
                if (!int.TryParse(args[0], out state.modelID)) return true;
                if (!int.TryParse(args[1], out state.health)) return true;
                if (!int.TryParse(args[2], out state.damage)) return true;
                state.entered = true;

                string address = clientfd.RemoteEndPoint!.ToString()!;
                // 广播新客户端的 Enter 给除自己以外的所有人
                await BroadcastExcept(ServerProtocol.PackEnter(address, state.modelID, state.health, state.damage), clientfd);
                // 将所有已在线客户端的 Enter 发送给新客户端
                await SyncExistingClientsTo(clientfd);
            }
            return true;
        }

        /// <summary>
        /// 发送完成后被调用，用于确认发送字节数或处理发送失败
        /// </summary>
        public static async Task SendAllAsync(byte[] sendBytes, Socket target)
        {
            if (!clients.TryGetValue(target, out var state)) return;

            bool lockTaken = false;
            try
            {
                await state.sendLock.WaitAsync();
                lockTaken = true;

                int totalSent = 0;
                while (totalSent < sendBytes.Length)
                {
                    int bytesSent = await target.SendAsync(
                        new ArraySegment<byte>(sendBytes, totalSent, sendBytes.Length - totalSent),
                        SocketFlags.None);
                    if (bytesSent <= 0)
                        throw new SocketException((int)SocketError.ConnectionReset);
                    totalSent += bytesSent;
                }

                Console.WriteLine("Sent " + totalSent + " bytes to client.");
            }
            catch (SocketException)
            {
                // 发送失败说明客户端已断开，广播 Leave 并清理
                Console.WriteLine("Client force closed: " + GetRemoteAddress(target));
                await RemoveClient(target);
            }
            catch (ObjectDisposedException)
            {
                await RemoveClient(target);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                await RemoveClient(target);
            }
            finally
            {
                if (lockTaken)
                    state.sendLock.Release();
            }
        }
    }
}
