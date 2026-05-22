//PackMove:  Move|x,y,z\n
//PackLeave: Leave|\n
//Unpack Move:    Move|ip:port,x,y,z
//Unpack Enter:   Enter|ip:port
//Unpack Leave:   Leave|ip:port
//Unpack Attack:  Attack|ip:port
using UnityEngine;

namespace ClientProtocol
{
    public enum ClientMessageType
    {
        Enter,
        Move,
        Leave,
        Attack,
    }

    public class ParsedMessage
    {
        public ClientMessageType clientMessageType;
        public string playerId;
        public PlayerInfo playerInfo = new();
    }
    public class PlayerInfo
    {
        public Vector3 position;
        public GameObject instance;
        public PlayerState playerState;
        public int modelID = 0;
        public SimpleCharacterAnimationState animationState =
                SimpleCharacterAnimationState.Land;
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

    public interface IProtocolSerializable
    {
        string Serialize();
        bool Deserialize(string data);
    }

    [System.Serializable]
    public class PlayerInitData : IProtocolSerializable
    {
        public int modelID;
        public int health;
        public int damage;
        const char ArgSeparator = ',';

        public string Serialize()
        {
            return modelID.ToString() + ArgSeparator + health + ArgSeparator + damage;
        }

        public bool Deserialize(string data)
        {
            string[] parts = data.Split(ArgSeparator);
            if (parts.Length != 3) return false;
            if (!int.TryParse(parts[0], out modelID)) return false;
            if (!int.TryParse(parts[1], out health)) return false;
            if (!int.TryParse(parts[2], out damage)) return false;
            return true;
        }

    }
    public static class Protocol
    {
        public const char Separator = '|';
        public const char LineEnd = '\n';
        public const char ArgSeparator = ',';

        public static string PackEnter(PlayerInitData initData)
        {
            return ClientMessageType.Enter.ToString() + Separator + initData.Serialize() + LineEnd;
        }

        public static string PackMove(float x, float y, float z)
        {
            return ClientMessageType.Move.ToString() + Separator + x + ArgSeparator + y + ArgSeparator + z + LineEnd;
        }

        public static string PackLeave()
        {
            return ClientMessageType.Leave.ToString() + Separator + LineEnd;
        }

        public static string PackAttack(string address)
        {
            return ClientMessageType.Attack.ToString() + Separator + address + LineEnd;
        }

        public static bool Unpack(string rawMsg, out ParsedMessage msg)
        {
            msg = new ParsedMessage();
            string[] split = rawMsg.Split(Separator);
            if (split.Length != 2) return false;
            if (!System.Enum.TryParse(split[0], out msg.clientMessageType)) return false;

            switch (msg.clientMessageType)
            {
                case ClientMessageType.Enter:
                    return ParseEnterArgs(split[1], msg);
                case ClientMessageType.Move:
                    return ParseMoveArgs(split[1], msg);
                case ClientMessageType.Attack:
                    return ParseAttackArgs(split[1], msg);

                case ClientMessageType.Leave:
                    msg.playerId = split[1];
                    return true;
                default:
                    return false;
            }
        }

        static bool ParseEnterArgs(string args, ParsedMessage msg)
        {
            string[] parts = args.Split(ArgSeparator);
            if (parts.Length != 4) return false;
            msg.playerId = parts[0];
            var initData = new PlayerInitData();
            if (!initData.Deserialize(parts[1] + ArgSeparator + parts[2] + ArgSeparator + parts[3])) return false;
            msg.playerInfo.modelID = initData.modelID;
            msg.playerInfo.playerState = new PlayerState(initData.health, initData.damage);
            return true;
        }
        static bool ParseMoveArgs(string args, ParsedMessage msg)
        {
            string[] parts = args.Split(ArgSeparator);
            if (parts.Length != 4) return false;
            msg.playerId = parts[0];
            if (!float.TryParse(parts[1], out msg.playerInfo.position.x)) return false;
            if (!float.TryParse(parts[2], out msg.playerInfo.position.y)) return false;
            if (!float.TryParse(parts[3], out msg.playerInfo.position.z)) return false;
            return true;
        }
        static bool ParseAttackArgs(string args, ParsedMessage msg)
        {
            msg.playerId = args.TrimEnd(LineEnd);
            return !string.IsNullOrEmpty(msg.playerId);
        }

    }
}
