//PackEnter:  Enter|ip:port\n
//PackLeave:  Leave|ip:port\n
//PackMove:   Move|ip:port,x,y,z\n

namespace MultiplayerServer
{
public enum ServerMessageType
{
    Enter,
    Move,
    Leave,
    Attack,
}

public class ServerProtocol
{
    public const char Separator = '|';
    public const char LineEnd = '\n';
    public const char ArgSeparator = ',';

    public static string PackEnter(string address)
    {
        return ServerMessageType.Enter.ToString() + Separator + address + LineEnd;
    }

    public static string PackLeave(string address)
    {
        return ServerMessageType.Leave.ToString() + Separator + address + LineEnd;
    }

    public static string PackMove(string address, string moveArgs)
    {
        return ServerMessageType.Move.ToString() + Separator + address + ArgSeparator + moveArgs + LineEnd;
    }
    public static string PackAttacked(string address, int damage)
    //���ش��������������Ҫ�㲥Ѫ����
    //�ͻ���Ҳ��Ҫ���ֵ����ÿ����ҵ�Ѫ��������������Ϣ��key=��ַ��
    //�������̣�
    // �ͻ��˹�������������������--
    // --�����˷��͹�������--
    // --�����ֱ��ת���㲥����������Ϣ--
    // --���пͻ��˽��������������ֵͬ������ʼ�����ͻ����൱���յ�ȷ����Ϣ��

    //��Ҫ���ӡ��޸ĵ�Э�飺
    // �ͻ��˵� Enter ����modelID+playerState��
    // �ͻ��˵� Attack ֻ����ַ��
    // ����˵� Enter �Գơ�
    // ����˵� Attack �㲥Ѫ��
    //TODOѪ����λ�á����������������루���棩��ע����ɷ����ͳһ����

    {
        return ServerMessageType.Attack.ToString() + Separator + address + damage.ToString() + LineEnd;
    }
}
}
