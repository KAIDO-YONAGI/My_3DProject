using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Net;

namespace MultiplayerServer
{
    // 所有已登记的 UDP 客户端集合
    internal sealed class ServerClientRegistry
    {
        private readonly ConcurrentDictionary<string, ClientState> clients = new();

        public int Count => clients.Count;
        public IEnumerable<KeyValuePair<string, ClientState>> Clients => clients;

        public ClientState GetOrAdd(IPEndPoint endPoint)
        {
            string address = GetRemoteAddress(endPoint);
            return clients.GetOrAdd(address, _ => new ClientState(endPoint));
        }

        public bool TryGet(
            IPEndPoint endPoint,
            [NotNullWhen(true)] out ClientState? clientState)
        {
            return clients.TryGetValue(GetRemoteAddress(endPoint), out clientState);
        }

        public bool TryRemove(
            IPEndPoint endPoint,
            [NotNullWhen(true)] out ClientState? clientState)
        {
            return clients.TryRemove(GetRemoteAddress(endPoint), out clientState);
        }

        public static string GetRemoteAddress(IPEndPoint endPoint)
        {
            return endPoint.ToString();
        }
    }
}
