using UnityEngine;
using ClientProtocol;

public class ClientMessageHandler
{
    public static void OnEnter(ParsedMessage msg)
    {
        // if (msg.playerId == myPlayerId || string.IsNullOrEmpty(msg.playerId)) return;
        //服务端已经排除重复enter
        PlayerManager.Instance.InitPlayer(msg.playerId, Vector3.zero);
        Debug.Log("OnEnter " + msg.playerId);
    }

    public static void OnMove(ParsedMessage msg, string myPlayerId)
    {
        if (msg.playerId == myPlayerId || string.IsNullOrEmpty(msg.playerId)) return;
        PlayerManager.Instance.SetPosition(msg.playerId, new Vector3(msg.x, msg.y, msg.z));
        Debug.Log("OnMove " + "Meaasge: " + msg + "Setted: " + PlayerManager.Instance.GetPosition(msg.playerId));
    }

    public static void OnLeave(ParsedMessage msg)
    {
        PlayerManager.Instance.RemovePosition(msg.playerId);
        Debug.Log("OnLeave " + msg.playerId);
    }
}
