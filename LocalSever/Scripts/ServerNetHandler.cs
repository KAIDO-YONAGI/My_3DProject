using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace MultiplayerServer
{
    /// <summary>
    /// 网络广播与客户端集合管理
    /// </summary>
    class ServerNetHandler
    {
        // 所有已连接的客户端集合
        public static Dictionary<Socket, ClientState> clients = new();

        /// <summary>
        /// 广播 Leave 并移除客户端连接
        /// </summary>
        public static void RemoveClient(Socket clientfd)
        {
            Broadcast(ServerProtocol.PackLeave(clientfd.RemoteEndPoint!.ToString()!));
            clients.Remove(clientfd);
            clientfd.Close();
        }

        /// <summary>
        /// 将所有已在线客户端的 Enter 消息发送给指定目标（新连接的客户端）
        /// </summary>
        public static void SyncExistingClientsTo(Socket target)
        {
            foreach (var pair in clients)
            {
                if (pair.Key == target) continue;
                SendTo(ServerProtocol.PackEnter(pair.Key.RemoteEndPoint!.ToString()!), target);
            }
        }

        /// <summary>
        /// 向单个客户端发送消息
        /// </summary>
        public static void SendTo(string sendStr, Socket target)
        {
            byte[] sendBytes = System.Text.Encoding.Default.GetBytes(sendStr);
            target.BeginSend(sendBytes, 0, sendBytes.Length, SocketFlags.None, SendCallback, target);
        }

        /// <summary>
        /// 向所有已连接客户端广播消息
        /// </summary>
        public static void Broadcast(string sendStr)
        {
            Console.WriteLine("[Broadcast] " + sendStr.TrimEnd(ServerProtocol.LineEnd));
            byte[] sendBytes = System.Text.Encoding.Default.GetBytes(sendStr);
            foreach (var pair in clients)
            {
                pair.Value.socket.BeginSend(sendBytes, 0, sendBytes.Length, SocketFlags.None, SendCallback, pair.Value.socket);
            }
        }

        /// <summary>
        /// 向除指定客户端外的所有人广播消息
        /// </summary>
        public static void BroadcastExcept(string sendStr, Socket exceptSocket)
        {
            Console.WriteLine("[BroadcastExcept] to " + (clients.Count - 1) + " clients: " + sendStr.TrimEnd(ServerProtocol.LineEnd));
            byte[] sendBytes = System.Text.Encoding.Default.GetBytes(sendStr);
            foreach (var pair in clients)
            {
                if (pair.Key == exceptSocket) continue;
                pair.Value.socket.BeginSend(sendBytes, 0, sendBytes.Length, SocketFlags.None, SendCallback, pair.Value.socket);
            }
        }

        /// <summary>
        /// 处理客户端发来的消息，根据消息类型进行分发
        /// </summary>
        public static bool HandleMessage(string msg, Socket clientfd)
        {
            string[] parts = msg.Split(ServerProtocol.Separator);
            if (parts[0] == ServerMessageType.Move.ToString())
                // 客户端发来位置更新，附加发送者地址后广播给所有人
                Broadcast(ServerProtocol.PackMove(clientfd.RemoteEndPoint!.ToString()!, parts[1]));
            else if (parts[0] == ServerMessageType.Leave.ToString())
            {
                // 客户端主动发送 Leave，广播给其他人后清理连接
                RemoveClient(clientfd);
                return false;
            }
            return true;
        }

        /// <summary>
        /// 发送完成后被调用，用于确认发送字节数或处理发送失败
        /// </summary>
        public static void SendCallback(IAsyncResult ar)
        {
            try
            {
                Socket? clientfd = (Socket?)ar.AsyncState;
                int bytesSent = clientfd!.EndSend(ar);
                Console.WriteLine("Sent " + bytesSent + " bytes to client.");
            }
            catch (SocketException)
            {
                // 发送失败说明客户端已断开，广播 Leave 并清理
                Socket? clientfd = (Socket?)ar.AsyncState;
                Console.WriteLine("Client force closed: " + clientfd!.RemoteEndPoint);
                RemoveClient(clientfd!);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }
}
