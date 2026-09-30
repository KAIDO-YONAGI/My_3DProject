# Mirror 网络运行时

文档 ID：`NETWORKING-GUIDE`
状态：`Active`
最后核验：`2026-09-30`

## 当前组件

- `AutoStartClient`：编辑器和普通客户端连接 `127.0.0.1`，断线后每 3 秒重试；批处理、Server active 或 Client active 时不重复连接。
- `NetworkCharacterManager`：挂在 `PersistentScene/Managers/NetworkCharacterManager`，按同一角色编号维护两套配置：本地拥有者使用 `CharactersForLocal`，远程拥有者使用 `CharactersForSync`。
- `NetworkCharacterSync`：挂在 `Player_Network.prefab`，只同步 `characterId`，不持有模型配置。
- 当前玩家 Prefab `Player_Network.prefab` 使用 `NetworkIdentity`、`NetworkTransformReliable`、`CharacterController`、`PlayerCharacterController` 和 `NetworkCharacterSync`。
- `NetworkManager` 固定配置在 `PersistentScene`，`dontDestroyOnLoad=false`、`onlineScene` 为空；场景生命周期由项目自己的 Additive `SceneChanger` 管理。
- 单机 `LocalPlayer` 不由 Mirror 生成，也不由 `NetworkCharacterManager` 生成；它是 `PersistentScene` 中的编辑器可见角色实例，继续使用本地角色 Prefab。网络玩家使用 `Player_Network.prefab` 网络外壳；`NetworkCharacterManager.localCharacterPrefabs[]` 只能引用 `CharactersForLocal`，`characterPrefabs[]` 只能引用 `CharactersForSync`，两个数组按角色编号一一对应，禁止混用。
- `PlayerCharacterController` 仍是普通 `MonoBehaviour`；角色切换时由 `NetworkCharacterManager` 重新绑定根对象的 `Animator`，销毁旧角色前先清空旧动画驱动。
- `PersistentScene` 不再覆盖 `NetworkManager.playerPrefab`；唯一来源是 `Assets/Core/Prefabs/NetworkManager.prefab` 中的 `Player_Network.prefab`。

## 当前同步边界

`NetworkTransformReliable.SyncDirection=ClientToServer`。本地玩家的移动结果上传服务端，再由 Mirror 广播；当前没有自定义 NetworkMessage、Command/Rpc 协议层。

当前网络组件和通道事实以 `../UnityRuntime/Mirror_KCP_Config.md` 为准。

## 已验证

- `PersistentScene` 可连接到 `MultiplayerSampleScene`。
- Host 模式下已验证本地角色生成、模型编号 `0 -> 1 -> 0` 切换，控制台无 `MissingReferenceException`。
- 玩家生成后具有本地所有权，移动没有在等待 2 秒后被拉回。
- 混用新旧玩家组件布局曾导致 `OnDeserialize` / `EndOfStreamException`，客户端与服务端必须使用同一组件布局构建。

## 维护触发

修改 `Assets/Core/Scripts/Networking/`、Mirror 组件列表、同步方向、连接重试策略或玩家网络归属时更新本文档。
