# Unity 运行时、场景与资产

文档 ID：`UNITY-GUIDE`
状态：`Active`
最后核验：`2026-10-03`

## 版本与依赖

- Unity 编辑器：`2022.3.62f3c1`。
- Mirror：`96.11.2`。
- Addressables：`1.22.3`。
- Input System：`1.14.2`。
- Terrain Tools：`5.0.6`。
- TextMeshPro：`3.0.7`。
- Timeline：`1.7.7`。
- UGUI：`1.0.0`。
- Visual Scripting：`1.9.4`。
- Unity MCP：Git 依赖的 `main` 分支。

## Build Settings

启用场景顺序如下：

1. `Assets/Core/Scenes/InitialScene.unity`
2. `Assets/Core/Scenes/PersistentScene.unity`
3. `Assets/Core/Scenes/MultiplayerSampleScene.unity`

## 场景职责

### InitialScene

`InitialScene` 是构建入口，根对象为 `Bootstrapper`。`InitialLoad` 读取 Persistent Scene 配置，向 `PersistentSceneRegistry` 注册常驻场景，并以 Additive 模式加载 `PersistentScene`。

### PersistentScene

`PersistentScene` 是常驻运行层，包含：

- `Managers/NetworkManager`：Mirror、KCP、HUD 和自动连接。
- `Managers/NetworkCharacterManager`：本地与远程角色表现装配、相机归属和单机联机模式切换。
- `Managers/SceneChanger`：以 Additive 模式加载玩法场景。
- `Managers/TimeManager`：项目时间管理。
- `EventSystem`：UI 输入。
- `LocalPlayer`：单机开发角色与本地相机。
- `NetworkPlayerSpawn_A`：位置 `(-2.63, -9.3075, -31.27)`。
- `NetworkPlayerSpawn_B`：位置 `(-0.63, -9.05172, -31.27)`。

`SceneChanger.firstSceneToLoad` 引用 `Assets/Core/SO/MultiplayerSampleSceneSO.asset`。该资产配置场景名 `MultiplayerSampleScene` 和初始位置 `(-2.63, 3.38, -31.27)`。

### MultiplayerSampleScene

`MultiplayerSampleScene` 是玩法环境层，提供 Terrain、`TerrainCollider`、方向光、默认环境对象和 Terrain 分组。场景中的 `Camera` 对象保持停用，运行视角来自单机角色或本地网络角色。

## NetworkManager Prefab

路径：`Assets/Core/Prefabs/NetworkManager.prefab`

关键组件：

- `Mirror.NetworkManager`
- `Mirror.NetworkManagerHUD`
- `kcp2k.KcpTransport`
- `AutoStartClient`

关键配置：

- `networkAddress=127.0.0.1`
- `maxConnections=100`
- `sendRate=60`
- `runInBackground=true`
- `headlessStartMode=AutoStartServer`
- `onlineScene` 为空
- `offlineScene` 为空
- `playerPrefab=Assets/Core/Prefabs/NetworkPlayer.prefab`
- `autoCreatePlayer=true`
- `playerSpawnMethod=Random`

## NetworkPlayer Prefab

路径：`Assets/Core/Prefabs/NetworkPlayer.prefab`

关键组件：

- `Mirror.NetworkIdentity`
- `Mirror.NetworkTransformReliable`
- `CharacterController`
- `PlayerCharacterController`
- `NetworkCharacterSync`

`NetworkTransformReliable` 使用 `ClientToServer`，同步位置和旋转，启用变化检测、旋转压缩和插值。`CharacterController` 的高度为 `2`，半径为 `0.5`，中心为 `(0, 1, 0)`。

`PlayerCharacterController` 的移动配置为运行速度 `6.8`、疾跑速度 `10.8`、加速度 `32`、减速度 `38`、空中控制 `0.45`、旋转速度 `360`、重力 `-28`、跳跃高度 `1.2`、接地吸附速度 `-2`。

## 角色表现资源

`NetworkCharacterManager` 的角色数组按下标配对：

| 角色编号 | 本地拥有者 Prefab | 远程同步 Prefab |
|---|---|---|
| `0` | `CharactersForLocal/娜娜莉（华丽飞踢）.prefab` | `CharactersForSync/娜娜莉（华丽飞踢）_Sync.prefab` |
| `1` | `CharactersForLocal/娜娜莉（学园之星）.prefab` | `CharactersForSync/娜娜莉（学园之星）_Sync.prefab` |

默认角色编号为 `1`。本地 Prefab 提供 Camera 和 `ThirdPersonCamera`。同步 Prefab 提供远程模型、Animator 和动画配置。

## Inspector 配置提示

`NetworkManager` Prefab 上的 `AutoStartClient` 字段显示中文 Tooltip，说明自动连接的运行环境、地址配置入口和重试间隔。连接地址统一填写在 Mirror `NetworkManager` 的 `Network Address`，使用 IP 或主机名。自动连接和 HUD 连接共用 `networkAddress`，端口由同一 Prefab 上的 `KcpTransport.Port` 提供。

`PersistentScene` 中 `NetworkCharacterManager` 的两个角色数组与默认编号显示中文 Tooltip。数组下标从 `0` 开始，两套数组的同一下标表示同一角色；默认编号对应的两个位置均配置有效 Prefab。

## 角色数量与相机规则

- 单机状态使用 `PersistentScene` 中的 `LocalPlayer`。
- 联机状态收起 `LocalPlayer`。
- 每个 Mirror 玩家对应一个 `NetworkPlayer` 网络根和一个角色表现。
- 本地玩家装配带相机的本地表现。
- 远程玩家装配同步表现。
- 每个客户端仅启用本地拥有者的 Camera、`ThirdPersonCamera` 和 `AudioListener`。
- 表现缓存同时比较角色编号和来源 Prefab；身份变化时按对应数组选择资源，并更新相机归属。
- 当前实例的相机组件在装配时缓存，重复初始化回调在身份相同时复用已配置状态。
- Additive 场景加载完成后重新应用相同规则。

## 维护触发

修改 Unity 或包版本、Build Settings、场景、Prefab、角色数组、出生点、相机归属或移动配置时更新本文档。Mirror 与 KCP 参数统一维护在 `Mirror_KCP_Config.md`。
