# My_3DProject

面向 2～5 人短回合乱斗的物理派对游戏。客户端基于 Unity，网络层使用 Mirror，专用服务器通过 KCP 传输承载连接、玩家生成与位置同步。

## 环境要求

| 项目 | 内容 |
|---|---|
| Unity 编辑器 | `2022.3.62f3c1` |
| 包依赖 | `Packages/manifest.json` |
| 第三方插件 | `Assets/Plugins/`（含 `Mirror/`） |

## 快速开始

1. 使用 Unity `2022.3.62f3c1` 打开项目，等待包解析与脚本编译完成。
2. 打开 `Assets/Core/Scenes/InitialScene.unity`，进入播放模式。`AutoStartClient` 连接 `127.0.0.1`。
3. 编辑器单独运行时，`PersistentScene` 中的单机 `LocalPlayer` 承担移动、物理与相机表现。
4. 联机运行时，先构建 Windows Server 并运行 `My_3DProject.exe`。服务器以 `HeadlessStartMode=AutoStartServer` 启动并在 UDP `7777` 监听，客户端连接后由 Mirror 生成 `NetworkPlayer`。
5. 本机多客户端联调时，使用 `Assets/Plugins/ParrelSync/` 克隆编辑器实例。

## 场景流程

三个场景按以下顺序进入 Build Settings。

```text
InitialScene                启动引导，注册并加载常驻场景
  → PersistentScene         常驻管理层，承载网络、角色表现、场景切换与时间管理
  → MultiplayerSampleScene  玩法环境，提供地形、碰撞与光照
```

`InitialLoad` 从 `InitialScene` 加载 `PersistentScene`，`SceneChanger` 以 Additive 模式加载 `MultiplayerSampleScene`。Mirror 的 `onlineScene` 留空，场景切换由 `SceneChanger` 负责。

## 目录结构

| 路径 | 内容 |
|---|---|
| `Assets/Core/Scenes/` | 三个场景 |
| `Assets/Core/Prefabs/` | `NetworkManager`、`NetworkPlayer`、`CharactersForLocal`、`CharactersForSync` |
| `Assets/Core/Scripts/Networking/` | 自动连接、角色编号同步、角色表现装配 |
| `Assets/Core/Scripts/Movement/` | 输入、移动、重力、动画、第三人称相机与 `DynamicBone` |
| `Assets/Core/Models/` | 角色模型源文件与骨骼配置说明 |
| `Assets/Core/Terrain/` | 玩法地形数据 |
| `Assets/Core/SO/` | 场景与事件 ScriptableObject |
| `Assets/Plugins/AddressableAssetsData/` | Addressables 分组与构建配置 |
| `Assets/Core/My_FrameWork/` | 场景管理、时间管理、UI 与 ScriptableObject 基础设施 |
| `Assets/Materials/ForestMaterials/` | 地形植被与地表贴图 |
| `Assets/Plugins/TextMesh Pro/` | TextMeshPro 资源 |
| `Assets/Plugins/Mirror/MirrorScriptTemplates/` | Mirror 脚本模板 |
| `Assets/Plugins/ParrelSync/` | 多编辑器实例工具 |
| `Y_MultipleAgentWorkflow/` | 项目权威文档 |
| `docs/` | 计划、草案与教学材料 |

## 网络配置

| 配置项 | 值 |
|---|---|
| Transport | `kcp2k.KcpTransport` |
| 服务器端口 | UDP `7777` |
| 客户端地址 | `127.0.0.1` |
| 玩家 Prefab | `Assets/Core/Prefabs/NetworkPlayer.prefab` |
| 自动创建玩家 | `true` |
| 出生点模式 | `Random` |
| 同步方向 | `ClientToServer` |
| Mirror onlineScene | 留空 |
| 自动重连间隔 | `3` 秒 |

`NetworkManager.prefab` 承载 Mirror `NetworkManager`、`NetworkManagerHUD`、`KcpTransport` 与 `AutoStartClient`。`NetworkPlayer.prefab` 承载 `NetworkIdentity`、`NetworkTransformReliable`、`CharacterController`、`PlayerCharacterController` 与 `NetworkCharacterSync`。

## 角色资源

两个角色模型各自提供本地表现与远程同步两套 Prefab，`NetworkCharacterManager` 按下标成对使用。

| 编号 | 模型 | 本地拥有者 | 远程玩家 |
|---|---|---|---|
| `0` | 娜娜莉（华丽飞踢） | `CharactersForLocal/娜娜莉（华丽飞踢）.prefab` | `CharactersForSync/娜娜莉（华丽飞踢）_Sync.prefab` |
| `1` | 娜娜莉（学园之星） | `CharactersForLocal/娜娜莉（学园之星）.prefab` | `CharactersForSync/娜娜莉（学园之星）_Sync.prefab` |

默认角色编号为 `1`。两个模型的源文件、材质、贴图与动画分别位于 `Assets/Core/Models/异环_娜娜莉（华丽飞踢）/` 与 `Assets/Core/Models/异环_娜娜莉（学园之星）/`。

本地 Prefab 提供 `Camera`、`AudioListener`、`ThirdPersonCamera`、`Animator`、`CharacterController`、`DynamicBone` 与 `PlayerCharacterController`，供本地拥有者获得完整视角与表现。

同步 Prefab 提供 `Animator`、`CharacterController`、`DynamicBone` 与 `PlayerCharacterController`，供远程玩家显示模型与动画。

角色骨骼物理由 `DynamicBone` 与 `DynamicBoneCollider` 驱动，参数含义与当前取值记录在 `Assets/Core/Models/骨骼配置须知.md`。

### 装配规则

`NetworkPlayer` 是稳定的网络根，服务器为每个连接生成一个网络根，客户端根据 `NetworkCharacterSync.characterId` 与 `NetworkIdentity.isLocalPlayer` 装配表现。

- 视觉实例挂到网络根下，局部 Transform 归零。
- 视觉实例自带的 `PlayerCharacterController` 与 `CharacterController` 停用，输入、运动与碰撞由网络根负责。
- 网络根的 `PlayerCharacterController` 绑定视觉实例的 `Animator` 与动画配置。
- 本地拥有者启用相机、`ThirdPersonCamera` 与 `AudioListener`，相机成为 `PlayerCharacterController.inputSpace`。
- 远程玩家显示模型与动画，相机与音频监听器保持关闭。
- 连接完成后，`PersistentScene` 中的单机 `LocalPlayer` 收起，活动角色数量与 Mirror 玩家数量一致。

## 插件与依赖

### 第三方插件

| 插件 | 位置 | 用途 |
|---|---|---|
| Mirror `96.11.2` | `Assets/Plugins/Mirror/` | 网络框架，附带 KCP 传输 |
| ParrelSync | `Assets/Plugins/ParrelSync/` | 多开 Unity 编辑器做联机联调 |
| DynamicBone | `Assets/Core/Scripts/Movement/DynamicBone/` | 角色头发、裙摆与尾巴的骨骼物理 |

### Unity 包

| 包 | 版本 |
|---|---|
| Addressables | `1.22.3` |
| Input System | `1.14.2` |
| Terrain Tools | `5.0.6` |
| TextMeshPro | `3.0.7` |
| Timeline | `1.7.7` |
| UGUI | `1.0.0` |
| Visual Scripting | `1.9.4` |
| Unity MCP | Git 依赖 `main` 分支 |

## 构建

```text
客户端      D:/Unity/Releases/3D_MultiplayerGame/Client/
专用服务器  D:/Unity/Releases/3D_MultiplayerGame/Server/
```

两个构建都包含 `InitialScene`、`PersistentScene` 与 `MultiplayerSampleScene`，并使用相同的 `NetworkPlayer` 组件布局与序列化字段。Windows Server 构建以 `HeadlessStartMode=AutoStartServer` 启动，在 UDP `7777` 监听。

## 文档

`Y_MultipleAgentWorkflow/` 是项目权威文档入口，按 `Client`、`Networking`、`Server`、`Protocol`、`UnityRuntime` 拆分知识域，`Workflow` 管理文档路由与并发租约。`docs/` 存放计划、草案与教学材料。
