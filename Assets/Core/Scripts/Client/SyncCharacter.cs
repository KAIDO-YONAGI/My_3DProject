using System.Collections;
using UnityEngine;
using ClientProtocol;

public class SyncCharacter : MonoBehaviour
{
    public GameObject localCharacter;
    private float sendInterval = .05f;
    private float lastSendTime;
    [SerializeField] BoolEventChannelSO connectResultChannel;
    [SerializeField] float reconnectDelay = 3f;
    [SerializeField] PlayerInitData playerInitData = new();
    bool connectResolved;
    bool lastConnectResult;
    string myPlayerId = "";

    void Start()
    {
        NetManager.Instance.AddListenerIntoList(ClientMessageType.Enter,
            ClientMessageHandler.OnEnter);
        NetManager.Instance.AddListenerIntoList(ClientMessageType.Move,
            msg => ClientMessageHandler.OnMove(msg, myPlayerId));
        //利用lambda包装为OnMove传入了Id引用，并且只向委托暴露指定的msg变量
        NetManager.Instance.AddListenerIntoList(ClientMessageType.Leave,
            ClientMessageHandler.OnLeave);
        connectResultChannel.OnEventRaised += OnConnectResult;
        StartCoroutine(ConnectWithRetry());
    }

    void OnDestroy()
    {
        connectResultChannel.OnEventRaised -= OnConnectResult;

        // 场景销毁时 NetManager 可能已先销毁，其 OnDestroy 会负责关闭连接。
        NetManager netManager = NetManager.Instance;
        if (netManager != null)
        {
            netManager.Disconnect();
        }
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
                Debug.Log("Connect succeeded, myPlayerId: " + myPlayerId);
                NetManager.Instance.Send(Protocol.PackEnter(playerInitData));
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
            NetManager.Instance.Send(Protocol.PackMove(pos.x, pos.y, pos.z));
        }
    }

}
