# Unity 运行时、场景与资产

文档 ID：`UNITY-GUIDE`
状态：`Active`
最后核验：`2026-09-30`

## 版本与依赖

- Unity 编辑器：`2022.3.62f3c1`。
- Mirror：`96.11.2`，本地插件位于 `Assets/Mirror/`，不入库。
- Addressables：`1.22.3`。
- Terrain Tools：`5.0.6`。
- TextMeshPro：`3.0.7`。
- Timeline：`1.7.7`。
- UGUI：`1.0.0`。
- Visual Scripting：`1.9.4`。
- Unity MCP：通过 GitHub `main` 分支引用，解析结果可能随上游变化。

## Build Settings

当前启用两个场景，顺序如下：

1. `Assets/Core/Scenes/PersistentScene.unity`
2. `Assets/Core/Scenes/MultiplayerSampleScene.unity`

联机构建必须同时包含这两个场景。旧的 `LobbyScene` 不再是当前联机入口。

## 场景职责

### PersistentScene

- 联机启动和常驻场景。
- `NetworkManager` 对象挂载 Mirror NetworkManager、`kcp2k.KcpTransport`、NetworkManagerHUD 和 `AutoStartClient`。
- `networkAddress=127.0.0.1`。
- `onlineScene` 为空；由 `Managers/SceneChanger` 以 Additive 模式加载 `Assets/Core/Scenes/MultiplayerSampleScene.unity`。
- `playerPrefab=Assets/Core/Prefabs/Player_Network.prefab`。
- `NetworkStartPosition` 位于玩法地形上方约 `0.2m`；角色随后由 `CharacterController` 重力和地形碰撞自然落地，不通过运行时传送修正位置。

### MultiplayerSampleScene

- 当前在线玩法场景，提供地形、`TerrainCollider`、方向光、EventSystem、相机及角色相关场景内容。
- `SceneChanger` Additive 加载完成后，`NetworkCharacterManager` 会重新应用表现模式；只有真正连接服务端后才禁用场景单机角色和非网络相机，联机时仅本地网络角色的 Camera 保持启用。
- 网络玩家由 Mirror 在连接后生成，出生点和地形碰撞必须在同一次客户端/服务器构建中保持一致。
- 场景结构或角色内容发生变化时，应直接核验场景序列化数据，不沿用旧文档中的层级快照。

## 当前玩家 Prefab

路径：`Assets/Core/Prefabs/Player_Network.prefab`

关键组件：

- `Mirror.NetworkIdentity`
- `Mirror.NetworkTransformReliable`，`SyncDirection=ClientToServer`
- `CharacterController`
- `PlayerCharacterController`
- `NetworkCharacterSync`

`PlayerCharacterController` 保持普通 `MonoBehaviour`。它可选读取同对象上的 `NetworkIdentity`：`NetworkClient.isConnected=true` 时只有 `isLocalPlayer` 为真的实例采集输入；尚未连接或正在重试时，没有 `NetworkIdentity` 的单机角色 Prefab 继续按原逻辑运行。不得仅为判断本地玩家而把该脚本改成 `NetworkBehaviour`。

`PersistentScene/Managers/NetworkCharacterManager` 持有 `localCharacterPrefabs[]`、`characterPrefabs[]` 和 `defaultCharacterId`。两个数组按相同下标表示同一个角色编号：本地拥有者加载 `CharactersForLocal`，远程拥有者加载 `CharactersForSync`。只有本地视觉 Prefab 保留 `ThirdPersonCamera` 和 `Camera`；切换角色时网络根先释放旧动画驱动，再绑定新模型的 Animator。

## 脚本职责

- `Assets/Core/Scripts/Networking/AutoStartClient.cs`：编辑器和普通客户端自动连接 `127.0.0.1`，失败或断开后每 3 秒重试；批处理以及已启动 Server/Client 的进程不重复连接。
- `Assets/Core/Scripts/Networking/NetworkCharacterManager.cs`：联机角色编号、远程角色加载、单机/联机表现切换、本地相机归属和 Additive 场景加载后的相机收拢。
- `Assets/Core/Scripts/Networking/NetworkCharacterSync.cs`：Player_Network 上的角色编号同步入口。
- `Assets/Core/Scripts/Networking/LocalPlayerCamera.cs`：历史相机辅助脚本，不挂在当前 `Player_Network`。
- `Assets/Core/Scripts/Networking/NetworkPlayerController.cs`：历史网络移动辅助脚本，不挂在当前 `Player_Network`。
- `Assets/Core/Scripts/Movement/Runtime/PlayerCharacterController.cs`：单机与联机共用的输入和移动入口。
- `Assets/Core/FrameWork/Scripts/`：按 Core、SO、Scene、UI 组织通用框架。

## 事件资产

- `Assets/Core/SO/EventSOs/BoolEventChannel.asset` 是当前唯一的通用布尔事件通道资产，GUID 为 `0ae334929aa1e354d818f91aeeac1bc9`。
- 该资产目前是缺失脚本资产：它引用的脚本 GUID `2e72fff171e71a040b9c5ce0f06a5cbe`（原 `Assets/Core/Scripts/Events/BoolEventChannelSO.cs`）已在提交 `2d61248` 删除，项目中不再存在名为 `BoolEventChannelSO` 的类型。
- 该资产当前没有被任何场景或 Prefab 引用；恢复脚本或废弃该资产前，不要把它的缺失脚本状态当作可用能力。
- 修改事件资产、脚本 GUID 或引用关系时，必须以 Unity 序列化引用和实际运行结果为证据。

## 已验证状态

- Build Settings 中两个当前场景均启用。
- `Server_12_7 + 两个 Unity Editor Play` 已验证 KCP 连接、玩家生成和本地/远程角色同步。
- 本地玩家实例满足 `local=True`、`owned=True`，同步方向为 `ClientToServer`。
- 本地角色运行时稳定在 `Y=-9.167`、`isGrounded=True`，远程角色稳定在 `Y=-9.170`；模型边界最低点与根节点约差 `0.01m`。
- 验证中启用相机仅为网络角色子节点的 `Main Camera`；场景原有 `Main Camera` 与 Additive 场景 `Camera` 均已禁用。
- 将本地相机偏航 `90°` 后，`CharacterMotor` 的前进方向从 `(0,0,1)` 变为 `(1,0,0)`，移动方向与相机朝向同步。
- 无服务端时场景单机角色从 `Y=3.38` 由 `CharacterController` 重力自然落到 `Y=-9.491`，`isGrounded=True`，单机相机保持启用。
- 双编辑器连接后每端 `networkPlayers=2`、`activeNetworkVisuals=2`、`standaloneActive=0`，场景单机角色被禁用，不再出现第三个悬空角色。
- 验证结束后服务器、UDP 7777 监听和 Editor Play 均已清理。

## 风险与维护触发

- 新旧客户端和服务器混用可能因 NetworkBehaviour 组件布局不一致触发 `OnDeserialize` 或 `EndOfStreamException`。
- Unity MCP 跟踪远端 `main`，更新依赖后需要重新核验。
- 修改 Unity/包版本、Build Settings、场景、Prefab、事件资产或移动输入归属时更新本文档。
- Mirror、KCP、连接和构建细节统一维护在 `Mirror_KCP_Config.md`。
