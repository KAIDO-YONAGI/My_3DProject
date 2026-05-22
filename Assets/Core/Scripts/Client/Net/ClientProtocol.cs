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
        public ClientMessageType name;
        public string playerId;
        public float x, y, z;
    }

    public class PlayerInfo
    {
        public Vector3 position;
        public int modelID = 0;
        public SimpleCharacterAnimationState animationState =
            SimpleCharacterAnimationState.Land;
        public GameObject instance;
        public PlayerState playerState;
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

    public static class Protocol
    {
        public const char Separator = '|';
        public const char LineEnd = '\n';
        public const char ArgSeparator = ',';

        public static string PackEnter(string address, int modelID, string playerInfo)
        {
            return ClientMessageType.Enter.ToString() + Separator + address + Separator + modelID + LineEnd;
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
            if (!System.Enum.TryParse(split[0], out msg.name)) return false;

            switch (msg.name)
            {
                case ClientMessageType.Move:
                    return ParseMoveArgs(split[1], msg);
                case ClientMessageType.Enter:
                    msg.playerId = split[1];
                    return true;
                case ClientMessageType.Leave:
                    msg.playerId = split[1];
                    return true;
                case ClientMessageType.Attack:
                    return ParseAttackArgs(split[1], msg);
                default:
                    return false;
            }
        }

        static bool ParseMoveArgs(string args, ParsedMessage msg)
        {
            string[] parts = args.Split(ArgSeparator);
            if (parts.Length != 4) return false;
            msg.playerId = parts[0];
            if (!float.TryParse(parts[1], out msg.x)) return false;
            if (!float.TryParse(parts[2], out msg.y)) return false;
            if (!float.TryParse(parts[3], out msg.z)) return false;
            return true;
        }

        static bool ParseAttackArgs(string args, ParsedMessage msg)
        {
            msg.playerId = args.TrimEnd(LineEnd);
            return !string.IsNullOrEmpty(msg.playerId);
        }
    }
}
