using System.Collections.Generic;
using UnityEngine;

public class PlayerPositionManager : MonoBehaviour
{
    public static PlayerPositionManager Instance { get; private set; }

    private Dictionary<string, Vector3> Positions = new();

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Positions = new Dictionary<string, Vector3>();
    }

    public void SetPosition(string playerId, Vector3 position)
    {
        Positions[playerId] = position;
        Debug.Log($"SetPosition: {playerId} -> {position}");
    }

    public Vector3 GetPosition(string playerId)
    {
        return Positions.TryGetValue(playerId, out var pos) ? pos : Vector3.zero;
    }

    public bool RemovePosition(string playerId)
    {
        return Positions.Remove(playerId);
    }
}
