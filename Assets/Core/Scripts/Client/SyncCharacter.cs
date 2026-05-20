using System.Collections;
using UnityEngine;

public class SyncCharacter : MonoBehaviour
{
    public GameObject localCharacter;
    public float sendInterval = 1f;
    private float lastSendTime;
    [SerializeField] BoolEventChannelSO connectResultChannel;
    [SerializeField] float reconnectDelay = 3f;
    bool connectResolved;
    bool lastConnectResult;
    string myPlayerId;

    void Start()
    {
        NetManager.Instance.AddListener(ClientMessageName.Enter, OnEnter);
        NetManager.Instance.AddListener(ClientMessageName.Move, OnMove);
        NetManager.Instance.AddListener(ClientMessageName.Leave, OnLeave);
        connectResultChannel.OnEventRaised += OnConnectResult;
        StartCoroutine(ConnectWithRetry());
    }

    void OnDestroy()
    {
        connectResultChannel.OnEventRaised -= OnConnectResult;
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
                //初始化本地端口，用于拒绝一些更新逻辑，
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
//TODO枚举类统一各个函数的入口
    void OnEnter(string msg)
    {
        if (!ClientProtocol.TryParseArg(msg, out string playerId, out float x, out float y, out float z)) return;
        if (playerId == myPlayerId) return;
        PlayerManager.Instance.InitPlayer(playerId, new Vector3(x, y, z));
        Debug.Log("OnEnter " + PlayerManager.Instance.GetPosition(playerId));

    }

    void OnMove(string msg)
    {
        if (!ClientProtocol.TryParseArg(msg, out string playerId, out float x, out float y, out float z)) return;
        if (playerId == myPlayerId) return;
        PlayerManager.Instance.SetPosition(playerId, new Vector3(x, y, z));
        Debug.Log("OnMove " + PlayerManager.Instance.GetPosition(playerId));

    }

    void OnLeave(string msg)
    {
        PlayerManager.Instance.RemovePosition(msg);
        Debug.Log("OnLeave " + msg);
    }
}
