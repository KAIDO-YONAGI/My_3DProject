# Mirror 网络 API 总览

这份总览解决两个问题：**遇到一种联机需求，应该选什么；选定以后，代码怎样接起来。** 接口以本工程 `Assets/Plugins/Mirror` 中的实现为准。完整竞技场代码从 [05 Mirror 接入](05-Mirror接入.md) 继续，某个机制的深入解释从 [专项目录](专项设计/README.md) 查询。

先抓住一条链：本地输入表达意图，网络请求把意图交给服务器，服务器检查并写结果，客户端用结果更新画面。同一功能可以同时使用几种 API，例如角色选择使用 Command 请求、SyncVar 保存编号、普通方法装配模型。

## 1. 先按需求选

| 你要做什么 | 选什么 | 怎么实现 | 从哪里看例子 |
| --- | --- | --- | --- |
| 按钮调用同一客户端里的脚本 | 普通 C# 方法、事件 | 找到本地玩家或服务，直接调用；需要跨端的那一步再进入网络入口 | [当前角色接入](专项设计/现有角色与本地表现接入.md) |
| 玩家请求换角色、准备、使用道具 | `[Command]` | 在玩家 `NetworkBehaviour` 上接收，服务器校验参数并写状态 | [第 4 节 Command](#4-客户端请求服务器command)，`CharacterIdSync` |
| 多人请求操作公共机关 | 玩家 Command，或开放归属的公共对象 Command | 从玩家归属连接或注入的 `sender` 取真实请求者，检查距离、资格和机关状态 | [第 4 节公共对象例子](#4-客户端请求服务器command) |
| 所有人需要知道现在选谁、是否准备 | `[SyncVar]` | 服务器维护字段，客户端初始化读取并通过 hook 刷新 | [第 5 节 SyncVar](#5-当前状态下行syncvar-与-hook) |
| 同步房间名单、背包、弹药映射 | `SyncList`、`SyncDictionary` 等 | 维护集合实例，使用集合的增删改 API，客户端订阅操作变化 | [第 6 节同步集合](#6-名单与背包同步集合) |
| 播放一次确认音效或特效 | `[ClientRpc]` | 服务器调用对象上的 RPC，把表现所需参数发给当前就绪观察者 | [第 7 节一次通知](#7-一次通知clientrpc-与-targetrpc) |
| 给一个玩家返回拒绝提示 | `[TargetRpc]` | 服务器传入目标连接，目标客户端执行提示 | [第 7 节定向回复](#7-一次通知clientrpc-与-targetrpc) |
| 传房间查询、输入包、整批快照 | `NetworkMessage` | 定义消息、两端注册、选择通道发送、停止时解除注册 | [第 8 节消息](#8-连接级请求与批量数据networkmessage)，`MvpNetwork` |
| 让各端生成或删除一个网络实体 | `NetworkServer.Spawn / Destroy` | 服务器创建已注册 Prefab，填初始数据，再 Spawn；结束时网络销毁 | [第 9 节网络实体](#9-网络实体生命周期与归属) |
| 普通对象持续移动、旋转 | `NetworkTransformReliable` 等变换组件 | 明确运动计算方与同步方向，由接收方缓冲播放 | [第 10 节连续运动](#10-连续运动时间与发送频率)，当前 `NetworkPlayer` |
| 一批碎片共享基线、Tick 和拓扑 | 自写快照，通过消息承载 | `Wire` 编码，`Replica` 缓冲，`MvpView` 采样显示 | 主线 [03 协议](03-编码协议.md)、[04 恢复](04-快照与恢复.md)、[05 接入](05-Mirror接入.md)、[09 显示](09-客户端显示.md) |
| 同步自定义结构或紧凑运动字段 | 自动序列化、自定义读写、`OnSerialize` | 简单消息先用自动生成；需要自定布局时按同一顺序写读 | [第 11 节序列化](#11-数据怎样变成字节) |
| 限制普通方法在哪一侧运行 | `[Server]`、`[Client]` 等 | 为当前进程内的方法调用加入执行侧检查 | [第 2 节身份与执行侧](#2-先判断当前是谁在哪里执行) |

“持续状态”和“一次事件”先分清。晚加入者需要看到当前准备状态，所以用状态同步；点击确认音只需播放一次，所以用事件。运动既有持续状态又有时间关系，通常需要快照缓冲和插值。

## 2. 先判断当前是谁、在哪里执行

这些成员放在 `NetworkBehaviour` 中读取；静态的 `NetworkClient`、`NetworkServer` 可从普通脚本访问。

| API | 表示什么 | 什么时候用 |
| --- | --- | --- |
| `isServer` | 这个对象已进入服务器生命周期 | 对象的服务器逻辑、碰撞和结算 |
| `isClient` | 这个对象已进入客户端生命周期 | 对象的客户端表现 |
| `isLocalPlayer` | 这个对象是本机的玩家对象 | 玩家输入、本地玩家相机 |
| `isOwned` | 当前客户端拥有这个对象 | 玩家以及归自己所有的宠物、可控实体输入 |
| `NetworkServer.active` | 当前进程的服务器正在运行 | 管理器、全局服务器系统 |
| `NetworkClient.active` | 当前进程的客户端已启动，包含连接过程 | 客户端会话管理 |
| `NetworkClient.isConnected` | 客户端已有连接 | 连接状态显示 |
| `NetworkClient.ready` | 客户端已向服务器声明可以接收世界对象 | 世界内输入与消息发送的准备检查 |
| `NetworkClient.localPlayer` | 本机玩家的 `NetworkIdentity` | 普通 UI 寻找自己的网络玩家 |
| `connectionToClient` | 服务器上这个对象的拥有者连接 | 给拥有者定向回复，判断对象归属 |
| `netId` | 对象在本次网络生命周期中的 Mirror 编号 | 查找已生成的网络对象 |

Host 的服务器和客户端在同一进程，`isServer` 与 `isClient` 可以同时为真。一个玩家的 `isLocalPlayer` 也可能为真。编写分支时分别考虑这些职责。

普通 UI 找自己：

```csharp
NetworkIdentity player = NetworkClient.localPlayer;
if (player != null &&
    player.TryGetComponent<CharacterIdSync>(out var character))
{
    character.SetLocalCharacter(selectedCharacterId);
}
```

上面的 `selectedCharacterId` 来自按钮选项，`CharacterIdSync` 位于 `Core.Scripts.Networking.Sync` 命名空间。`SetLocalCharacter` 是项目中的普通方法；真正的网络上行发生在它调用 Command 的地方。完整调用见 [角色选择链](专项设计/现有角色与本地表现接入.md)。

`[Server]` 为普通方法加入 `NetworkServer.active` 检查，`[Client]` 检查客户端活动状态；`[ServerCallback]`、`[ClientCallback]` 提供静默返回的对应形式。**这些特性保护当前进程内的方法调用。跨网络发送由 Command、RPC 或消息完成。**

例如服务器自己的回合流程与 Command 都可以调用同一个 `[Server] ServerSetReady` 方法。调用链确定什么时候调用，特性保护执行侧，方法内部检查业务规则。

## 3. 启动连接与安排生命周期

### 启停管理器

| 目的 | 调用 |
| --- | --- |
| 运行专用服务器 | `manager.StartServer()` |
| 连接服务器 | 设置 `manager.networkAddress`，再 `manager.StartClient()` |
| 按 URI 连接 | `manager.StartClient(uri)` |
| 同一进程运行服务器和本地客户端 | `manager.StartHost()` |
| 停止对应身份 | `StopServer()`、`StopClient()`、`StopHost()` |

`manager` 是场景中的 `NetworkManager` 实例。当前项目由 `AutoStartClient` 发起连接；教程由 `MvpView` 的按钮启动 `MvpNetwork`。正式 KCP 配置见 [工程配置](../../../Y_MultipleAgentWorkflow/Project/Mirror_KCP_Config.md)，教学场景按 [第 10 章](10-场景与构建.md) 使用 UDP `7787`。

### 管理器回调

| 重写方法 | 适合放什么 |
| --- | --- |
| `OnStartServer()` | 建立服务器世界，注册客户端到服务器的消息 |
| `OnStopServer()` | 停止世界，解绑自己注册的服务器消息 |
| `OnStartClient()` | 注册服务器到客户端的消息，建立本次客户端接收上下文 |
| `OnStopClient()` | 清理副本与界面绑定，解绑自己注册的客户端消息 |
| `OnClientConnect()` | 处理客户端连接成功 |
| `OnClientSceneChanged()` | 处理客户端完成网络场景切换 |
| `OnServerAddPlayer(NetworkConnectionToClient conn)` | 创建玩家、设置业务身份、注册到连接 |
| `OnServerDisconnect(NetworkConnectionToClient conn)` | 清理业务世界中的玩家，再清理 Mirror 玩家对象 |

本工程 `OnClientConnect` 与 `OnClientSceneChanged` 都是无参数重写。`OnStartClient` 表示客户端启动；连接成功、ready、场景就绪各有自己的时机。

默认 `OnClientConnect` 和 `OnClientSceneChanged` 按场景与 `autoCreatePlayer` 配置处理 Ready、AddPlayer。保留默认行为时调用 `base`。`OnServerAddPlayer` 的默认实现会实例化并注册玩家；主线自己完整接管玩家创建，使用 `NetworkServer.AddPlayerForConnection`，因此该回调按完整代码处理。`OnServerDisconnect` 先清理自己的世界状态，再调用 `base` 完成 Mirror 对象清理。

### 网络对象回调

| 重写方法 | 适合放什么 |
| --- | --- |
| `OnStartServer()` | 初始化服务器对象状态 |
| `OnStartClient()` | 读取初始同步值、建立客户端表现、订阅变化 |
| `OnStartLocalPlayer()` | 绑定本地玩家输入、上报本机角色选择 |
| `OnStartAuthority()`、`OnStopAuthority()` | 开始和结束当前客户端对对象的拥有关系 |
| `OnStopClient()` | 解绑客户端回调、移除本地索引与表现 |
| `OnStopServer()` | 移除服务器索引、释放服务器对象资源 |

对象的初始同步数据在 `OnStartClient` 前应用。表现初始化先读当前值，再依靠后续变化回调。Host 分别执行两侧职责，初始化与清理方法适合设计为重复调用仍保持一致。详细排查见 [生命周期与开发验证](专项设计/生命周期与开发验证.md)。

## 4. 客户端请求服务器：Command

**什么时候用：** 操作属于一个已生成的网络对象，例如玩家切换角色、准备或请求投掷。Command 方法写在 `NetworkBehaviour` 中，参数表达请求；方法体在服务器执行。

默认 `[Command]` 要求客户端拥有该对象。玩家 Prefab 是常用的请求入口：

```csharp
using Mirror;

public sealed class ReadyRequestExample : NetworkBehaviour
{
    [SyncVar] private bool ready;
    public bool Ready => ready;

    public void RequestReady(bool desired)
    {
        if (isLocalPlayer && NetworkClient.ready)
            CmdSetReady(desired);
    }

    [Command]
    private void CmdSetReady(bool desired)
    {
        // 房间阶段、参赛资格等规则在这里判断。
        ready = desired;
    }
}
```

组件放在玩家网络根上，采用 `ServerToClient`。本地按钮调用 `RequestReady`，服务器写字段，客户端读取服务器确认的 `Ready`。完整 UI 绑定练习见 [状态与界面绑定](专项设计/状态与界面绑定.md)。

公共对象也可以开放请求。下面成员放在采用 `ServerToClient` 的机关 `NetworkBehaviour` 中：

```csharp
[SyncVar] private bool opened;

[Command(requiresAuthority = false)]
private void CmdRequestOpen(NetworkConnectionToClient sender = null)
{
    if (opened || sender == null || !sender.isReady || sender.identity == null)
        return;

    if (UnityEngine.Vector3.Distance(
        sender.identity.transform.position, transform.position) > 3f)
        return;

    opened = true;
}
```

客户端在自己的机关副本上调用该 Command。Mirror 在服务器接收时注入真实 `sender`，服务器从 `sender.identity` 找请求者。示例以三米距离和开启状态为规则；实际功能还根据玩家存活、房间、冷却、资源和请求频率判断资格。把请求放在玩家 Command 上、传入目标 `netId`，也能完成相同交互。

`requiresAuthority = false` 放开对象归属检查；业务资格由服务器检查。客户端消息中的玩家编号、位置、命中与伤害都应按请求数据处理。

`[Command(channel = Channels.Unreliable)]` 可用于后续采样持续覆盖的瞄准方向。消耗资源、切换准备、确认投掷等动作先采用默认可靠通道，再用序号和服务器规则控制重复。MVP 输入采用可靠 `MvpInput` 消息，运动快照采用非可靠 `MvpMotion` 消息。

当前例子：[CharacterIdSync.cs](../../../Assets/Core/Scripts/Networking/Sync/CharacterIdSync.cs)。生成与接收过程：[代码生成与序列化](专项设计/代码生成与序列化.md)。

## 5. 当前状态下行：SyncVar 与 hook

**什么时候用：** 某个对象上的当前值需要同步，并且晚进入观察范围的客户端也需要得到它。角色编号、准备状态和一个对象的生命值是常见例子。

下面是采用 `ServerToClient` 的状态组件：

```csharp
using System;
using Mirror;

public sealed class HealthStateExample : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnHealthChanged))]
    private int health = 100;

    public int Health => health;
    public event Action<int> HealthChanged;

    [Server]
    public void ServerDamage(int damage)
    {
        if (damage > 0)
            health = Math.Max(0, health - damage);
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        HealthChanged?.Invoke(health);
    }

    private void OnHealthChanged(int oldValue, int newValue)
    {
        HealthChanged?.Invoke(newValue);
    }
}
```

接线顺序：

1. 服务器在自己的结算流程调用 `ServerDamage`。
2. 字段变化由 Mirror 发送，接收范围取决于组件同步模式和对象观察者。
3. hook 参数依次是旧值、新值，用新值更新本地表现。
4. UI 绑定时先读 `Health`，再订阅 `HealthChanged`；解绑时解除订阅。这也覆盖 UI 晚于网络对象创建的情况。

同一发送间隔内的多次赋值可能合并成最终值。重复写入相同值通常没有变化回调。因此每次受击的声音用事件，当前剩余生命用状态。多个字段构成一份不可拆的业务值时，可以使用一个结构体并整体赋值。

SyncVar 的方向由组件 `syncDirection` 决定，范围还受 `syncMode` 与观察者控制。本节示例明确使用服务器到客户端。当前 `CharacterIdSync` 也是这条方向；MVP 的生命与分数由 `WorldState` 和快照承载，`MvpBody` 上的 SyncVar 只承担身份。

## 6. 名单与背包：同步集合

**什么时候用：** 对象维护一组当前元素，并需要同步增删改。列表用 `SyncList<T>`，键值映射用 `SyncDictionary<TKey, TValue>`，去重集合用 `SyncHashSet<T>`，有序集合用 `SyncSortedSet<T>`。

成员放在 `NetworkBehaviour` 中，并保留同一个集合实例：

```csharp
public struct Item
{
    public int Id;
    public int Count;
}

public readonly SyncList<Item> Items = new SyncList<Item>();
public readonly SyncDictionary<int, int> Ammo = new SyncDictionary<int, int>();

[Server]
public void ServerSetCount(int index, int count)
{
    if (index < 0 || index >= Items.Count || count < 0)
        return;

    Item item = Items[index];
    item.Count = count;
    Items[index] = item;
}
```

这段练习采用 `ServerToClient`，由服务器调用 `Add`、`Remove`、索引赋值等操作。结构体元素取出修改后写回索引，集合才能记录替换。集合的可写侧同样服从组件同步方向与归属配置。

本工程 `SyncList<T>` 的常用回调签名如下：

| 回调 | 参数 | 用途 |
| --- | --- | --- |
| `OnAdd` | `int index` | 新增元素 |
| `OnInsert` | `int index` | 插入元素 |
| `OnSet` | `int index, T oldValue` | 替换元素 |
| `OnRemove` | `int index, T oldValue` | 删除元素 |
| `OnClear` | 无参数 | 清空；该回调发生在实际清空之前 |
| `OnChange` | `Operation operation, int index, T value` | 汇总操作通知 |

在 `OnStartClient` 订阅，在 `OnStopClient` 解绑。初始全量内容直接建立集合，客户端按当前内容刷新一次。变化回调需要整表重读时，先标记刷新，在 `LateUpdate` 中读取已完成操作的集合。具名处理器方便按相同委托解绑。

完整房间名单、字典和 UI 接线见 [集合公共对象与消息](专项设计/集合公共对象与消息.md)。源码：[SyncList.cs](../../../Assets/Plugins/Mirror/Core/SyncList.cs)。

## 7. 一次通知：ClientRpc 与 TargetRpc

**什么时候用：** 服务器已经作出决定，需要通知客户端发生了一次什么。持久结果放入同步状态，通知参数带齐这次表现需要的信息。

下面成员放在已 Spawn 的 `NetworkBehaviour` 中：

```csharp
public event System.Action<UnityEngine.Vector3> EffectReceived;
public event System.Action<int> RequestRejected;

[ClientRpc]
private void RpcShowEffect(UnityEngine.Vector3 position)
{
    EffectReceived?.Invoke(position);
}

[TargetRpc]
private void TargetReject(NetworkConnectionToClient target, int reason)
{
    RequestRejected?.Invoke(reason);
}
```

服务器调用 `RpcShowEffect(position)`，客户端表现层订阅事件播放声音或特效。服务器调用 `TargetReject(requestingConnection, reason)`，只有目标连接执行拒绝提示。

| 形式 | 接收范围 | 选用条件 |
| --- | --- | --- |
| `[ClientRpc]` | 该对象当前就绪的观察客户端，默认包含拥有者 | 观察范围内共同播放一次表现 |
| `[ClientRpc(includeOwner = false)]` | 上述范围中排除拥有者 | 拥有者已播放本地反馈，其他人接收确认表现 |
| `[TargetRpc]` 且首参为 `NetworkConnectionToClient` | 该连接 | 请求结果、私有提示 |
| `[TargetRpc]` 省略连接参数 | 对象拥有者连接 | 固定回复这个对象的拥有者 |

TargetRpc 的首个连接参数负责路由，业务参数才传入接收方法体。显式连接为 `null` 时会回退到对象拥有者。目标客户端也需要存在承载 RPC 的网络对象副本。

ClientRpc 的对象观察范围可以小于全部连接。晚加入者通过当前状态恢复世界；需要重放的事件由业务保存与恢复。跨对象或跨通道依赖的状态使用请求号、事件号或 Tick 对齐，RPC 参数直接包含本次表现数据。

当前项目的音频调用可从 [NetworkAudioPlayer.cs](../../../Assets/Core/Scripts/Networking/Sync/NetworkAudioPlayer.cs) 追踪。MVP 爆炸使用可靠 `MvpExplosion` 消息，带 `Round / Event / Tick`，由 `MvpView` 去重排队并按播放时间显示，见 [09 客户端显示](09-客户端显示.md)。

## 8. 连接级请求与批量数据：NetworkMessage

**什么时候用：** 消息直接属于连接或世界，例如房间状态查询、输入帧、完整基线、批量运动与恢复请求。处理器可以从连接判断请求者，业务数据保持自己的编号和协议。

消息使用实现 `NetworkMessage` 的结构体，公开字段由 Weaver 生成读写。下面是独立的查询管理器练习：

```csharp
using Mirror;

public struct PlayerCountRequest : NetworkMessage { }
public struct PlayerCountReply : NetworkMessage
{
    public int Count;
}

public sealed class CountQueryExample : NetworkManager
{
    public int LastCount { get; private set; }

    public override void OnStartServer()
    {
        base.OnStartServer();
        NetworkServer.RegisterHandler<PlayerCountRequest>(OnCountRequest);
    }

    private void OnCountRequest(
        NetworkConnectionToClient conn, PlayerCountRequest message)
    {
        if (!conn.isReady || conn.identity == null)
            return;

        conn.Send(new PlayerCountReply { Count = numPlayers });
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        NetworkClient.RegisterHandler<PlayerCountReply>(OnCountReply);
    }

    private void OnCountReply(PlayerCountReply message)
    {
        LastCount = message.Count;
    }

    public void RequestCount()
    {
        if (NetworkClient.ready)
            NetworkClient.Send(new PlayerCountRequest());
    }

    public override void OnStopClient()
    {
        NetworkClient.UnregisterHandler<PlayerCountReply>();
        base.OnStopClient();
    }

    public override void OnStopServer()
    {
        NetworkServer.UnregisterHandler<PlayerCountRequest>();
        base.OnStopServer();
    }
}
```

该例采用默认自动创建玩家流程。每个场景使用一个网络管理器，已有 `MvpNetwork` 时，将对应字段与注册逻辑接入它的回调。项目请求还按房间权限与频率进一步过滤。

| API | 怎么用 |
| --- | --- |
| `NetworkClient.Send(message, channelId)` | 客户端发给服务器 |
| `conn.Send(message, channelId)` | 服务器发给一条客户端连接 |
| `NetworkServer.SendToAll(message, channelId, sendToReadyOnly)` | 按参数发给全部连接或仅 ready 连接；`sendToReadyOnly` 默认是 `false` |
| `NetworkServer.SendToReady(message, channelId)` | 服务器发给 ready 连接 |
| `RegisterHandler<T>(handler)` | 默认要求连接处于 Mirror 已认证状态 |
| `UnregisterHandler<T>()` | 停止时只解除自己注册的消息类型 |

认证、ready、拥有玩家、房间资格分别检查。需要同类型消息的不同业务处理时，由一个处理器再按消息内容路由。

| 通道 | 特点 | 本教程用途 |
| --- | --- | --- |
| `Channels.Reliable`，值为 0 | 可靠、有序，丢包重传可产生等待 | 输入、完整基线、恢复请求、爆炸事件 |
| `Channels.Unreliable`，值为 1 | 可丢失、可乱序 | 运动包；业务用 Tick、基线编号处理到达顺序 |

选择非可靠通道时，同时设计丢失、重复、乱序、序号和恢复。可靠与非可靠通道之间的数据关系通过业务编号和时间协调。

完整实现：[05 Mirror 接入](05-Mirror接入.md)。房间查询练习：[集合公共对象与消息](专项设计/集合公共对象与消息.md)。

## 9. 网络实体生命周期与归属

**什么时候用：** 一个实体需要独立身份、生成、销毁或归属，例如玩家、炸弹与可拾取道具。纯本地的模型、UI、特效由普通 Unity 创建即可。

网络生成按这个顺序接线：

1. Prefab 根添加 `NetworkIdentity`，再添加需要同步的 `NetworkBehaviour`。
2. 客户端通过管理器的 Spawnable Prefabs 或 `NetworkClient.RegisterPrefab` 注册相应资源。玩家模板使用管理器的 Player Prefab。
3. 服务器 `Instantiate`，填好业务身份与初始状态，再调用 `NetworkServer.Spawn`。
4. 结束时由服务器 `NetworkServer.Destroy`；客户端由网络生命周期清理。

下面成员放在服务器使用的 `NetworkBehaviour` 中，`itemPrefab` 引用已注册的网络 Prefab：

```csharp
public UnityEngine.GameObject itemPrefab;

[Server]
public void ServerCreateItem(UnityEngine.Vector3 position)
{
    UnityEngine.GameObject item = UnityEngine.Object.Instantiate(
        itemPrefab, position, UnityEngine.Quaternion.identity);
    NetworkServer.Spawn(item);
}

[Server]
public void ServerRemoveItem(UnityEngine.GameObject item)
{
    if (item != null)
        NetworkServer.Destroy(item);
}
```

| API | 什么时候用 | 需要配合什么 |
| --- | --- | --- |
| `NetworkServer.Spawn(obj)` | 服务器拥有的公共实体 | 初始状态、客户端 Prefab 注册 |
| `NetworkServer.Spawn(obj, ownerConnection)` | 生成时交给某客户端拥有 | 请求权限、断线清理规则 |
| `identity.AssignClientAuthority(conn)` | 已生成的非玩家对象改变客户端归属 | 检查现有拥有者；返回 `bool` 表示是否成功 |
| `identity.RemoveClientAuthority()` | 回收非玩家对象归属 | 对象停止本地控制 |
| `NetworkServer.AddPlayerForConnection(conn, player)` | 为连接建立玩家 | 管理器玩家创建回调 |
| `NetworkServer.ReplacePlayerForConnection(...)` | 替换连接的玩家对象 | 按所用重载处理旧玩家对象生命周期 |
| `NetworkServer.UnSpawn(obj)` | 网络退场并进入对象池 | 两端生成与回收处理器、状态复位 |
| `NetworkServer.Destroy(obj)` | 服务器结束网络实体生命周期 | 客户端清理回调 |

玩家对象的归属通过玩家管理 API 处理。拥有者断线时，Mirror 会清理该连接拥有的对象；需要跨连接保留的公共道具，通常由服务器拥有，用业务字段记录持有人。

归属决定哪些客户端可以发默认 Command 等操作，`syncDirection` 决定组件数据方向。分配归属后，继续明确谁计算运动、谁写状态。

客户端查网络编号用 `NetworkClient.spawned.TryGetValue(netId, out identity)`，服务器用 `NetworkServer.spawned`。查找失败时按对象到达、销毁和观察范围处理；角色挂点需要模型装配完毕后再解析。详见 [当前角色接入](专项设计/现有角色与本地表现接入.md)。

## 10. 连续运动、时间与发送频率

**什么时候用：** 对象的位置、旋转或运动参数持续变化。先明确计算结果的一侧，再决定接收方怎样播放。

| 运动情形 | 方案 | 怎样接线 |
| --- | --- | --- |
| 当前项目的玩家移动 | `NetworkTransformReliable`，`ClientToServer` | 拥有者 `CharacterMotor` 移动根，服务器接收并转发，远端缓冲插值 |
| 当前项目的动画运动表现 | `CharacterMotionSync`，`ClientToServer` | 拥有者写运动字段，自定义序列化，远端应用最近收到的表现状态 |
| Mirror 组件练习中的公共刚体 | 服务器模拟，变换或刚体同步组件下行 | 服务器写权威运动，客户端按组件规则播放 |
| 可解析的短时弹道 | 起点、初速、重力与起始时间 | 双端用同一轨迹函数；碰撞与结算由服务器裁定 |
| 主线角色、炸弹和碎片 | 自写批量快照 | 服务器模拟，`Wire` 编码，`Replica` 排序与采样，`MvpView` 显示 |

同一根的运动写入入口保持明确。动画参数同步与位置快照分别承担表现和运动职责。当前 `CharacterMotionSync` 传三项最近状态，没有时间戳与插值缓冲；当前位置插值来自 `NetworkTransformReliable`。

网络时间使用本地实现的语义：

| API | 表示什么 | 适合什么 |
| --- | --- | --- |
| `NetworkTime.localTime` | 本地不受时间缩放的时间 | 等待、超时、本地发送节流 |
| `NetworkTime.time` | 网络时间；客户端位于插值播放时间线上，受时间缩放影响 | 与网络播放时间一致的表现 |
| `NetworkTime.unscaledTime` | 不受缩放的网络时间；客户端同样使用插值时间线 | 按网络播放时间推进的非缩放表现 |
| `NetworkTime.predictedTime` | 供预测使用的网络时间估计 | 本地预测时间选择 |
| `NetworkTime.rtt` | 平滑后的往返延迟 | 延迟显示与网络测量 |

`NetworkTime.time` 在客户端的含义包含播放缓冲。轨迹接管与预测要先明确使用哪条时间线。主线使用服务器 Tick 和自己的 `PlaybackClock`，保持结构、运动和爆炸在同一播放时刻生效。

`SetDirty()` 标记自定义状态需要发送，组件发送间隔与管理器发送调度控制实际发送时机。发送频率、玩法固定 Tick、渲染帧率分别配置。`AccurateInterval` 负责间隔触发，玩法 Tick 由模拟系统维护。

轨迹代码与组件选择见 [运动轨迹与同步选择](专项设计/运动轨迹与同步选择.md)，视觉预测接管见 [投掷物预测与权威接管](专项设计/投掷物预测与权威接管.md)。

## 11. 数据怎样变成字节

**什么时候用：** 定义消息参数、SyncVar 类型，或为紧凑状态自定编码。简单结构先使用 Weaver 为公开字段生成的序列化，属性通过明确字段承载数据。

需要为特定类型自定义读写时，使用静态扩展方法。下面例子是一种整数字段协议：

```csharp
using Mirror;

public struct ItemKey
{
    public int Id;
}

public static class ItemKeySerialization
{
    public static void WriteItemKey(this NetworkWriter writer, ItemKey value)
    {
        writer.WriteInt(value.Id);
    }

    public static ItemKey ReadItemKey(this NetworkReader reader)
    {
        return new ItemKey { Id = reader.ReadInt() };
    }
}
```

Weaver 根据类型、静态扩展方法签名匹配读写。双端按相同字段顺序、类型和长度规则编码解码。编译成功后同时检查 Console 的 Weaver 诊断。

需要控制某个组件自己的同步数据时，重写：

```csharp
public override void OnSerialize(NetworkWriter writer, bool initialState)
{
    base.OnSerialize(writer, initialState);
    writer.WriteByte(motionState);
}

public override void OnDeserialize(NetworkReader reader, bool initialState)
{
    base.OnDeserialize(reader, initialState);
    motionState = reader.ReadByte();
}
```

该成员摘录中的 `motionState` 是组件的 `byte` 字段。本工程两个方法的返回类型均为 `void`。需要同步时调用 `SetDirty()`；实际写读布局、同步方向与接收方验证一起设计。

`initialState` 表示这个网络对象的初始序列化。完整世界恢复还需要基线、回合、对象集合、拓扑和后续事件，主线在 `Wire / Replica` 层实现这些规则。

项目例子：[CharacterMotionSync.cs](../../../Assets/Core/Scripts/Networking/Sync/CharacterMotionSync.cs)。Weaver、生成调用和序列化深入查询：[代码生成与序列化](专项设计/代码生成与序列化.md)。

## 12. 把一个功能接完整

以“玩家按键投掷”为例，按五步确定 API：

1. **本地入口。** 输入组件读取按键，普通方法组织方向和请求序号；需要即时反馈时创建本地视觉球。
2. **请求上行。** 对象练习使用玩家 Command；MVP 使用可靠 `MvpInput`，由 `conn.identity` 确定玩家。
3. **服务器决定。** 检查回合、序号、方向、冷却、玩家状态与资源，计算发射位置，生成网络物体。
4. **持续结果。** 组件练习同步发射状态和运动；MVP 把炸弹写入世界基线与运动快照。
5. **一次表现与退出。** 服务器结算一次爆炸，通知携带事件身份与时间；客户端去重播放，并清理视觉记录与订阅。

主线实现见 [08 爆炸与回合](08-爆炸与回合.md) 与 [09 客户端显示](09-客户端显示.md)。需要按键即时反馈时查询 [投掷物预测与权威接管](专项设计/投掷物预测与权威接管.md)；需要刚体弹跳、命中与击退组件时查询 [爆炸物与命中裁决](专项设计/爆炸物与命中裁决.md)。

源码核对入口：[NetworkManager.cs](../../../Assets/Plugins/Mirror/Core/NetworkManager.cs)、[NetworkBehaviour.cs](../../../Assets/Plugins/Mirror/Core/NetworkBehaviour.cs)、[NetworkIdentity.cs](../../../Assets/Plugins/Mirror/Core/NetworkIdentity.cs)、[NetworkClient.cs](../../../Assets/Plugins/Mirror/Core/NetworkClient.cs)、[NetworkServer.cs](../../../Assets/Plugins/Mirror/Core/NetworkServer.cs)、[NetworkTime.cs](../../../Assets/Plugins/Mirror/Core/NetworkTime.cs)、[Weaver](../../../Assets/Plugins/Mirror/Editor/Weaver)。

[返回教程入口](README.md) · [专项查询](专项设计/README.md)
