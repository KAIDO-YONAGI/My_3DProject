using System.Collections.Generic;
using System.Numerics;

namespace MultiplayerServer
{
public class PlayerInfoManager
{

    public class PlayerInfo
    {
        public Vector3 Position;
        public int ModelID;
        public string State;
        public PlayerState PlayerState;

        public PlayerInfo(Vector3 position, int modelID, string state, PlayerState playerState)
        {
            Position = position;
            ModelID = modelID;
            State = state;
            PlayerState = playerState;
        }
    }
    public class PlayerState
    {
        public int Health;
        public int Damage;

        public PlayerState(int health, int damage)
        {
            Health = health;
            Damage = damage;
        }
    }
    private Dictionary<string, PlayerInfo> playerInfo = new();


    public Dictionary<string, PlayerInfo> GetAllPlayerInfo() => playerInfo;

    public bool TryGetPlayerInfo(string playerId, out PlayerInfo? info) => playerInfo.TryGetValue(playerId, out info);

    public void SetPlayerInfo(string playerId, PlayerInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        playerInfo[playerId] = info;
    }

    public void RemovePlayer(string playerId) => playerInfo.Remove(playerId);


}
}
