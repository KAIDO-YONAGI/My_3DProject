# Mirror 网络运行时

文档 ID：`MULTIPLAYER-GUIDE`
状态：`Active`
最后核验：`2026-10-14`

## 当前组件

- `AutoStartClient`：编辑器和普通客户端读取 `NetworkManager.networkAddress` 发起连接，断线后按重试间隔重连。批处理进程、活动 Server 和活动 Client 跳过自动连接。
- `NetworkCharacterManager`：按角色编号维护一份完整角色 Prefab，装配网络玩家表现，按身份管理相机、输入、控制器和碰撞体，并切换单机与联机表现。
- `CharacterIdSync`：同步 `characterId`，通过 `CommandSetCharacter` 接收本地玩家启动时上报的本机默认编号和本地拥有者提交的角色选择。
- `CharacterMotionSync`：同步运动表现快照（状态编号、局部方向、动画权重），拥有者上行、服务器校验并转发、远程客户端据此驱动动画。线格式与约束见 `Sync_Protocol.md` 的「运动表现」一节。
- `NetworkPlayer.prefab`：承载 `Transform`、`NetworkIdentity`、`NetworkTransformReliable`、`CharacterController`、`PlayerCharacterController`、`CharacterIdSync` 和 `CharacterMotionSync`。
- `NetworkManager.prefab`：承载 Mirror `NetworkManager`、`NetworkManagerHUD`、`KcpTransport` 和 `AutoStartClient`。
- `NetworkStartPosition`：常驻场景中的两个网络出生点。

项目网络脚本位于 `Assets/Core/Scripts/Networking/`：`Connection/` 放自动连接，`Sync/` 放角色编号与运动同步，`CharacterPresentation/` 放角色表现装配。

## 连接与玩家生成

```text
AutoStartClient.StartClient
  → NetworkClient.Connect
  → KcpTransport.ClientConnect
  → KcpClient 使用 UDP 与服务器握手
  → NetworkClient.Ready
  → NetworkClient.AddPlayer
  → NetworkManager.OnServerAddPlayer
  → NetworkServer.AddPlayerForConnection
  → 各客户端收到 NetworkPlayer 的 Spawn 数据
  → 本地玩家启动，上报本机默认角色编号
```

`NetworkManager.onlineScene` 为空。Mirror 完成认证后直接发送 Ready 和 AddPlayer。服务器以 `Random` 模式从两个 `NetworkStartPosition` 中选取位置，实例化 `NetworkPlayer` 并绑定到连接。

## 角色装配

`NetworkPlayer` 是统一的网络根，具体角色模型在每个客户端本地装配。

1. 服务器在 `CharacterIdSync.OnStartServer` 写入服务器配置的默认 `characterId`，作为客户端未上报时的兜底编号随初始 Spawn 下发。
2. 客户端在 `OnStartLocalPlayer` 把本机 `NetworkCharacterManager.defaultCharacterId` 通过 `CommandSetCharacter` 上报一次，服务器校验通过后覆盖 `characterId`。
3. 客户端在 `OnStartClient` 和 `OnStartLocalPlayer` 应用角色编号。
4. `NetworkCharacterManager` 根据 `CharacterIdSync.characterId` 从 `characters` 数组取得完整角色 Prefab。
5. 单机直接使用完整 Prefab；联机本地和远程实例化同一份 Prefab。
6. 视觉实例成为网络根子对象，局部位置和旋转归零。
7. 联机实例上的 `PlayerCharacterController` 与 `CharacterController` 停用，网络根负责输入、运动和碰撞。
8. 网络根的 `PlayerCharacterController` 绑定视觉实例的 Animator 和动画配置。
9. 本地 Camera、`ThirdPersonCamera` 和 `AudioListener` 启用并绑定为 `inputSpace`；远程对应组件保持关闭。

装配边界为：Mirror 把远程视觉角色挂在带 `NetworkTransformReliable` 的 `NetworkPlayer` 根节点下，视觉子预制件只提供模型、动画配置和相机，网络同步组件只放在根节点。根控制器位于 `Character/Control/`，运动算法位于 `Character/Movement/`，五态代码位于 `Character/Movement/States/`。

角色条目使用下标表达角色编号，每个下标保存一份完整角色 Prefab。默认编号取客户端本机 `NetworkCharacterManager.defaultCharacterId`，由本地玩家启动时上报；服务器自身的配置值是客户端未上报或上报编号无效时的兜底。角色资源对照见 `../Project/Project_Guide.md`。

每个网络玩家在 `presentations` 字典中对应一条 `CharacterPresentationState`，保存视觉实例、角色编号、来源 Prefab、已配置的本地身份以及相机组件数组。实例有效、编号和来源 Prefab 相同时复用该状态；身份相同时直接返回，身份变化时使用缓存组件重新配置相机和输入参考空间。编号或来源 Prefab 变化时替换实例，已销毁的实例重新装配。

装配时读取视觉 Prefab 的显式 `CharacterVisual` 引用，停用不属于当前用途的 Camera、`AudioListener` 和 `ThirdPersonCamera`，再配置本地相机目标和网络根输入空间。角色组件不通过运行时层级扫描补齐模型配置。

网络根的 `PlayerCharacterController` 首次初始化时缓存自身 `NetworkIdentity`，单机控制器也缓存身份缺失的结果。身份组件在初始化前配置；组件内的本地与远程归属值实时读取。此缓存与客户端稍后挂载的模型、Animator 初始化独立，角色装配和模型替换继续使用原有重绑入口。

根控制器在 `Awake` 初始化运动依赖，角色装配通过 `RebindAnimator` 绑定 Animator、动画配置和素材可用性缓存。逐帧更新使用缓存依赖，模型解绑后保持空动画引用，后续装配再次显式绑定。运动驱动在模型替换过程中沿用同一实例。

## 表现模式与相机收拢

单机角色通过管理器的场景序列化引用切换激活状态，单机相机、控制器和监听器由对应的场景引用数组启停。`ApplyPresentationMode`、`SetStandalonePlayerEnabled`、`SetStandaloneCameraEnabled` 均为实例方法，新增场景组件必须显式配置。动态生成的网络表现仍在装配时缓存自身子层级组件。

`NetworkCharacterManager` 在 `NetworkClient.isConnected=true` 后进入联机表现模式：

- 收起场景单机角色。
- 保留所有 Mirror 创建的网络根。
- 每个网络根只保留一个当前角色表现。
- 仅本地拥有者启用 Camera、`ThirdPersonCamera` 和 `AudioListener`。
- Additive 场景加载完成后再次应用相同规则。

两名客户端连接同一服务器时，每端显示两个网络角色，其中一个是本地拥有者，一个是远程同步角色。

## 同步边界

- `NetworkTransformReliable.SyncDirection=ClientToServer`。
- 本地拥有者移动网络根，变换数据发送到服务器。
- 服务器缓冲并广播变换快照。
- 远程客户端插值应用位置和旋转。
- `CharacterIdSync.characterId` 是 `SyncVar`。
- `CommandSetCharacter` 将本地角色选择提交到服务器，服务器校验后更新 `SyncVar`。Host 和远端客户端使用同一入口，表现刷新交给 SyncVar hook。

客户端和服务器使用相同的 NetworkBehaviour 顺序、组件布局和序列化字段。消息分层、字段约束和兼容规则见 `Sync_Protocol.md`。

代码状态机在本地生成 `Idle`、`Run`、`Sprint`、`Rising`、`Falling`。远程控制器不读取输入，不运行 `CharacterMotor.Tick`，当前位置由变换快照驱动，动画由收到的运动快照驱动，首个快照到达前使用 Idle。运动状态的传输格式见 `Sync_Protocol.md` 的「运动表现」一节。

## 源码字段说明

`Assets/Core/Scripts/Networking/` 中的可配置字段通过中文 `Tooltip` 提供用途、单位和配置约束。运行时缓存与关键装配入口使用源码注释说明数据归属和调用关系。

- `AutoStartClient`：两个自动连接开关分别用于编辑器和构建客户端；服务端地址统一配置在 `NetworkManager.networkAddress`；重连间隔使用真实时间秒。连接端口配置在 `KcpTransport.Port`。
- `NetworkCharacterManager`：`characters` 按下标保存每个角色的一份完整 Prefab；`defaultCharacterId` 是本机默认编号，客户端在本地玩家启动时上报它，服务器进程内的该字段是兜底编号。`presentations` 统一维护每个玩家的装配状态，清理时解除动画和输入引用、停用并销毁实例、移除该状态。
- `CharacterIdSync`：`characterId` 由服务器写入并经 `SyncVar` 同步；`playerController` 缓存网络根上的运动控制器。本机默认编号在本地玩家启动时上报一次，运行中的角色选择由拥有者经同一个 `Command` 提交到服务器，服务器拒收未通过校验的编号并记录警告。
- `CharacterMotionSync`：按 `syncInterval` 以可靠 `ClientToServer` 向观察者同步运动快照；越界快照丢弃并限流记录警告。上行读取根控制器缓存的当前快照。

## 维护触发

修改 `Assets/Core/Scripts/Networking/`、Mirror 组件列表、同步方向、连接策略、玩家生成、角色装配或相机归属时更新本文档。
