# Unity 运行时、场景与资产

文档 ID：`UNITY-GUIDE`
状态：`Active`
最后核验：`2026-09-29`

## 版本与依赖

- Unity 编辑器版本：`2022.3.62f3c1`。
- Addressables：`1.22.3`。
- Unity MCP：从 GitHub `main` 分支引用。
- 其他显式包：Terrain Tools `5.0.6`、TextMeshPro `3.0.7`、Timeline `1.7.7`、UGUI `1.0.0`、Visual Scripting `1.9.4`。
- Mirror `96.11.2`：本地插件，`Assets/Mirror/` 与 `Assets/Plugins/` 在 `.gitignore` 中，不入库。安装与重装方法见 `Mirror_KCP_Config.md`。

## 构建与场景

- Build Settings 场景：`LobbyScene`（0 号，构建启动场景）、`MultiplayerSampleScene`（1 号）。
- `Assets/Core/Scenes/LobbyScene.unity`：联机原型场景，层级为 `NetworkManager`、`Ground`、`Directional Light`。NetworkManager 对象挂载 `Mirror.NetworkManager`、`NetworkManagerHUD`、`kcp2k.KcpTransport`、`AutoStartServerBuild`，playerPrefab 指向 `Player_Network.prefab`。配置细节见 `Mirror_KCP_Config.md`。
- `Assets/Core/Scenes/MultiplayerSampleScene.unity`：单机原型场景基底。根节点为 `Defaults`（方向光、EventSystem、禁用的场景相机、地形）、`Managers`（空节点）、`Character`（两个本地角色实例，"学园之星"禁用、"华丽飞踢"启用）、`TerrainGroup_0`。
- `Assets/Core/Scenes/Main.unity` 是空场景。

## Prefab

- `Assets/Core/Prefabs/Player_Network.prefab`：联网玩家，组件为 `NetworkIdentity`、`NetworkTransformReliable`、`CharacterController`、`NetworkPlayerController`，子对象 `PlayerCameraRig`（Camera、AudioListener、LocalPlayerCamera）。
- 角色 Prefab（"学园之星""华丽飞踢"）：保留角色相机、AudioListener、CharacterController 和移动组件，作为派对游戏角色的美术与组件基底。
- `CharactersForSync` 下的远端表现 Prefab 禁用 CharacterController 和本地移动组件，用作远端表现模型。

## 脚本目录

- `Assets/Core/Scripts/Networking/`：`NetworkPlayerController`、`LocalPlayerCamera`、`AutoStartServerBuild`。
- `Assets/Core/FrameWork/Scripts/`：按 `Core`、`SO`、`Scene`、`UI` 四层组织。`Core` 存放枚举与单例基类，`SO` 存放事件通道与场景 ScriptableObject，包括 `BoolEventChannelSO`。
- `Assets/Core/Models/_SharedDependencies/`：第三方共享库（Movement、DynamicBone）。`Movement/Resources/CharacterLocomotion.controller` 被 `CharacterAnimator.cs` 通过 `Resources.Load` 硬编码路径加载，不可移动。

## 事件资产

- `Assets/Core/EventSOs/BoolEventChannel.asset` 绑定 `Assets/Core/FrameWork/Scripts/SO/BoolEventChannelSO.cs`，当前无场景或 Prefab 引用，作为通用事件通道备用。

## 风险

- 场景相机禁用，视角依赖玩家 Prefab 内的相机。
- NavMesh 配置未绑定烘焙后的 `NavMeshData`。
- Unity MCP 跟踪远端 `main`，依赖解析结果随上游变化。
- 当前没有经过用户确认的 Unity 测试或批处理命令。

## 维护触发

修改 Unity 版本、包版本、Build Settings、场景层级、Prefab 绑定或事件资产时更新本文档。
