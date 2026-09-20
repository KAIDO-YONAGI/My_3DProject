using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace MultiplayerServer
{
    // ServerMessageBroadcaster 负责一对多发送（广播）。
    // Broadcast / BroadcastExcept / SyncExistingClientsTo 都是遍历 clients 集合做多端发送。
    internal sealed class ServerMessageBroadcaster
    {
        private readonly ServerClientRegistry clientRegistry;
        private readonly ServerSocketSender socketSender;

        public ServerMessageBroadcaster(
            ServerClientRegistry clientRegistry,
            ServerSocketSender socketSender)
        {
            this.clientRegistry = clientRegistry;
            this.socketSender = socketSender;
        }

        /// <summary>
        /// 向所有已登记客户端广播消息
        /// </summary>
        public async Task Broadcast(string sendStr)
        {
            Console.WriteLine("[Broadcast] " + sendStr.TrimEnd(ServerProtocol.LineEnd));
            List<Task> sendTasks = new();
            foreach (var pair in clientRegistry.Clients)
            {
                sendTasks.Add(socketSender.SendTo(sendStr, pair.Value.endPoint));
            }

            await Task.WhenAll(sendTasks);
        }

        /// <summary>
        /// 向除指定客户端外的所有人广播消息
        /// </summary>
        public async Task BroadcastExcept(string sendStr, IPEndPoint exceptEndPoint)
        {
            Console.WriteLine("[BroadcastExcept] to " + Math.Max(0, clientRegistry.Count - 1)
                + " clients: " + sendStr.TrimEnd(ServerProtocol.LineEnd));
            string exceptAddress = ServerClientRegistry.GetRemoteAddress(exceptEndPoint);
            List<Task> sendTasks = new();
            foreach (var pair in clientRegistry.Clients)
            {
                if (pair.Key == exceptAddress) continue;
                sendTasks.Add(socketSender.SendTo(sendStr, pair.Value.endPoint));
            }

            await Task.WhenAll(sendTasks);
        }

        /// <summary>
        /// 将所有已在线客户端的 Enter 消息发送给指定目标（新连接的客户端）
        /// </summary>
        public async Task SyncExistingClientsTo(IPEndPoint target)
        {
            string targetAddress = ServerClientRegistry.GetRemoteAddress(target);
            List<Task> sendTasks = new();
            foreach (var pair in clientRegistry.Clients)
            {
                if (pair.Key == targetAddress) continue;
                if (!pair.Value.entered) continue;

                sendTasks.Add(socketSender.SendTo(ServerProtocol.PackEnter(
                    pair.Key,
                    pair.Value.modelID,
                    pair.Value.health,
                    pair.Value.damage), target));
            }

            await Task.WhenAll(sendTasks);
        }
    }
}
