using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace MultiplayerServer
{
    // ServerNetHandler 只协调协议分发与 UDP 客户端登记清理。
    internal static class ServerNetHandler
    {
        private static readonly ServerClientRegistry clientRegistry = new ServerClientRegistry();
        private static ServerMessageBroadcaster? messageBroadcaster;

        public static void Initialize(UdpClient server)
        {
            ServerSocketSender socketSender = new ServerSocketSender(server, clientRegistry);
            messageBroadcaster = new ServerMessageBroadcaster(clientRegistry, socketSender);
        }

        /// <summary>
        /// 广播 Leave 并移除客户端端点
        /// </summary>
        public static async Task RemoveClient(IPEndPoint clientEndPoint)
        {
            // 已不在集合中则无需重复清理（防止多处重复调用导致多次广播 Leave）
            if (!clientRegistry.TryRemove(clientEndPoint, out _))
            {
                return;
            }

            string address = ServerClientRegistry.GetRemoteAddress(clientEndPoint);
            if (!string.IsNullOrEmpty(address))
            {
                await GetBroadcaster().Broadcast(ServerProtocol.PackLeave(address));
            }
        }

        /// <summary>
        /// 处理客户端发来的消息，根据消息类型进行分发
        /// </summary>
        public static async Task HandleMessage(string msg, IPEndPoint clientEndPoint)
        {
            string[] parts = msg.Split(ServerProtocol.Separator);
            if (parts.Length != 2) return;

            if (parts[0] == ServerMessageType.Move.ToString())
            {
                if (!clientRegistry.TryGet(clientEndPoint, out ClientState? state) || !state.entered)
                {
                    return;
                }

                string address = ServerClientRegistry.GetRemoteAddress(clientEndPoint);
                await GetBroadcaster().Broadcast(ServerProtocol.PackMove(address, parts[1]));
            }
            else if (parts[0] == ServerMessageType.Leave.ToString())
            {
                // UDP 没有连接关闭事件，客户端主动发送 Leave 后由服务端移除端点。
                await RemoveClient(clientEndPoint);
            }
            else if (parts[0] == ServerMessageType.Enter.ToString())
            {
                string[] args = parts[1].Split(ServerProtocol.ArgSeparator);
                if (args.Length != 3) return;

                ClientState state = clientRegistry.GetOrAdd(clientEndPoint);
                if (!int.TryParse(args[0], out state.modelID)) return;
                if (!int.TryParse(args[1], out state.health)) return;
                if (!int.TryParse(args[2], out state.damage)) return;
                state.entered = true;

                string address = ServerClientRegistry.GetRemoteAddress(clientEndPoint);
                await GetBroadcaster().BroadcastExcept(
                    ServerProtocol.PackEnter(address, state.modelID, state.health, state.damage),
                    clientEndPoint);
                await GetBroadcaster().SyncExistingClientsTo(clientEndPoint);
            }
        }

        private static ServerMessageBroadcaster GetBroadcaster()
        {
            return messageBroadcaster
                ?? throw new InvalidOperationException("ServerNetHandler has not been initialized.");
        }
    }
}
