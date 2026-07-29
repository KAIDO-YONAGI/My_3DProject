using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;

namespace MultiplayerServer
{
    // 所有已连接的客户端集合
    internal sealed class ServerClientRegistry
    {
        private readonly ConcurrentDictionary<Socket, ClientState> clients = new();

        public int Count => clients.Count;
        public IEnumerable<KeyValuePair<Socket, ClientState>> Clients => clients;

        public bool TryAdd(ClientState clientState)
        {
            return clients.TryAdd(clientState.socket, clientState);
        }

        public bool TryGet(
            Socket socket,
            [NotNullWhen(true)] out ClientState? clientState)
        {
            return clients.TryGetValue(socket, out clientState);
        }

        public bool TryRemove(
            Socket socket,
            [NotNullWhen(true)] out ClientState? clientState)
        {
            return clients.TryRemove(socket, out clientState);
        }

        // 安全地取对端地址：socket 已关闭时 RemoteEndPoint 会抛 ObjectDisposedException
        public static string GetRemoteAddress(Socket socket)
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
    }
}
