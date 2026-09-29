# UnityRuntime 知识域路由

文档 ID：`BUS-UNITYRUNTIME`
状态：`Active`
维护计数：`3/5`
最后更新：`2026-09-29`

## 任务路由

| 触发词 | 权威文档 |
|---|---|
| Unity 版本、Packages、Build Settings | `UnityRuntime_Guide.md` |
| 场景层级、启动组件、相机和地形 | `UnityRuntime_Guide.md` |
| Prefab、远端同步模型、事件资产 | `UnityRuntime_Guide.md` |
| Mirror 安装、KCP 参数、NetworkManager/玩家 Prefab 配置 | `Mirror_KCP_Config.md` |
| 服务器构建、启动命令、客户端连接地址 | `Mirror_KCP_Config.md` |

## 主要证据路径

- `ProjectSettings/`
- `Packages/manifest.json`
- `Assets/Core/Scenes/`
- `Assets/Core/Prefabs/`
- `Assets/Core/EventSOs/`
- `Assets/Core/Scripts/Networking/`
- `Assets/Mirror/`

## 并发资源

- `workflow:UnityRuntime`
- `path:ProjectSettings`
- `path:Packages/manifest.json`
- `path:Assets/Core/Scenes`
- `path:Assets/Core/Prefabs`
- `path:Assets/Core/EventSOs`
- `path:Assets/Core/Scripts`

## 能力边界

本域负责 Unity 序列化状态、项目配置和 Mirror 工程配置。玩法计划与同步模型设计路由到 `docs/plan/`。
