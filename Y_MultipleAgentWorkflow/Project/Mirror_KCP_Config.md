# Mirror 与 KCP 工程配置

文档 ID：`UNITY-NETCFG`
状态：`Active`
最后核验：`2026-10-10`（角色装配与暂缓范围局部核验）

## 配置总览

| 配置项 | 当前值 | 配置位置 |
|---|---|---|
| Mirror 版本 | `96.11.2` | `Assets/Plugins/Mirror/version.txt` |
| Transport | `kcp2k.KcpTransport` | `NetworkManager.prefab` |
| 服务器端口 | UDP `7777` | `KcpTransport.Port` |
| 客户端地址 | `127.0.0.1` | NetworkManager.networkAddress |
| 玩家 Prefab | `Assets/Core/Prefabs/NetworkPlayer.prefab` | NetworkManager |
| 自动创建玩家 | `true` | NetworkManager |
| 出生点模式 | `Random` | NetworkManager |
| 场景切换 | `SceneChanger` Additive 加载 | PersistentScene |
| Mirror onlineScene | 空 | NetworkManager |
| 同步方向 | `ClientToServer` | NetworkTransformReliable |
| 自动重连间隔 | `3` 秒 | AutoStartClient |

其余 KCP 参数在 `NetworkManager` Prefab 的 `KcpTransport` 上配置，含端口、双栈、低延迟开关、更新间隔、超时、收发缓冲与窗口大小。

## 场景启动关系

```text
InitialScene
  → InitialLoad
  → PersistentScene（常驻层：网络管理、角色表现装配、场景切换、单机角色、网络出生点）
  → MultiplayerSampleScene（玩法层：地形、碰撞与光照）
```

Mirror 的 `onlineScene` 保持为空。`SceneChanger` 负责玩法场景加载，NetworkManager 负责连接、身份、玩家创建和网络同步。

## 客户端启动

`AutoStartClient` 在编辑器和普通客户端构建中运行：

1. 检查当前进程类型和 Mirror 活动状态。
2. 使用 `NetworkManager.networkAddress` 指定连接目标。
3. 调用 `NetworkManager.StartClient`。
4. 连接结束后继续监视状态。
5. 断开时等待重试间隔后再次连接。

## 服务器启动

Windows Server 构建使用 `HeadlessStartMode=AutoStartServer`：

1. `NetworkManager.StartServer` 初始化服务端。
2. `NetworkServer.Listen` 注册 Transport 回调。
3. `KcpTransport.ServerStart` 创建 `KcpServer`。
4. `KcpServer.Start` 创建非阻塞 UDP socket 并绑定 `7777`。
5. EarlyUpdate 接收数据，LateUpdate 刷新待发送数据。

## 玩家生成

客户端连接并完成认证后，Mirror 发送 Ready 和 AddPlayer。服务器从两个 `NetworkStartPosition` 中随机选择一个位置，创建 `NetworkPlayer`，再通过 `NetworkServer.AddPlayerForConnection` 建立连接归属。

`NetworkPlayer` 生成后：

- 本地拥有者运行输入、移动和本地相机。
- 远程实例接收服务器广播的 Transform。
- `NetworkCharacterSync` 同步角色编号。
- `NetworkCharacterManager` 为网络根装配本地或远程表现。

## 表现装配与相机隔离

`NetworkPlayer` 自身不保存具体角色模型。`NetworkCharacterManager` 读取 `characterId` 和 `isLocalPlayer`：

- 本地拥有者实例化 `localCharacterPrefabs[characterId]`。
- 远程玩家实例化 `characterPrefabs[characterId]`。
- 视觉实例挂到网络根下并重置局部 Transform。
- 视觉子对象上的移动组件和 `CharacterController` 停用。
- 网络根的移动组件绑定新模型 Animator。
- 本地 Camera 成为 `inputSpace` 和 `MainCamera`。
- 远程 Camera、`ThirdPersonCamera` 和 `AudioListener` 保持关闭。
- 连接完成后场景单机角色关闭。
- 单机角色通过 `NetworkCharacterManager.standalonePlayer` 场景引用启停，单机相机、控制器和监听器由对应的场景引用指向该角色的 `Main Camera`，不全局查找或查询父级网络身份。
- Additive 场景加载完成后再次收拢相机和单机角色。

`presentations` 按玩家保存实例、编号、来源 Prefab、相机身份和组件缓存。重复回调命中有效实例、相同编号与 Prefab 时复用状态；身份变化时更新相机配置，Prefab 变化时替换表现实例。Host 和远端客户端均通过 `CommandSetCharacter` 提交角色选择，服务器校验后更新 SyncVar。

## 移动、重力与高度

本地输入驱动网络根的 `PlayerCharacterController`。`CharacterMotor` 根据本地 Camera 的水平 forward 和 right 计算方向，调用 `CharacterController.Move`。重力持续作用，出生后角色依据地形碰撞自然落地。

`NetworkTransformReliable` 负责同步移动结果。项目移动链路不调用 Transform 传送方法，也不执行运行时高度钳制。

本地运动代码采用 `Idle`、`Run`、`Sprint`、`Rising`、`Falling` 状态机，脚本位于 `Character/Movement/States/`，Animator 保留状态内方向混合。网络脚本位于 `Assets/Core/Scripts/Networking/`。

远程控制器不读取输入，位置由变换快照驱动，首个运动快照到达前使用 Idle。运动表现状态的传输格式见 `../Multiplayer/Sync_Protocol.md` 的「运动表现」一节。

## 构建

客户端输出根目录：

`D:/Unity/Releases/3D_MultiplayerGame/Client/`

专用服务器输出根目录：

`D:/Unity/Releases/3D_MultiplayerGame/Server/`

两个构建都包含以下场景：

1. `InitialScene`
2. `PersistentScene`
3. `MultiplayerSampleScene`
4. `MultiplayerSampleScene 1`

客户端与服务器使用相同的 `NetworkPlayer` 组件布局、NetworkBehaviour 顺序和序列化字段。

## 验收

- 服务端日志显示 UDP `7777` 已监听。
- 每个客户端完成 KCP 连接和 Mirror Ready。
- 每个连接生成一个 `NetworkPlayer`。
- 两名客户端连接时，每端显示两个网络角色。
- 每端只有本地拥有者的 Camera 和 `AudioListener` 启用。
- 本地前进方向随相机水平朝向变化。
- 玩家从出生点依据重力和地形碰撞落地。
- 角色切换后各端使用同一 `characterId` 对应的本地或同步 Prefab。

## 维护触发

修改 Mirror 版本、KCP 参数、NetworkManager、玩家 Prefab、角色装配、连接地址、出生点或构建配置时更新本文档。
