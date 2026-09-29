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
            return;
        }

        nextConnectAttempt = Time.unscaledTime + reconnectInterval;
        manager.networkAddress = connectAddress;
        manager.StartClient();
        Debug.Log($"[AutoStartClient] Connecting to {connectAddress}");
    }
}
