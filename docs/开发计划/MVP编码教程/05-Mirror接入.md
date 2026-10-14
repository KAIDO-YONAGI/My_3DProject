# 05 Mirror 接入

## 本章解决的问题

客户端按下跳跃后，服务器如何确定是谁在跳？一个炸弹如何同时具有 Mirror 网络身份和教程中的业务编号？本章把连接、网络对象和自写协议接到一起。先把“连接属于谁”和“状态怎样传输”分开理解，再读两个完整类。

教程适配环境为 Unity `2022.3.62f3c1`、工程内置 Mirror、Input System `1.14.2` 和 URP `14.0.12`。下面的 `Unity/` 是教程代码文件的分组路径，代码命名空间均为 `Mvp`。

## 数据与职责

| 类型或字段 | 持有者与方向 | 本章中的具体职责 |
|---|---|---|
| `MvpNetwork : NetworkManager` | 服务器与客户端各自执行生命周期回调 | 启动世界、注册消息、创建玩家、清理连接 |
| `NetworkIdentity` | Mirror 网络对象 | Mirror 管理对象生成、销毁与连接归属；其 `netId` 与业务 `Id` 各有用途 |
| `MvpBody : NetworkBehaviour` | 每个业务网络对象 | 读取 `isServer`，承载四个同步身份字段，转发服务器碰撞 |
| `Id / Round / Generation / Kind` | 服务器通过 `SyncVar` 同步到客户端 | 让网络对象与 `BodyState` 对上业务编号、回合、代次和类别 |
| `MvpInput` | 客户端 → 服务器，可靠通道 | 回合、输入序号、方向与按钮位；玩家身份由连接确定 |
| `MvpBaseline / MvpMotion` | 服务器 → 客户端 | `Bytes` 携带第 03 章 `Wire` 编码的完整状态或运动差量 |
| `MvpRecover` | 客户端 → 服务器，可靠通道 | 请求可用基线；第 06 章检查玩家与恢复间隔 |
| `MvpExplosion` | 服务器 → 客户端，可靠通道 | 爆炸的回合、事件编号、模拟 Tick、来源编号和位置 |

`SyncVar` 解决网络对象上的字段同步。这里四个字段负责身份，位置、速度、血量和块归属走 `WorldState` 与自定义消息。`NetworkMessage` 是 Mirror 的消息接口，Mirror 负责消息外层的序列化和分发；其中 `Bytes` 的内部格式由 `Wire` 负责。自定义消息的接收函数按类型注册，服务器函数还获得发送它的 `conn`。

`RegisterHandler` 默认要求连接通过 Mirror 的认证状态检查，本章再检查 `conn.isReady` 和 `conn.identity`。`MvpInput` 里没有目标玩家编号：服务器从 `conn.identity` 取出 `MvpBody.Id`，随后让 `ReceiveInput` 检查回合、输入序号和数值。客户端只能通过这条处理链表达自己连接对应玩家的意图。

## 编写顺序与编译依赖

1. 对照第 01～04 章准备 `Model.cs`、`Structure.cs`、`Wire.cs` 和 `Replica.cs`，其中包含 `BodyKind`、`InputGate`、`Rules` 与客户端快照类型。
2. 编写下面的 `Unity/MvpNetwork.cs`，先看消息结构，再看服务器回调，最后看客户端回调。
3. 编写 `Unity/MvpBody.cs`，将它挂在统一网络 Prefab 根上，与唯一的 `NetworkIdentity` 放在一起。
4. 将 `World`、`View`、`BodyPrefab` 指向相应组件或 Prefab；第 06～09 章的完整类共同提供这些类型和方法。
5. 以第 05～09 章全部代码齐备作为 Unity 编译单位检查 Console。`MvpWorld.cs`、`MvpWorld.Players.cs`、`MvpWorld.Gameplay.cs` 的 `partial` 声明合成一个类，彼此直接访问私有字段和方法。第 05 章单独导入时，`MvpWorld`、`MvpView` 及其成员有跨章依赖。

Mirror 的 Weaver 在编译阶段生成消息与 `SyncVar` 的读写代码。普通 C# 编译与 Weaver 都成功，消息注册和字段同步才具备运行条件。这里的完整代码保留跨章调用，不把某一章裁成独立运行样例。

## 完整代码：消息和连接管理

```csharp file=Unity/MvpNetwork.cs
using Mirror;
using UnityEngine;

namespace Mvp
{
    // 输入包只传意图。Round 隔离回合，Sequence 供服务器拒绝重复或倒退序号。
    public struct MvpInput : NetworkMessage
    {
        public uint Round, Sequence;
        // 世界 X/Z 平面的方向分量；服务器限制数值范围并归一化对角方向。
        public float X, Z;
        // 位 0 表示跳跃，位 1 表示投掷；客户端缓存边沿，服务器按 Tick 消费。
        public byte Buttons;
    }
    // 完整基线携带对象列表、结构与运动状态，发送点指定可靠通道。
    public struct MvpBaseline : NetworkMessage { public byte[] Bytes; }
    // 运动包引用某个基线编号，发送点指定可丢弃通道；缺基线由 Replica 暂存。
    public struct MvpMotion : NetworkMessage { public byte[] Bytes; }
    // 恢复只报告客户端当前回合，服务器从连接身份确定申请者。
    public struct MvpRecover : NetworkMessage { public uint Round; }
    // 可靠事件携带发生时间；MvpView 根据播放 Tick 决定何时显示。
    public struct MvpExplosion : NetworkMessage
    {
        // Source 是触发来源业务编号，Event 在本回合递增。
        public uint Round, Event, Tick, Source;
        public Vector3 Position;
    }

    // 生命周期入口：世界模拟归 World，客户端输入与显示归 View。
    public sealed class MvpNetwork : NetworkManager
    {
        // 三个引用由场景配置提供，BodyPrefab 是全部业务类别共用的网络根。
        public MvpWorld World;
        public MvpView View;
        public MvpBody BodyPrefab;

        // 启动服务器时建立物理世界，并注册客户端到服务器的业务消息。
        public override void OnStartServer()
        {
            base.OnStartServer();
            World.Begin(this);
            NetworkServer.RegisterHandler<MvpInput>((conn, msg) =>
            {
                // 身份从已认证连接取得，客户端消息只表达操作意图。
                // ready、玩家身份与组件齐备才转交；msg 无法指定另一个玩家的 Id。
                if (conn.isReady && conn.identity != null &&
                    conn.identity.TryGetComponent<MvpBody>(out var body))
                    World.ReceiveInput(body.Id, msg);
            });
            NetworkServer.RegisterHandler<MvpRecover>((conn, msg) =>
            {
                // Recover 继续检查组件、玩家登记、回合与服务器全局恢复间隔。
                if (conn.isReady && conn.identity != null)
                    World.Recover(conn.identity.GetComponent<MvpBody>(), msg.Round);
            });
        }

        // Mirror 请求为连接添加玩家时，先填写业务身份并建立服务器角色组件。
        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            var body = Instantiate(BodyPrefab);
            World.AddPlayer(body);
            // 再交给 Mirror 绑定 conn.identity、生成对象并赋予该连接玩家归属。
            NetworkServer.AddPlayerForConnection(conn, body.gameObject);
        }

        // 先从业务字典移除玩家，再由基类执行 Mirror 的连接对象清理。
        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            if (conn.identity != null) World.RemovePlayer(conn.identity.GetComponent<MvpBody>());
            base.OnServerDisconnect(conn);
        }

        // 与 MvpWorld.OnDisable 共用 End，重复清理由世界自己的 running 标志拦住。
        public override void OnStopServer()
        {
            World.End();
            base.OnStopServer();
        }

        // 客户端连接启动时重置副本；每种业务消息只调用对应的显示层接收入口。
        public override void OnStartClient()
        {
            base.OnStartClient();
            View.ResetClient();
            // 基线和运动的解码归 Replica，爆炸的去重与定时播放归 MvpView。
            NetworkClient.RegisterHandler<MvpBaseline>(msg => View.ReceiveBaseline(msg.Bytes));
            NetworkClient.RegisterHandler<MvpMotion>(msg => View.ReceiveMotion(msg.Bytes));
            NetworkClient.RegisterHandler<MvpExplosion>(msg => View.ReceiveExplosion(msg));
        }

        // 清掉显示状态和业务身份索引，重新连接使用新的客户端副本。
        public override void OnStopClient()
        {
            View.ResetClient();
            MvpBody.ClientBodies.Clear();
            base.OnStopClient();
        }
    }
}
```

## 完整代码：网络对象身份与碰撞转发

`MvpBody` 的职责很窄。服务器负责填写身份；客户端 `OnStartClient` 用业务 `Id` 建索引；碰撞回调只在 `isServer` 成立时进入玩法。客户端独立显示根由第 09 章创建，不放在这个网络根的子层级里。

```csharp file=Unity/MvpBody.cs
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace Mvp
{
    public sealed class MvpBody : NetworkBehaviour
    {
        // 业务编号由世界分配，供快照、结构父编号和客户端查表共同使用。
        [SyncVar] public uint Id;
        // 回合重置保留玩家网络对象，同时更新所属回合。
        [SyncVar] public uint Round;
        // 快照身份还包含代次；本套创建代码使用默认代次 1。
        [SyncVar] public ushort Generation = 1;
        // 统一 Prefab 按类别建立服务器物理形状和客户端显示形状。
        [SyncVar] public BodyKind Kind;
        // 客户端已生成网络身份的业务索引，不承担运动快照存储。
        public static readonly Dictionary<uint, MvpBody> ClientBodies = new Dictionary<uint, MvpBody>();

        // Mirror 应用生成消息中的初始同步字段后，才调用此客户端回调。
        public override void OnStartClient()
        {
            ClientBodies[Id] = this;
        }
        // 网络身份退出时移除索引；独立显示根的消失时刻由播放拓扑确定。
        public override void OnStopClient()
        {
            ClientBodies.Remove(Id);
        }
        // 刚体碰撞发生在服务器 Physics.Simulate 中；炸弹触发仅进入爆炸队列。
        void OnCollisionEnter(Collision collision)
        {
            if (isServer) FindObjectOfType<MvpWorld>().Impact(this, collision);
        }
        // CharacterController.Move 触发角色接触，伤害检查由 Players 分部负责。
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (isServer) FindObjectOfType<MvpWorld>().CharacterImpact(this, hit);
        }
    }
}
```

## 手工追踪一次输入

设服务器当前为 `Round=2`、`Tick=100`，连接 A 的 `conn.identity` 上 `MvpBody.Id=7`。客户端发出 `Round=2, Sequence=41, X=1, Z=0, Buttons=1`，表示向右并请求跳跃。

1. Mirror 按 `MvpInput` 类型找到注册函数，把连接 A 与消息一起传入。
2. `conn.isReady` 为真，身份存在，组件查找得到业务编号 `7`；调用的是 `World.ReceiveInput(7, msg)`。
3. 第 07 章的玩家 7 存活，`InputGate` 的上一序号若为 40，则接受 41；保存方向 `(1,0,0)`、跳跃位和 `LastInput=100`。
4. 世界下一次 `FixedUpdate` 先令 `Tick=101`，再调用玩家 7 的 `MovePlayer`。站在地面时，跳跃位参与这一次移动，函数末尾把按钮清零。
5. 同一个序号 41 再到达，`InputGate` 因 `sequence <= LastSequence` 拒绝。连接 B 的玩家仍由连接 B 自己的 `identity` 决定。

对应的对象创建链为 `Instantiate → World.AddPlayer → AddPlayerForConnection`。`AddPlayer` 在 Mirror 生成前填写四个同步字段；普通客户端创建对象并应用同步字段后，`OnStartClient` 将它登记到 `ClientBodies[7]`。完整基线到达与对象生成是两条处理路径，第 09 章在首次建立显示根时检查二者是否匹配。

## 预期与排查

| 检查动作 | 代码对应的预期 | 优先检查位置 |
|---|---|---|
| 连接并添加玩家 | 一个连接绑定一个玩家身份，业务字典也登记该玩家 | `OnServerAddPlayer` 的三步顺序、`BodyPrefab` 引用 |
| 按一次跳跃 | 输入进入本连接玩家的 `ReceiveInput` | `conn.isReady`、`conn.identity`、`InputGate.Accept` |
| 对照身份和快照 | `Id / Round / Generation` 能与 `BodyState` 匹配 | Prefab 根的 `NetworkIdentity` 与 `MvpBody`、生成前字段赋值 |
| 状态先到而身份稍后到 | 副本保留状态，身份匹配后建立显示根 | 第 09 章 `DrawWorld` 的首次建根分支 |
| Unity 编译 | 第 01～09 章依赖齐备，Console 普通编译与 Weaver 通过 | 文件名、`Mvp` 命名空间、三个 `MvpWorld` 分部声明 |

下一章：[服务器世界](06-服务器世界.md)。
