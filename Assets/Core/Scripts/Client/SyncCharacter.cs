using UnityEngine;

public class SyncCharacter : MonoBehaviour
{
    public GameObject localCharacter;
    public float sendInterval = 1f;

    private float lastSendTime;

    void Start()
    {
        //On...为供mamager调用的回调函数
        NetManager.Instance.AddListener(ClientMessageName.Enter, OnEnter);
        NetManager.Instance.AddListener(ClientMessageName.Move, OnMove);
        NetManager.Instance.AddListener(ClientMessageName.Leave, OnLeave);
        NetManager.Instance.Connect("127.0.0.1", 8888);//TODO多次尝试链接
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
        if (!ClientProtocol.TryParseArg(msg, out string playerId, out float x, out float y, out float z)) return;
        PlayerPositionManager.Instance.SetPosition(playerId, new Vector3(x, y, z));
        Debug.Log("OnMove " + PlayerPositionManager.Instance.GetPosition(playerId));

    }

    void OnLeave(string msg)
    {
        PlayerPositionManager.Instance.RemovePosition(msg);
        Debug.Log("OnLeave " + msg);
    }
}
