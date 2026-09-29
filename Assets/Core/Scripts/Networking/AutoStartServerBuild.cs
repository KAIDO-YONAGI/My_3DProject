using System;
using UnityEngine;
using Mirror;

// 启动模式自动分流：
// - Dedicated Server Build（Application.isBatchMode）自动 StartServer，端口被占用时自动重试；
// - 普通客户端构建自动以 Client 连接目标地址（阶段一验证用，后续替换为连接界面）；
// - Unity 编辑器不自动启动，保留 NetworkManagerHUD 手动选择，便于双开测试。
public class AutoStartServerBuild : MonoBehaviour
{
    [Tooltip("客户端构建启动后自动连接的服务器地址。用 127.0.0.1 强制 IPv4：localhost 会解析为 ::1，而 KCP 服务器在部分机器上降级只绑 IPv4 时，IPv6 握手包会丢失。")]
    public string connectAddress = "127.0.0.1";

    [Tooltip("服务器端口被占用时的重试间隔（秒）")]
    public float serverRetryInterval = 2f;

    [Tooltip("客户端连接失败时的重试间隔（秒）")]
    public float clientRetryInterval = 3f;

    private float nextServerTry;
    private float nextClientTry;

    private void Update()
    {
        var nm = NetworkManager.singleton;
        if (nm == null) return;

        // Dedicated Server：StartServer 失败（端口占用等）不会停用组件，轮询重试
        if (Application.isBatchMode && !NetworkServer.active && Time.unscaledTime >= nextServerTry)
        {
            nextServerTry = Time.unscaledTime + serverRetryInterval;
            try
            {
                nm.StartServer();
                Debug.Log("[AutoStartServerBuild] Server started on port " +
                          (nm.transport is kcp2k.KcpTransport kcp ? kcp.port : 0));
            }
            catch (Exception e)
            {
                // KCP 绑定失败（端口被占用/防火墙）会抛 SocketException，记录后等待下一轮
                Debug.LogWarning("[AutoStartServerBuild] StartServer failed, retrying: " + e.Message);
            }
            return;
        }

#if !UNITY_EDITOR
        // 普通客户端构建：自动连接；断开后按间隔重连
        if (!Application.isBatchMode && !NetworkServer.active &&
            !NetworkClient.active && Time.unscaledTime >= nextClientTry)
        {
            nextClientTry = Time.unscaledTime + clientRetryInterval;
            nm.networkAddress = connectAddress;
            nm.StartClient();
            Debug.Log("[AutoStartServerBuild] Client auto-connecting to " + connectAddress);
        }
#endif
    }
}
