//PackMove: Move|x,y,z
//PackEnter: Enter|ip:port
//PackLeave: Leave|ip:port
//Unpack: Move|ip:port,x,y,z
public enum ClientMessageName
{
    Enter,
    Move,
    Leave,
}

public class ParsedMessage
{
    public ClientMessageName name;
    public string playerId;
    public float x, y, z;
}

public class ClientProtocol
{
    public const char Separator = '|';
    public const char LineEnd = '\n';
    public const char ArgSeparator = ',';

    public static string PackMove(float x, float y, float z)
    {
        return ClientMessageName.Move.ToString() + Separator + x + "," + y + "," + z + LineEnd;
    }

    public static bool Unpack(string rawMsg, out ParsedMessage msg)
    {
        msg = new ParsedMessage();
        string[] split = rawMsg.Split(Separator);
        if (split.Length != 2) return false;
        if (!System.Enum.TryParse(split[0], out msg.name)) return false;

        switch (msg.name)
        {
            case ClientMessageName.Move:
                return ParseMoveArgs(split[1], msg);
            case ClientMessageName.Enter:

            case ClientMessageName.Leave:
                msg.playerId = split[1];
                return true;
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
}
