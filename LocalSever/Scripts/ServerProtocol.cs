public enum ServerMessageName
{
    Enter,
    Move,
    Leave,
}

public class ServerProtocol
{
    public const char Separator = '|';
    public const char LineEnd = '\n';

    public static string PackEnter(string address)
    {
        return "Enter" + Separator + address + LineEnd;
    }

    public static string PackLeave(string address)
    {
        return "Leave" + Separator + address + LineEnd;
    }
}
