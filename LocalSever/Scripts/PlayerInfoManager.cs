using System.Collections.Generic;
using System.Numerics;
public class PlayerInfoManager
{
    public class PlayerState
    {
        public int Health { get; set; } = 100;
        public int Damage { get; set; } = 0;
    }
    public class PlayerInfo
    {
        public Vector3 Position { get; set; } = Vector3.Zero;
        public int ModelID { get; set; } = 0;
        public string State { get; set; } = "idle";
        public PlayerState PlayerState { get; set; } = new();
    }

    private readonly Dictionary<string, PlayerInfo> playerInfo = [];

    public Dictionary<string, PlayerInfo> GetAllPlayerInfo() => playerInfo;

    public bool TryGetPlayerInfo(string playerId, out PlayerInfo? info) => playerInfo.TryGetValue(playerId, out info);

    public void SetPlayerInfo(string playerId, PlayerInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        playerInfo[playerId] = info;
    }

    public void RemovePlayer(string playerId) => playerInfo.Remove(playerId);

    
}
