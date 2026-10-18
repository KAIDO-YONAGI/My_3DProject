# Mirror 网络 API 总览

本页按需求选择 API，并把 API 放回当前 Unity 工程的调用链中。具体行为以工程内 Mirror 96.11.2 源码为准。

## 按需求选择

| 需求 | API | 实现边界 |
| --- | --- | --- |
| 本地按钮、按键调用本机逻辑 | 普通 C# 方法、Unity 事件 | 先完成本地输入和表现，只有跨端的请求才进入 Mirror |
| 玩家请求改变服务器状态 | `[Command]` | 放在拥有者的玩家 `NetworkBehaviour` 上；服务器重新检查距离、资格、目标和当前状态 |
| 同步晚加入者也需要知道的字段 | `[SyncVar]` | 服务器修改当前状态；客户端收到后更新视图 |
| 同步平台格子集合 | `SyncList<int>` | 服务器执行增删改；客户端按集合变化重建或局部刷新显示 |
| 同步平台连续位姿 | `NetworkTransformReliable` | 明确模拟端与 `SyncDirection`；接收端只呈现，不另跑同一份权威物理 |
| 新回合把玩家放回出生点 | `NetworkTransformReliable.ServerTeleport` | 服务器传入目标位置与旋转，通知客户端并清除旧位置插值 |
| 广播一次音效或特效 | `[ClientRpc]` | 从已生成网络对象调用，只通知当前观察该对象的客户端 |
| 只回复一个玩家 | `[TargetRpc]` | 服务器指定目标连接，适用于个人提示或请求结果 |
| 创建或销毁网络实体 | `NetworkServer.Spawn`、`NetworkServer.Destroy` | 服务器创建已注册 Prefab，设置初始业务状态后 Spawn |
| 自定义连接级消息或批量协议 | `NetworkMessage` | 仅在常规对象同步无法满足且已有明确数据需求时采用；MVP 不自写通信协议 |
| 批量对象共享基线、Tick 和恢复策略 | 独立快照同步内核 | 作为单独的进阶专题；先测量现有同步成本，再决定是否需要 |

先区分当前状态和一次事件。平台是否锚定、属于哪一层，是加入后仍需知道的状态；爆炸音效是发生时播放的一次事件。平台格子拓扑使用集合状态，平台移动使用变换同步，这几类数据不能互相替代。

## 当前工程的真实调用链

角色选择展示了普通方法、Command 和 SyncVar 的分工：

1. UI 调用 `CharacterIdSync.SetLocalCharacter`，这一步是本地 C# 调用。
2. 方法确认本机玩家并检查角色编号，再调用 `CommandSetCharacter`。
3. Mirror 将 Command 发送到服务器；服务器再次验证编号和对象归属。
4. 服务器写入角色编号，SyncVar 将当前值同步给观察者。
5. 客户端通过 SyncVar hook 更新角色模型。

入口位于 [`CharacterIdSync.cs`](../../../Assets/Core/Scripts/Networking/Sync/CharacterIdSync.cs)。移动表现另由 [`CharacterMotionSync.cs`](../../../Assets/Core/Scripts/Networking/Sync/CharacterMotionSync.cs) 同步动画参数，玩家位姿由现有 `NetworkTransformReliable` 组件按 `ClientToServer` 方向同步。它们属于现有工程行为，不代表服务器计算玩家移动。

音频也有现成范例：[`NetworkAudioPlayer.cs`](../../../Assets/Core/Scripts/Networking/Sync/NetworkAudioPlayer.cs) 在本机播放一次，再通过 `ClientRpc(includeOwner = false)` 通知其他观察者，避免拥有者重复播放。

## 执行侧与对象归属

| API | 含义与用途 |
| --- | --- |
| `isServer`、`isClient` | 当前对象分别进入服务器或客户端生命周期；Host 中两者可以同时为真 |
| `isLocalPlayer`、`isOwned` | 判断本机玩家对象或本机拥有对象，输入入口通常先做此判断 |
| `NetworkClient.localPlayer` | 从普通 UI 定位本机玩家的 `NetworkIdentity` |
| `connectionToClient` | 服务器端对象的拥有者连接，可用于定向回复或归属校验 |
| `NetworkServer.active`、`NetworkClient.active` | 检查对应网络侧是否运行；它们不代替业务权限验证 |
| `netId` | 已生成网络对象在当前生命周期内的 Mirror 标识 |
| `[Server]`、`[Client]` | 限制方法在哪一侧执行；它们本身不发送网络数据 |
| `[ServerCallback]` | 让 `Update` 等 Unity 回调只在服务器执行，其他端直接返回 |

玩家输入和相机只处理本机玩家。服务器的破坏、平台物理、道具和胜负判定以服务器状态为准。教程保留当前玩家移动所有权，不把这条链描述成服务器权威移动。

## 请求服务器执行 `[Command]`

Command 表达客户端意图，不是客户端裁决结果。Mirror 96.11.2 的 `[Command]` 默认要求调用者拥有该网络对象，并使用 Reliable 通道。把入口放在当前玩家对象上，可以直接利用玩家对象的所有权：

第 05 章的 `ArenaPlayerActions.Update` 在本机读 E 和相机朝向，调用 `CmdUse(aimDirection)`。Mirror 把这次调用交给服务器上的同一个玩家对象。服务器检查存活状态、频率和方向数值，再进入第 08 章的 `ArenaMatch.ServerTryUse`；服务器自己发射射线、检查库存和命中平台，最后调用 `PlatformChunk.ServerBreak`。

沿教程查实现时，依次看 `Update → CmdUse → ServerTryUse → ServerApplyFirstHit → ServerBreak`。客户端传的是操作方向，命中对象与破坏结果由服务器计算。把 Command 放在玩家根上，可以利用现有所有权判断谁有权提出这次操作。

## 持续状态 `[SyncVar]`

SyncVar 适合数量有限、晚加入者需要当前值的状态。平台组件 `PlatformChunk` 使用 `Anchored`、`IsOpening`、`Layer`、`RoundId` 描述平台当前状态；字段由服务器修改，客户端 hook 或状态刷新代码更新表现。

SyncVar 不是事件队列。快速连续变化时，客户端关心的是同步后的当前值，不保证逐次播放每一次中间变化。若某次变化需要精确播放一次音效或特效，使用 RPC 事件；若它改变平台当前拓扑，则同步拓扑本身。

## 集合同步 `SyncList<T>`

教程使用 `SyncList<int> Cells` 存储平台格子编号。Mirror 96.11.2 的变化回调签名为：

```csharp
private void OnCellsChanged(
    SyncList<int>.Operation operation,
    int index,
    int item)
{
    // 按操作刷新平台视图，或重建轻量网格。
}
```

订阅 `Cells.OnChange` 时，委托类型是 `Action<Operation, int, T>`，这里的 `T` 为 `int`。操作枚举是 `OP_ADD`、`OP_SET`、`OP_INSERT`、`OP_REMOVEAT`、`OP_CLEAR`。`item` 对 Add、Insert 表示新值，对 Set、RemoveAt 表示旧值；Clear 不携带有意义的格子值。第 03 章使用这个三参数回调标记形状需要重建。

初始化和晚加入显示应能从当前 `Cells` 全量建立视图；回调适合处理后续变化。集合变化只同步格子数据，不会自动更新 Mesh、碰撞体或平台位姿。

## 一次通知与定向回复

`[ClientRpc]` 从服务器上的 `NetworkBehaviour` 调用，发送给当前观察该对象的客户端。Mirror 默认包含调用该 RPC 的 Host 本机客户端；需要排除拥有者时写明 `includeOwner = false`。音频范例采用“本机先播放一次，RPC 只通知其他端”的模式。

`[TargetRpc]` 适合给单个连接返回结果。它仍从服务器调用，目标连接作为参数传入。第 07 章的新局复位是具体例子：

在 `ArenaPlayerActions.ServerReset` 方法体中，传送完成后调用：

```csharp
TargetResetMotion(connectionToClient);
```

在同一个 `ArenaPlayerActions` 类内部，加入这个方法声明：

```csharp
[TargetRpc]
private void TargetResetMotion(NetworkConnectionToClient target)
{
    // 只有拥有者运行运动器，因此只清理这个客户端的旧速度。
    GetComponent<PlayerCharacterController>()?.ResetMotion();
}
```

完整接入位置见第 07 章。RPC 负责这一次定向操作；存活状态和回合编号仍由 SyncVar 保存。

## 网络对象 Spawn 与销毁

服务器通过 `NetworkServer.Spawn(gameObject)` 生成已登记 Prefab 的网络对象，可用 `Spawn(gameObject, ownerConnection)` 指定拥有者。离场使用 `NetworkServer.Destroy(gameObject)`，不要只调用 Unity `Destroy`，否则其他客户端不会收到对应的网络对象生命周期变化。

教程把 `PlatformChunk` Prefab 放在 `Assets/Core/Prefabs/Gameplay/PlatformChunk.prefab`，由现有玩法场景中的 `ArenaSceneBootstrap` 在服务器侧创建并 Spawn。初始 SyncVar、SyncList 和变换应在 Spawn 前准备好，使晚加入客户端建立一致的初始视图。

## 连续位姿同步

当前玩家保留 `NetworkTransformReliable.ClientToServer`：玩家所有者侧的 `CharacterMotor` 驱动移动，服务器接收位姿并向其他观察者转发。平台由服务器计算破坏和动态物理，平台变换组件设为 `ServerToClient`；普通客户端的刚体保持 Kinematic，以网络位姿呈现，不在客户端重复模拟平台。

NetworkTransform 同步位姿，不等于自动同步完整刚体属性、碰撞历史或玩法状态。平台拓扑仍由 `Cells` 等业务状态描述。需要自定义速度、基线或批量恢复时，再查阅[独立快照同步内核](专项设计/独立快照同步内核.md)。

## 消息与序列化的边界

Mirror 的 Command、RPC、SyncVar、SyncList 和网络对象序列化由 Mirror 与 Weaver 配合完成。MVP 使用这些内建机制；只有在测量确认对象级变换成本不合适，并提出明确的批量数据和恢复需求后，才评估 `NetworkMessage` 承载的独立协议。进阶方案见[独立快照同步内核](专项设计/独立快照同步内核.md)。

更多查询入口见[专项设计索引](专项设计/README.md)。
