# Project 知识域路由

文档 ID：`BUS-PROJECT`
状态：`Active`
维护计数：`4/5`
最后更新：`2026-10-10`

## 任务路由

| 任务 | 权威文档 |
|---|---|
| Unity 版本、Packages、资产目录布局 | `Project_Guide.md` |
| Addressables 配置与构建脚本 | `Project_Guide.md` |
| Player Settings、输入系统开关与旧输入 API 边界 | `Project_Guide.md` |
| Prefab 组件组成与序列化配置值 | `Project_Guide.md` |
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
- `Packages/manifest.json`
- `Assets/Plugins/Mirror/version.txt`
- `Assets/Plugins/AddressableAssetsData/`
- `Assets/Core/Prefabs/NetworkPlayer.prefab`
- `Assets/Core/Prefabs/NetworkManager.prefab`
- `Assets/Materials/`
- `D:/Unity/Releases/3D_MultiplayerGame/`

## 并发资源

- `workflow:Project`
- `path:ProjectSettings`
- `path:Packages/manifest.json`
- `path:Assets/Plugins/AddressableAssetsData`
- `path:Assets/Materials`
- `path:D:/Unity/Releases/3D_MultiplayerGame`

## 能力边界

维护 Unity 工程配置、包依赖、Addressables、Prefab 序列化状态、资产目录布局和构建产物路径。场景内容归 `../Scene/Scene_Guide.md`，联机参数归 `Mirror_KCP_Config.md`。
