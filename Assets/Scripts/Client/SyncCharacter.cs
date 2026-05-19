using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SyncCharacter : MonoBehaviour
{
    void Start()
    {
        NetManager.AddListener(MessageName.Enter, OnEnter);
        NetManager.AddListener(MessageName.Move, OnMove);
        NetManager.AddListener(MessageName.Leave, OnLeave);
        NetManager.Connect("127.0.0.1", 8888);
    }
    void OnEnter(string msg)
    {
        Debug.Log("OnEnter" + msg);
    }
    void OnMove(string msg)
    {
        Debug.Log("OnMove" + msg);
    }
    void OnLeave(string msg)
    {
        Debug.Log("OnLeave" + msg);

    }
}

