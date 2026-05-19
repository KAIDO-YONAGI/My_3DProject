//Received from 127.0.0.1:11042: Move|-27.47568,2.840125,-6.122546

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
