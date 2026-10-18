# Project 知识域路由

文档 ID：`BUS-PROJECT`
状态：`Active`
维护计数：`2/5`
最后更新：`2026-10-18`

## 任务路由

| 任务 | 权威文档 |
|---|---|
| Unity 版本、Packages、资产目录布局 | `Project_Guide.md` |
| 渲染管线、URP 资产、三渲二着色器与调参 | `Project_Guide.md`、`Assets/Core/Shaders/Toon/TOON_MIGRATION.md` |
| Addressables 配置与构建脚本 | `Project_Guide.md` |
| Player Settings、输入系统开关与旧输入 API 边界 | `Project_Guide.md` |
| 音效播放、空间坐标与播放器对象池 | `Project_Guide.md` |
| Prefab 组件组成 | `Project_Guide.md` |
| 经典石材、Blender、Cell Fracture、不规则预切 Cube 与粒子材质 | `Project_Guide.md`；资源预览同时读取 `../Scene/Scene_Guide.md` |
| 共用 Animator Controller、角色覆盖资产与 Prefab 动画绑定 | `Project_Guide.md`、`../Character/Character_Guide.md`，核对共用 Controller 与 `Assets/Core/Animations/Characters/` |
| 角色 FBX 导入、Humanoid Avatar 与骨骼映射 | `Project_Guide.md`、`../Character/Character_Guide.md` |
| 学园之星裙摆起点、动态骨骼与大腿碰撞器 | `Project_Guide.md`、`../Character/Character_Guide.md`，核对完整角色 Prefab |
| Mirror 参数、玩家 Prefab、联机验证和构建产物 | `Mirror_KCP_Config.md` |
| 场景列表与场景职责 | `../Scene/Scene_Guide.md` |
| 角色 Prefab 与移动配置 | `../Character/Character_Guide.md` |
| 网络组件布局与同步字段 | `../Multiplayer/Multiplayer_Guide.md` |

## 下级导航

| 子类 | Router |
|---|---|
| 无 | 无 |

## 主要证据路径

- `ProjectSettings/ProjectVersion.txt`
- `ProjectSettings/EditorBuildSettings.asset`
- `ProjectSettings/GraphicsSettings.asset`
- `ProjectSettings/QualitySettings.asset`
- `Packages/manifest.json`
- `Packages/packages-lock.json`
- `Assets/Core/Shaders/Toon/`
- `Assets/ToonEditor/`
- `Assets/Core/Scripts/GamePlayFrameWork/`
- `Assets/Core/Scripts/StateMachine/`
- `Assets/Rendering/`
- `Assets/Tests/`
- `Assets/Terrain/`
- `Assets/Core/Prefabs/Lights/`
- `Assets/Plugins/Mirror/version.txt`
- `Assets/Plugins/AddressableAssetsData/`
- `Assets/Core/Prefabs/NetworkPlayer.prefab`
- `Assets/Core/Prefabs/NetworkManager.prefab`
- `Assets/Core/Prefabs/Characters/`
- `Assets/Core/Prefabs/Environment/MinecraftClassic/`
- `Assets/Art/MinecraftClassic/`
- `C:/Users/12248/.codex/tools/blender-platform-setup/`
- `Assets/Core/Models/Resources/CharacterLocomotion.controller`
- `Assets/Core/Animations/Characters/`
- `Assets/Core/Models/异环_娜娜莉（华丽飞踢）/Model/娜娜莉（华丽飞踢）.fbx.meta`
- `Assets/Materials/`
- `D:/Unity/Releases/3D_MultiplayerGame/`

## 并发资源

- `workflow:Project`
- `path:ProjectSettings`
- `path:Packages/manifest.json`
- `path:Assets/Core/Shaders`
- `path:Assets/ToonEditor`
- `path:Assets/Core/Scripts/GamePlayFrameWork`
- `path:Assets/Core/Scripts/StateMachine`
- `path:Assets/Core/Models/Resources/CharacterLocomotion.controller`
- `path:Assets/Core/Animations/Characters`
- `path:Assets/Core/Prefabs/Environment/MinecraftClassic`
- `path:Assets/Art/MinecraftClassic`
- `path:C:/Users/12248/.codex/tools/blender-platform-setup`
- `path:Assets/Rendering`
- `path:Assets/Tests`
- `path:Assets/Terrain`
- `path:Assets/Core/Models/异环_娜娜莉（华丽飞踢）/Model/娜娜莉（华丽飞踢）.fbx.meta`
- `path:Assets/Plugins/AddressableAssetsData`
- `path:Assets/Materials`
- `path:D:/Unity/Releases/3D_MultiplayerGame`

## 能力边界

维护 Unity 工程配置、包依赖、Addressables、Prefab 序列化状态、资产目录布局、Blender 预切资源制作入口、环境表现资源和构建产物路径。场景内容归 `../Scene/Scene_Guide.md`，联机参数归 `Mirror_KCP_Config.md`。
