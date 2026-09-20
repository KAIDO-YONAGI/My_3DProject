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

        public static string PackEnter(string address, int modelID, int health, int damage)
        {
            return ServerMessageType.Enter.ToString() + Separator + address + ArgSeparator
                + modelID + ArgSeparator + health + ArgSeparator + damage + LineEnd;
        }

        public static string PackLeave(string address)
        {
            return ServerMessageType.Leave.ToString() + Separator + address + LineEnd;
        }

        public static string PackMove(string address, string moveArgs)
        //线性插值+预测回滚解决带宽问题
        //move这种高频通信使用UDP协议
        //传移动状态，初步构思为：客户端等待一次收包时间，如果收到的状态回报依旧，那就继续播放动画，否则切出动画
        {
            return ServerMessageType.Move.ToString() + Separator + address + ArgSeparator + moveArgs + LineEnd;
        }
        public static string PackAttacked(string address, int damage)
        //返回处理结果，并且需要广播血量，
        //客户端也需要有字典管理每个玩家的血量、攻击力等信息（key=地址）
        //大体流程：
        // 客户端攻击，并且搜索到敌人--
        // --向服务端发送攻击请求--
        // --服务端直接转发广播这条攻击信息--
        // --所有客户端将被攻击玩家生命值同步（初始发包客户端相当于收到确认信息）

        //需要添加、修改的协议：
        // 客户端的 Enter 发送modelID+playerState、
        // 客户端的 Attack 只发地址、
        // 服务端的 Enter 对称、
        // 服务端的 Attack 广播血量
        //TODO血量、位置、攻击力、攻击距离（缓存）在注册后由服务端统一管理

        {
            return ServerMessageType.Attack.ToString() + Separator + address + damage.ToString() + LineEnd;
        }
    }
}
