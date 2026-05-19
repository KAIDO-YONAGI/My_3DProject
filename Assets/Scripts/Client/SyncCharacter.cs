using UnityEngine;

public class SyncCharacter : MonoBehaviour
{
    public GameObject localCharacter;
    public float sendInterval = 0.1f;

    private float lastSendTime;

    void Start()
    {
        //On...为供mamager调用的回调函数
        NetManager.Instance.AddListener(MessageName.Enter, OnEnter);
        NetManager.Instance.AddListener(MessageName.Move, OnMove);
        NetManager.Instance.AddListener(MessageName.Leave, OnLeave);
        NetManager.Instance.Connect("127.0.0.1", 8888);
    }

    void Update()
    {
        if (localCharacter != null && Time.time - lastSendTime > sendInterval)
        {
            lastSendTime = Time.time;
            Vector3 pos = localCharacter.transform.position;
            NetManager.Instance.Send("Move|" + pos.x + "," + pos.y + "," + pos.z + "\n");
        }
    }

    void OnEnter(string msg)
    {
        Debug.Log("OnEnter " + msg);
    }

    void OnMove(string msg)
    {
        Debug.Log("OnMove " + msg);
        string[] xyz = msg.Split(',');
        float x = float.Parse(xyz[0]);
        float y = float.Parse(xyz[1]);
        float z = float.Parse(xyz[2]);
        // TODO: 用 x, y, z 更新远程角色位置
    }

    void OnLeave(string msg)
    {
        Debug.Log("OnLeave " + msg);
    }
}
