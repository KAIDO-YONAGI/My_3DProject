using Mirror;
using UnityEngine;

/// <summary>
/// 按运行环境的开关自动连接 NetworkManager 配置的地址，使用 Time.unscaledTime 安排重试。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class AutoStartClient : MonoBehaviour
{
    [Tooltip("Unity 编辑器进入运行模式后自动连接服务端。连接目标由 NetworkManager 的 Network Address 指定。")]
    [SerializeField]
    private bool autoConnectInEditor = true;

    [Tooltip("构建客户端启动后自动连接服务端。批处理进程和已启动的服务端保持各自的启动流程。")]
    [SerializeField]
    private bool autoConnectInPlayer = true;

    [Tooltip("两次连接尝试之间的最小间隔，单位为真实时间秒，最小值为 0.5。Mirror 客户端回到非活动状态后按此间隔重试。")]
    [SerializeField, Min(0.5f)]
    private float reconnectInterval = 3f;

    // 下次允许连接的未缩放时间，重试计时与游戏时间缩放独立。
    private float nextConnectAttempt;
    // 等待管理器期间记录是否已提示，找到管理器后重置。
    private bool hasReportedMissingManager;

    private void Update()
    {
        // 自动连接在普通客户端且 Mirror 空闲时执行；连接进行中也属于 NetworkClient.active。
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
        // 以当前尝试的起点计算下次允许时间，后续重试共用这一时间门槛。
        nextConnectAttempt = Time.unscaledTime + reconnectInterval;
        manager.StartClient();
        Debug.Log(
            $"[AutoStartClient] {(Application.isEditor ? "编辑器" : "构建客户端")} 正在连接 {manager.networkAddress}:{GetTransportPort(manager)}",
            this);
    }

    // 读取 KCP 端口供连接日志显示。
    private static int GetTransportPort(NetworkManager manager)
    {
        return manager.transport is kcp2k.KcpTransport kcpTransport
            ? kcpTransport.Port
            : -1;
    }
}
