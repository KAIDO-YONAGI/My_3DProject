# Scene 知识域路由

文档 ID：`BUS-SCENE`
状态：`Active`
维护计数：`1/5`
最后更新：`2026-10-19`

## 任务路由

| 任务 | 权威文档 |
|---|---|
| 场景列表与场景职责 | `Scene_Guide.md` |
| 场景引导、常驻层与玩法层加载链路 | `Scene_Guide.md`，并读取 `Assets/Core/Scripts/GamePlayFrameWork/Scripts/Scene/` |
| 单机与联机表现切换、角色与相机收拢 | `Scene_Guide.md` |
| 场景配置资产与初始位置 | `Scene_Guide.md`，并读取 `Assets/Core/SO/` |
| MinecraftClassicPreview、经典石材预览、不规则碎块展开与粒子样本 | `Scene_Guide.md`、`../Project/Project_Guide.md` |
| MinecraftClassicPlatform、独立平台根对象、烘焙外表面、合并碰撞与延迟碎块 | `Scene_Guide.md`、`../Project/Project_Guide.md` |
| 平台场景转换、独立布局与破碎表现接入 | `Scene_Guide.md`、`../Project/Project_Guide.md`；操作示例见 `../../docs/VoxelPlatformRenderer.md` |
| 第一层中央平台编辑、独立 Cube 与排除烘焙 | `Scene_Guide.md`、`../Project/Project_Guide.md`；操作示例见 `../../docs/VoxelPlatformRenderer.md` |
| 角色模型与动画配置 | `../Character/Character_Guide.md` |
| 玩家生成与网络身份 | `../Multiplayer/Multiplayer_Guide.md` |
| Unity 版本与 Build Settings 配置 | `../Project/Project_Guide.md` |

## 下级导航

| 子类 | Router |
|---|---|
| 无 | 无 |

## 主要证据路径

- `Assets/Core/Scenes/InitialScene.unity`
- `Assets/Core/Scenes/PersistentScene.unity`
- `Assets/Core/Scenes/MultiplayerSampleScene.unity`
- `Assets/Core/Scenes/MultiplayerSampleScene 1.unity`
- `Assets/Core/Scenes/MinecraftClassicPreview.unity`
- `Assets/Core/Scenes/MinecraftClassicPlatform.unity`
- `Assets/Core/Scripts/Environment/VoxelPlatformRenderer.cs`
- `Assets/Core/Scripts/GamePlayFrameWork/Scripts/Scene/`
- `Assets/Core/SO/`
- `Assets/Core/Prefabs/Characters/`
- `Assets/Terrain/`
- `ProjectSettings/EditorBuildSettings.asset`

## 并发资源

- `workflow:Scene`
- `path:Assets/Core/Scenes`
- `path:Assets/Core/SO`
- `path:Assets/Core/Scripts/GamePlayFrameWork/Scripts/Scene`
- `path:Assets/Terrain`

## 能力边界

维护场景引导、常驻层与玩法层职责、场景加载与卸载链路、资源预览场景，以及单机与联机两种表现模式的切换规则。
