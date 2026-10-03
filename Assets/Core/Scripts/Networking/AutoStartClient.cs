using Mirror;
using UnityEngine;

/// <summary>
/// 按编辑器和构建客户端的开关自动连接 Mirror，使用真实时间安排连接重试。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class AutoStartClient : MonoBehaviour
{
    [Tooltip("Unity 编辑器进入运行模式后自动连接服务端。连接目标由 Connect Address 指定。")]
    [SerializeField]
    private bool autoConnectInEditor = true;

    [Tooltip("构建客户端启动后自动连接服务端。批处理进程和已启动的服务端保持各自的启动流程。")]
    [SerializeField]
    private bool autoConnectInPlayer = true;

    [Tooltip("服务端的 IP 地址或主机名。127.0.0.1 表示当前计算机；连接端口由 NetworkManager 的 KcpTransport.Port 配置。")]
    [SerializeField]
    private string connectAddress = "127.0.0.1";

    [Tooltip("两次连接尝试之间的最小间隔，单位为真实时间秒，最小值为 0.5。Mirror 客户端回到非活动状态后按此间隔重试。")]
    [SerializeField, Min(0.5f)]
    private float reconnectInterval = 3f;

    // 下次允许发起连接的 Time.unscaledTime 时间戳，游戏时间缩放保持独立。
    private float nextConnectAttempt;
    // 等待 NetworkManager 期间只输出一次提示，找到管理器后重新允许提示。
    private bool hasReportedMissingManager;

    private void Update()
    {
        // NetworkClient.active 包含连接进行中的状态，当前尝试结束后才能再次 StartClient。
        if (Application.isBatchMode || NetworkServer.active || NetworkClient.active)
        {
            return;
        }

#if UNITY_EDITOR
        if (!autoConnectInEditor)
        {
            return;
        }
#else
        if (!autoConnectInPlayer)
        {
            return;
        }
#endif

        if (Time.unscaledTime < nextConnectAttempt)
        {
            return;
        }

        NetworkManager manager = NetworkManager.singleton;
        if (manager == null)
        {
            if (!hasReportedMissingManager)
            {
                hasReportedMissingManager = true;
                Debug.LogWarning("[AutoStartClient] NetworkManager.singleton 尚未准备好，等待下一次重试。", this);
            }

            return;
        }

        hasReportedMissingManager = false;
        // 以发起尝试的时刻计算重试间隔，让连接失败和断线后的重试使用同一计时规则。
        nextConnectAttempt = Time.unscaledTime + reconnectInterval;
        manager.networkAddress = connectAddress;
        manager.StartClient();
        Debug.Log(
            $"[AutoStartClient] {(Application.isEditor ? "编辑器" : "构建客户端")} 正在连接 {connectAddress}:{GetTransportPort(manager)}",
            this);
    }

    // 只用于诊断编辑器与构建版本是否使用了同一份 KCP 端口配置。
    private static int GetTransportPort(NetworkManager manager)
    {
        return manager.transport is kcp2k.KcpTransport kcpTransport
            ? kcpTransport.Port
            : -1;
    }
}
