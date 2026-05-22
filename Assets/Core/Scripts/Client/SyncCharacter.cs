using System.Collections;
using UnityEngine;

public class SyncCharacter : MonoBehaviour
{
    public GameObject localCharacter;
    private float sendInterval = .05f;
    private float lastSendTime;
    [SerializeField] BoolEventChannelSO connectResultChannel;
    [SerializeField] float reconnectDelay = 3f;
    bool connectResolved;
    bool lastConnectResult;
    string myPlayerId = "";

    void Start()
    {
        NetManager.Instance.AddListener(ClientMessageType.Enter, ClientMessageHandler.OnEnter);
        NetManager.Instance.AddListener(ClientMessageType.Move, msg => ClientMessageHandler.OnMove(msg, myPlayerId));
        NetManager.Instance.AddListener(ClientMessageType.Leave, ClientMessageHandler.OnLeave);
        connectResultChannel.OnEventRaised += OnConnectResult;
        StartCoroutine(ConnectWithRetry());
    }

    void OnDestroy()
    {
        connectResultChannel.OnEventRaised -= OnConnectResult;
        NetManager.Instance.Disconnect();
    }

    void OnConnectResult(bool success)
    {
        connectResolved = true;
        lastConnectResult = success;
    }

    IEnumerator ConnectWithRetry()//协程等事件触发并且尝试重连
    // TODO 断线重连
    {
        while (true)
        {
            connectResolved = false;
            NetManager.Instance.Connect("127.0.0.1", 8888);
            yield return new WaitUntil(() => connectResolved);

            if (lastConnectResult)
            {
                myPlayerId = NetManager.Instance.GetDescribe();
                //初始化本地端口，用于拒绝一些更新逻辑
                // TODO不过以后可能会有用（比如判断是否开G等非法手段修改客户端）
                Debug.Log("Connect succeeded, myPlayerId: " + myPlayerId);
                yield break;
            }

            Debug.Log($"Connect failed, retrying in {reconnectDelay}s...");
            yield return new WaitForSeconds(reconnectDelay);
        }
    }

    void Update()
    {
        if (localCharacter != null && Time.time - lastSendTime > sendInterval)
        {
            lastSendTime = Time.time;
            Vector3 pos = localCharacter.transform.position;
            NetManager.Instance.Send(ClientProtocol.PackMove(pos.x, pos.y, pos.z));
        }
    }

}
