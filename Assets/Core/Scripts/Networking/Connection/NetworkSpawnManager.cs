using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Core.Scripts.Networking.Connection
{
    /// <summary>
    /// Mirror 网络管理器的生成位置扩展：在基类的出生点机制上加一层服务端占用记录，
    /// 让同时连接的玩家优先落在互不重复的出生点，全部被占用时才回到基类的随机/轮转规则。
    /// </summary>
    /// <remarks>
    /// <para>位置只由服务器决定：基类 OnServerAddPlayer 用 GetStartPosition 返回的 Transform 实例化玩家，
    /// 再把 Spawn 数据下发；客户端改本地坐标会被随后的状态同步覆盖。</para>
    /// <para>出生点仍由场景里的 NetworkStartPosition 在 Awake 时注册进 NetworkManager.startPositions，
    /// 所以只需把本组件替换到 NetworkManager.prefab 上，playerPrefab、Player Spawn Method 等配置沿用原值。</para>
    /// </remarks>
    public class NetworkSpawnManager : NetworkManager
    {
        // 连接编号 → 该连接占用的出生点。
        // 用连接编号而不是连接对象做键：断开重连会拿到新编号，不会把旧连接当成仍在使用。
        private readonly Dictionary<int, Transform> occupiedByConnection = new();

        /// <summary>
        /// 取出生点：优先返回第一个未被占用的，全部被占用时交给基类按 Player Spawn Method 决定。
        /// 场景里没有 NetworkStartPosition 时返回 null，调用方按「无出生点」处理（落在 Prefab 自身位置）。
        /// </summary>
        public override Transform GetStartPosition()
        {
            // 出生点随场景卸载被销毁，先剔除空引用，避免把玩家生成到已销毁的位置上。
            startPositions.RemoveAll(point => point == null);

            // 按注册顺序取第一个空闲出生点。RegisterStartPosition 已按 sibling index 排过序，
            // 因此顺序与层级面板一致，调位置时可以直接对照。
            foreach (Transform point in startPositions)
            {
                if (!IsOccupied(point))
                {
                    return point;
                }
            }

            // 玩家数多于出生点数：不拒绝生成，退回基类的随机/轮转，允许两人落在同一点。
            return startPositions.Count > 0 ? base.GetStartPosition() : null;
        }

        /// <summary>
        /// 服务器生成玩家：沿用基类的实例化与 AddPlayerForConnection 流程，
        /// 额外把选中的出生点登记为当前连接占用。
        /// </summary>
        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            // 与基类行为一致：一个连接只允许一个玩家对象，重复请求直接报错返回。
            if (conn.identity != null)
            {
                Debug.LogError("There is already a player for this connection.");
                return;
            }

            Transform startPosition = GetStartPosition();
            GameObject player = startPosition != null
                ? Instantiate(playerPrefab, startPosition.position, startPosition.rotation)
                : Instantiate(playerPrefab);

            // 命名沿用基类格式，日志和层级里能直接看出玩家属于哪个连接。
            player.name = $"{playerPrefab.name} [connId={conn.connectionId}]";

            // 生成失败时玩家对象没有进入网络世界：销毁它并输出日志，避免留下一个没人管的实例。
            if (!NetworkServer.AddPlayerForConnection(conn, player))
            {
                Debug.LogError(
                    $"[{nameof(NetworkSpawnManager)}] 连接 {conn.connectionId} 的玩家生成失败。",
                    this);
                Destroy(player);
                return;
            }

            // 生成成功后才登记占用；没有出生点（null）时不记录，此时所有玩家都按 Prefab 自身位置生成。
            if (startPosition != null)
            {
                occupiedByConnection[conn.connectionId] = startPosition;
            }
        }

        /// <summary>连接断开：释放该连接占用的出生点，再交给基类销毁玩家对象。</summary>
        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            // 先释放占用，即使基类的销毁流程提前返回，出生点也不会漏放。
            if (conn != null)
            {
                occupiedByConnection.Remove(conn.connectionId);
            }

            base.OnServerDisconnect(conn);
        }

        /// <summary>服务器停止：清空占用记录，下次启动从干净状态开始。</summary>
        public override void OnStopServer()
        {
            occupiedByConnection.Clear();
            base.OnStopServer();
        }

        // 判断某个出生点是否已被任一连接占用。
        // 玩家数受 maxConnections 限制，字典很小，线性查找比维护反向索引更不容易出错。
        private bool IsOccupied(Transform startPosition)
        {
            foreach (Transform usedPosition in occupiedByConnection.Values)
            {
                if (usedPosition == startPosition)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
