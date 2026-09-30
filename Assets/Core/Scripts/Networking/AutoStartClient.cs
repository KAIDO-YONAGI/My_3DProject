using Mirror;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(100)]
public sealed class AutoStartClient : MonoBehaviour
{
    [SerializeField]
    private bool autoConnectInEditor = true;

    [SerializeField]
    private bool autoConnectInPlayer = true;

    [SerializeField]
    private string connectAddress = "127.0.0.1";

    [SerializeField, Min(0.5f)]
    private float reconnectInterval = 3f;

    private float nextConnectAttempt;
    private bool hasReportedMissingManager;

    private void Update()
    {
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
