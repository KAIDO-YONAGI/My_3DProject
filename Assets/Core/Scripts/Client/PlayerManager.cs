using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{

    public List<GameObject> models;

    public static PlayerManager Instance { get; private set; }
    private List<string> playerToRefreshList = new();
    class OtherPlayerInfo
    {
        public Vector3 position;
        public int modelID = 0;
        public SimpleCharacterAnimationState animationState =
            SimpleCharacterAnimationState.Land;
        public GameObject instance;
        public PlayerState playerState;
    }
    private Dictionary<string, OtherPlayerInfo> players = new();

    class PlayerState
    {
        public int health;
        public int damage;
    }
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void InitPlayer(string playerId, Vector3 position)
    {
        if (players.ContainsKey(playerId)) return;
        var info = new OtherPlayerInfo();
        info.position = position;
        info.modelID = 0;
        info.instance = Instantiate(models[info.modelID], position, Quaternion.identity);
        players[playerId] = info;
        playerToRefreshList.Add(playerId);
    }

    public void SetPosition(string playerId, Vector3 position)
    {
        if (!players.TryGetValue(playerId, out var info)) return;
        info.position = position;
        playerToRefreshList.Add(playerId);
    }

    public Vector3 GetPosition(string playerId)
    {
        return players.TryGetValue(playerId, out var info) ? info.position : Vector3.zero;
    }

    public bool RemovePosition(string playerId)
    {
        if (!players.TryGetValue(playerId, out var info)) return false;
        if (info.instance != null)
            Destroy(info.instance);
        return players.Remove(playerId);
    }
    private void Update()
    {
        if (playerToRefreshList.Count <= 0) return;

        foreach (var playerId in playerToRefreshList)
        {
            // TODO: 刷新玩家显示
            players[playerId].instance.transform.position =
            players.TryGetValue(playerId, out var info) ? info.position : Vector3.zero;
        }
        playerToRefreshList.Clear();
    }

}
