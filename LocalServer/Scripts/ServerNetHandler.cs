using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace MultiplayerServer
{
    // ServerNetHandler 只协调协议分发与连接清理。
    // 客户端状态、单播发送和广播分别由独立类型管理。
    internal static class ServerNetHandler
    {
        private static readonly ServerClientRegistry clientRegistry = new ServerClientRegistry();
        private static readonly ServerSocketSender socketSender =
            new ServerSocketSender(clientRegistry, RemoveClient);
        private static readonly ServerMessageBroadcaster messageBroadcaster =
            new ServerMessageBroadcaster(clientRegistry, socketSender);

        public static bool TryAddClient(ClientState clientState)
        {
            return clientRegistry.TryAdd(clientState);
        }

        /// <summary>
        /// 广播 Leave 并移除客户端连接
        /// </summary>
        public static async Task RemoveClient(Socket clientfd)
        {
            // 已不在集合中则无需重复清理（防止多处重复调用导致多次广播 Leave）
            if (!clientRegistry.TryRemove(clientfd, out _))
            {
                return;
            }

            string address = ServerClientRegistry.GetRemoteAddress(clientfd);

            // Shutdown 可能因 socket 已关闭/断开而抛异常，属正常情况，记日志即可
            try
            {
                clientfd.Shutdown(SocketShutdown.Both);
            }
            catch (Exception e)
            {
                Console.WriteLine("Shutdown ignored for " + address + ": " + e.GetType().Name);
            }

            // Close 同样可能抛异常（如已关闭），记日志即可
            try
            {
                clientfd.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine("Close ignored for " + address + ": " + e.GetType().Name);
            }

            if (!string.IsNullOrEmpty(address))
            {
                await messageBroadcaster.Broadcast(ServerProtocol.PackLeave(address));
            }
        }

        /// <summary>
        /// 处理客户端发来的消息，根据消息类型进行分发
        /// </summary>
        public static async Task<bool> HandleMessage(string msg, Socket clientfd)
        {
            string[] parts = msg.Split(ServerProtocol.Separator);
            if (parts.Length != 2) return true;

            if (parts[0] == ServerMessageType.Move.ToString())
            {
                // 客户端发来位置更新，附加发送者地址后广播给所有人
                string address = ServerClientRegistry.GetRemoteAddress(clientfd);
                if (!string.IsNullOrEmpty(address))
                {
                    await messageBroadcaster.Broadcast(ServerProtocol.PackMove(address, parts[1]));
                }
            }
            else if (parts[0] == ServerMessageType.Leave.ToString())
            {
                // 客户端主动发送 Leave，广播给其他人后清理连接
                await RemoveClient(clientfd);
                //表示消息流结束了
                return false;
            }
            else if (parts[0] == ServerMessageType.Enter.ToString())
            {
                string[] args = parts[1].Split(ServerProtocol.ArgSeparator);
                if (args.Length != 3) return true;
                if (!clientRegistry.TryGet(clientfd, out ClientState? state)) return true;
                if (!int.TryParse(args[0], out state.modelID)) return true;
                if (!int.TryParse(args[1], out state.health)) return true;
                if (!int.TryParse(args[2], out state.damage)) return true;
                state.entered = true;

                string address = ServerClientRegistry.GetRemoteAddress(clientfd);
                if (string.IsNullOrEmpty(address)) return true;

                // 广播新客户端的 Enter 给除自己以外的所有人
                await messageBroadcaster.BroadcastExcept(
                    ServerProtocol.PackEnter(address, state.modelID, state.health, state.damage),
                    clientfd);
                // 将所有已在线客户端的 Enter 发送给新客户端
                await messageBroadcaster.SyncExistingClientsTo(clientfd);
            }
            return true;
        }
    }
}
