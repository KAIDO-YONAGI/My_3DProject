# Mirror 网络运行时

文档 ID：`NETWORKING-GUIDE`
状态：`Active`
最后核验：`2026-10-03`

## 当前组件

- `AutoStartClient`：编辑器和普通客户端连接 `127.0.0.1`，断线后每 3 秒重试。批处理进程、活动 Server 和活动 Client 跳过自动连接。
- `NetworkCharacterManager`：按角色编号维护本地角色 Prefab 与同步角色 Prefab，装配网络玩家表现，管理相机归属，并切换单机与联机表现。
- `NetworkCharacterSync`：同步 `characterId`，通过 `CmdSetCharacter` 接收本地拥有者的角色选择。
- `NetworkPlayer.prefab`：承载 `NetworkIdentity`、`NetworkTransformReliable`、`CharacterController`、`PlayerCharacterController` 和 `NetworkCharacterSync`。
- `NetworkManager.prefab`：承载 Mirror `NetworkManager`、`NetworkManagerHUD`、`KcpTransport` 和 `AutoStartClient`。
- `NetworkStartPosition`：`PersistentScene` 中的 `NetworkPlayerSpawn_A` 与 `NetworkPlayerSpawn_B`。

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
```

`NetworkManager.onlineScene` 为空。Mirror 完成认证后直接发送 Ready 和 AddPlayer。服务器以 `Random` 模式从两个 `NetworkStartPosition` 中选取位置，实例化 `NetworkPlayer` 并绑定到连接。

## 角色装配

`NetworkPlayer` 是统一的网络根，具体角色模型在每个客户端本地装配。

1. 服务器在 `NetworkCharacterSync.OnStartServer` 设置默认 `characterId`。
2. 客户端在 `OnStartClient` 和 `OnStartLocalPlayer` 应用角色编号。
3. `NetworkCharacterManager` 根据 `NetworkIdentity.isLocalPlayer` 选择 Prefab 数组。
4. 本地拥有者使用 `localCharacterPrefabs`，获得完整角色、Camera、`ThirdPersonCamera` 和 `AudioListener`。
5. 远程玩家使用 `characterPrefabs`，获得同步模型和 Animator。
6. 视觉实例成为网络根子对象，局部位置和旋转归零。
7. 视觉实例上的 `PlayerCharacterController` 与 `CharacterController` 停用，网络根负责输入、运动和碰撞。
8. 网络根的 `PlayerCharacterController` 绑定视觉实例的 Animator 和动画配置。
9. 本地 Camera 绑定为 `inputSpace`，远程相机、远程音频监听器和远程本地控制组件保持关闭。

两个 Prefab 数组使用相同下标表达同一角色编号。当前默认角色编号为 `1`。

## 多余角色与相机收拢

`PersistentScene` 保留一个单机 `LocalPlayer`，便于无服务端时直接开发移动、物理和相机。`NetworkCharacterManager` 在 `NetworkClient.isConnected=true` 后进入联机表现模式：

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
- `NetworkCharacterSync.characterId` 是 `SyncVar`。
- `CmdSetCharacter` 将本地角色选择提交到服务器，服务器校验后更新 `SyncVar`。

客户端和服务器使用相同的 NetworkBehaviour 顺序、组件布局和序列化字段。

## 源码字段说明

`Assets/Core/Scripts/Networking/` 中的可配置字段通过中文 `Tooltip` 提供用途、单位和配置约束。运行时缓存与关键装配入口使用源码注释说明数据归属和调用关系。

- `AutoStartClient`：两个自动连接开关分别用于编辑器和构建客户端；`connectAddress` 指定服务端地址；`reconnectInterval` 使用真实时间秒。连接端口配置在 `KcpTransport.Port`。
- `NetworkCharacterManager`：`localCharacterPrefabs` 与 `characterPrefabs` 按下标配对；`defaultCharacterId` 指定服务器生成玩家时的初始编号。实例缓存与编号缓存共同复用重复回调中的角色表现。
- `NetworkCharacterSync`：`characterId` 由服务器写入并经 `SyncVar` 同步；`playerController` 缓存网络根上的运动控制器。角色选择经拥有者 `Command` 提交到服务器。

## 维护触发

修改 `Assets/Core/Scripts/Networking/`、Mirror 组件列表、同步方向、连接策略、玩家生成、角色装配或相机归属时更新本文档。
