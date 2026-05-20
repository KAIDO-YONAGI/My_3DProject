//PackEnter:  Enter|ip:port\n
//PackLeave:  Leave|ip:port\n
//PackMove:   Move|ip:port,x,y,z\n

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
        return ServerMessageName.Enter.ToString() + Separator + address + LineEnd;
    }

    public static string PackLeave(string address)
    {
        return ServerMessageName.Leave.ToString() + Separator + address + LineEnd;
    }

    public static string PackMove(string address, string moveArgs)
    {
        return ServerMessageName.Move.ToString() + Separator + address + "," + moveArgs + LineEnd;
    }
}
