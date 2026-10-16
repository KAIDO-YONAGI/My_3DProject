# 同步协议

文档 ID：`SYNC-PROTOCOL`
状态：`Active`
最后核验：`2026-10-16`

## 协议分层

```text
UDP Datagram
  → kcp2k 通道头与连接 Cookie
  → KCP 可靠传输或非可靠直传
  → Mirror Transport 事件
  → Mirror 连接、Ready、AddPlayer、Spawn 和状态消息
  → NetworkTransformReliable 变换数据
  → CharacterIdSync 角色编号
  → CharacterMotionSync 运动快照
  → NetworkAudioPlayer 音频事件
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

## 现有同步字段

### 角色变换

`NetworkTransformReliable` 使用 `ClientToServer` 方向。本地拥有者序列化位置和旋转，服务器接收后广播，远程客户端通过快照缓冲插值显示。

### 角色编号

`CharacterIdSync.characterId` 是 `SyncVar`。服务器持有最终值，客户端 Hook 收到变化后重新装配角色表现。

本地玩家启动上报本机默认编号，运行中由本地拥有者调用 `SetLocalCharacter` 提交角色选择，两者共用同一个上行入口：

```text
OnStartLocalPlayer 上报本机默认编号 / SetLocalCharacter 提交角色选择
  → CommandSetCharacter
  → 服务器校验角色编号
  → 更新 characterId
  → SyncVar Hook
  → 各客户端重新装配对应表现
```

Host 和远端客户端通过同一个 `CommandSetCharacter` 入口提交角色选择。服务器在 `OnStartServer` 写入的默认编号是客户端未上报时的兜底值，收到上报后按两套角色数组校验，拒收未通过校验的编号并记录警告。表现由 SyncVar hook 驱动，客户端装配状态按实例、编号、来源 Prefab 和本地身份判断复用与刷新。

### 运动表现

运动状态、局部方向和动画权重经拥有者客户端上行，服务器校验后转发给其他观察者。线格式如下：

| 字段 | 线格式 | 约束 |
|---|---|---|
| 运动状态 | `byte` | `Idle=0`、`Run=1`、`Sprint=2`、`Rising=3`、`Falling=4` |
| 局部运动方向 | `Vector2`，两个 `float` | 有限数值，向量长度在单位圆容差内 |
| 动画权重 | `float` | 有限数值，位于 `[0,1]` |

- 采用可靠 `ClientToServer` 自定义序列化，组件挂在 `NetworkPlayer` 根节点末尾；传输 `CharacterMotionSnapshot` 的三个原始字段，即 `CharacterMotion` 中能够跨客户端复现的那部分，`IsGrounded`、`WantsSprint` 等派生属性与 Animator 对象、全部动画参数、物理速度均不上线。
- 使用现有发送频率，静止时持续采样。服务器按拥有者 `remoteTimeline` 采样后转发，普通客户端按 `NetworkTime.unscaledTime` 采样，时间偏移与同根 `NetworkTransformReliable` 配置一致。
- 离散状态按快照时间切换，同状态内的方向与权重插值。
- 服务端校验枚举、有限数值和范围，限制缓冲容量；异常快照丢弃并保持最后有效状态，丢弃按 1 秒间隔限流输出警告并按失败项区分原因（状态编号超上限多属双端版本不一致）。服务端只做校验和转发，位置保持客户端权威。
- 本地拥有者只运行本地状态机，不应用回传快照；Host 的远程角色使用服务器采样结果。
- 根控制器提供只读当前快照属性和远程快照接收入口。延迟装配和换角色保留根运动快照，首个快照到达前使用 Idle。

验收覆盖首次加入、延迟装配、换角色、停止移动、异常快照和缓冲上限，以及同版本双客户端、Host、专用服务器与延迟条件下的位置和状态时序。

### 音频事件

反馈不作为持续状态同步。`AudioTriggerManager` 在本地触发时生成 `FeedbackMessage`，携带 `AudioType`、声源对象和触发时的 `WorldPosition`。本地先播放一次，再由来源对象对应的本地 `NetworkAudioPlayer` 调用 Command：

```text
本地输入
  → AudioPlayerManager 本地播放
  → FeedbackEventSO
  → 本地拥有者 NetworkAudioPlayer
  → CommandPlayAudio
  → 服务器
  → RpcPlayAudio(includeOwner = false)
  → 其他观察者按 WorldPosition 播放
```

`isLocalPlayer` 用于限制 Command 的发起者，`includeOwner = false` 用于限制 ClientRpc 的接收者，两者职责不同，不能互相替代。事件来源不是本地网络玩家时直接忽略；客户端未 Ready 时不发送 Command。

## 兼容性规则

客户端与服务器使用相同的玩家 Prefab 组件布局、NetworkBehaviour 顺序、同步字段和序列化配置。网络组件或同步字段发生变化时，同时构建并部署客户端与服务器。

`NetworkPlayer.prefab` 的根节点组件顺序为 `Transform`、`NetworkIdentity`、`NetworkTransformReliable`、`CharacterController`、`PlayerCharacterController`、`CharacterIdSync`、`CharacterMotionSync`、`NetworkAudioPlayer`。追加网络组件时保持原 NetworkBehaviour 顺序，只在根节点末尾追加。

## 维护触发

修改传输参数、Mirror 消息流程、Command、SyncVar、NetworkTransform、网络音频事件、序列化字段或兼容规则时更新本文档。
