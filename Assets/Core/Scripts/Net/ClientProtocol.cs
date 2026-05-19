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

public class ClientProtocol
{
    public const char Separator = '|';
    public const char LineEnd = '\n';
    public const char ArgSeparator = ',';

    public static string PackMove(float x, float y, float z)
    {
        return ClientMessageName.Move.ToString() + Separator + x + "," + y + "," + z + LineEnd;
    }

    public static bool Unpack(string rawMsg, out ClientMessageName msgName, out string args)
    {
        msgName = default;
        args = null;
        string[] split = rawMsg.Split(Separator);
        if (split.Length != 2) return false;
        if (!System.Enum.TryParse(split[0], out msgName)) return false;//将字符串解析为对应类型的枚举
        args = split[1];
        return true;
    }

    public static bool TryParseArg(string args, out string playerId, out float x, out float y, out float z)
    {
        playerId = "";
        x = y = z = 0;
        string[] parts = args.Split(ArgSeparator);
        if (parts.Length != 4) return false;
        playerId = parts[0];
        if (!float.TryParse(parts[1], out x)) return false;
        if (!float.TryParse(parts[2], out y)) return false;
        if (!float.TryParse(parts[3], out z)) return false;
        return true;
    }
}
