# UnityRuntime 知识域路由

文档 ID：`BUS-UNITYRUNTIME`
状态：`Active`
维护计数：`3/5`
最后更新：`2026-09-29`

## 任务路由

| 触发词 | 必读文档 |
|---|---|
| Unity 版本、Packages、Build Settings | `UnityRuntime_Guide.md` |
| PersistentScene、MultiplayerSampleScene、相机、地形 | `UnityRuntime_Guide.md` |
| Prefab、移动组件、事件资产 | `UnityRuntime_Guide.md` |
| Mirror、KCP、NetworkManager、玩家 Prefab、同步方向 | `Mirror_KCP_Config.md` |
| AutoStartClient、专用服务器、连接地址、构建和联调 | `Mirror_KCP_Config.md` |

## 主要证据路径

- `ProjectSettings/ProjectVersion.txt`
- `ProjectSettings/EditorBuildSettings.asset`
- `Packages/manifest.json`
- `Assets/Core/Scenes/PersistentScene.unity`
- `Assets/Core/Scenes/MultiplayerSampleScene.unity`
- `Assets/Core/Prefabs/CharactersForSync/`
- `Assets/Core/Scripts/Networking/`
- `Assets/Core/Scripts/Movement/Runtime/`
- `Assets/Core/EventSOs/`
- `Assets/Mirror/`
- `D:/Unity/Releases/3D_MultiplayerGame/`

## 并发资源

- `workflow:UnityRuntime`
- `path:ProjectSettings`
- `path:Packages/manifest.json`
- `path:Assets/Core/Scenes`
- `path:Assets/Core/Prefabs/CharactersForSync`
- `path:Assets/Core/Scripts/Networking`
- `path:Assets/Core/Scripts/Movement/Runtime`
- `path:Assets/Core/EventSOs`

## 能力边界

本域负责 Unity 序列化状态、项目配置、Mirror 工程配置和已完成的运行验证。玩法方案与未来同步模型设计可以读取 `docs/plan/`，但只有实际落地并验证后才能写成当前事实。
