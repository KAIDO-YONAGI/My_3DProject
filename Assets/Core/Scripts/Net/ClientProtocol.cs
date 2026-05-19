//OnMove -23.40294,4.271813,-41.18822
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
        return "Move" + Separator + x + "," + y + "," + z + LineEnd;
    }

    public static bool TryParseArg(string args, out float x, out float y, out float z)
    {
        x = y = z = 0;
        string[] xyz = args.Split(ArgSeparator);
        if (xyz.Length != 3) return false;
        x = float.Parse(xyz[0]);
        y = float.Parse(xyz[1]);
        z = float.Parse(xyz[2]);
        return true;
    }
}
