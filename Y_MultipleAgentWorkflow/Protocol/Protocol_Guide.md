# 网络协议边界

文档 ID：`PROTOCOL-GUIDE`
状态：`Active`
最后核验：`2026-10-10`（运动表现协议边界局部核验）

## 协议分层

```text
UDP Datagram
  → kcp2k 通道头与连接 Cookie
  → KCP 可靠传输或非可靠直传
  → Mirror Transport 事件
  → Mirror 连接、Ready、AddPlayer、Spawn 和状态消息
  → NetworkTransformReliable 变换数据
  → NetworkCharacterSync 角色编号
```

## 传输层

- 服务器监听 UDP `7777`。
- KCP 可靠通道负责握手、确认、重传、窗口和分片。
- 非可靠通道保留 kcp2k 通道头与 Cookie，数据直接交给上层。
- KCP 客户端和服务器在 EarlyUpdate 接收数据，在 LateUpdate 刷新发送。

## Mirror 消息层

- 连接建立后，Mirror 完成认证并进入 Ready。
- `AddPlayerMessage` 请求服务器创建当前连接的玩家对象。
- Spawn 数据携带 `NetworkIdentity`、初始 Transform 和 NetworkBehaviour 初始状态。
- Entity State 数据携带 NetworkBehaviour 的增量状态。

## 项目同步字段

### 角色变换

`NetworkTransformReliable` 使用 `ClientToServer` 方向。本地拥有者序列化位置和旋转，服务器接收后广播，远程客户端通过快照缓冲插值显示。

### 角色编号

`NetworkCharacterSync.characterId` 是 `SyncVar`。服务器持有最终值，客户端 Hook 收到变化后重新装配角色表现。

本地拥有者调用 `SetLocalCharacter` 时：

```text
SetLocalCharacter
  → CommandSetCharacter
  → 服务器校验角色编号
  → 更新 characterId
  → SyncVar Hook
  → 各客户端重新装配对应表现
```

Host 和远端客户端通过同一个 `CommandSetCharacter` 入口提交角色选择。服务器校验角色编号后写入 `characterId`，表现由 SyncVar hook 驱动。客户端装配状态按实例、编号、来源 Prefab 和本地身份判断复用与刷新。

## 运动表现协议（Proposal）

用户已暂缓本轮运动状态网络实现。当前不存在 `NetworkCharacterMovementSync.cs`，`CharacterMotion.State` 只在本地使用，以下约定是后续接入方案，不能作为当前可用协议：

| 字段 | 计划线格式 | 约束 |
|---|---|---|
| 运动状态 | `byte` | `Idle=0`、`Run=1`、`Sprint=2`、`Rising=3`、`Falling=4` |
| 局部运动方向 | `Vector2`，两个 `float` | 有限数值，向量长度不超过单位圆的容差 |
| 动画权重 | `float` | 有限数值且位于 `[0,1]` |

- 采用可靠 `ClientToServer` 自定义序列化，组件只挂在 `NetworkPlayer` 根节点；不同步 Animator 对象、全部动画参数或物理速度，不改成服务器物理模拟。
- 使用现有发送频率，静止时持续采样。服务器按拥有者 `remoteTimeline` 采样后转发，普通客户端按 `NetworkTime.unscaledTime` 采样，时间偏移与同根 `NetworkTransformReliable` 配置一致。
- 离散状态按快照时间切换，仅同状态方向与权重插值；准确切换阈值、初始快照和停流策略需在实现时确定并测试。
- 服务端校验枚举、有限数值和范围，限制缓冲容量；异常丢弃并保持最后有效状态。这不是服务器权威或反作弊。
- 本地拥有者只运行本地状态机，不应用回传；Host 远程角色使用服务器采样结果，不重复客户端插值。
- 根控制器需要只读当前快照与远程快照接收接口；延迟装配和换角色保留根运动快照，首个快照之前使用 Idle。当前接口和缓冲尚未接入。

验收须覆盖首次加入、延迟装配、换角色、停止移动、异常快照和缓冲上限，以及同版本双客户端、Host、专用服务器与延迟条件下的位置/状态时序。本轮未完成这些同步验收。

## 兼容性规则

客户端与服务器使用相同的玩家 Prefab 组件布局、NetworkBehaviour 顺序、同步字段和序列化配置。网络组件或同步字段发生变化时，同时构建并部署客户端与服务器。

未来追加运动状态网络组件时保持原 NetworkBehaviour 顺序，只在根节点末尾追加；变更后旧客户端和服务器不得混用。当前资产上残留的待接入组件记录不代表协议已发布。

## 维护触发

修改传输参数、Mirror 消息流程、Command、SyncVar、NetworkTransform、序列化字段或兼容规则时更新本文档，并同步 Networking、Server 和 UnityRuntime 文档。
