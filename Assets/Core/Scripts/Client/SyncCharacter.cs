using UnityEngine;

public class SyncCharacter : MonoBehaviour
{
    public GameObject localCharacter;
    public float sendInterval = 0.1f;

    private float lastSendTime;

    void Start()
    {
        //On...为供mamager调用的回调函数
        NetManager.Instance.AddListener(ClientMessageName.Enter, OnEnter);
        NetManager.Instance.AddListener(ClientMessageName.Move, OnMove);
        NetManager.Instance.AddListener(ClientMessageName.Leave, OnLeave);
        NetManager.Instance.Connect("127.0.0.1", 8888);
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

    void OnEnter(string msg)
    {
        Debug.Log("OnEnter " + msg);
    }

    void OnMove(string msg)
    {
        Debug.Log("OnMove " + msg);
        if (!ClientProtocol.TryParseArg(msg, out float x, out float y, out float z)) return;
        // TODO: 用 x, y, z 更新远程角色位置
    }

    void OnLeave(string msg)
    {
        Debug.Log("OnLeave " + msg);
    }
}
