# UnityRuntime 知识域路由

文档 ID：`BUS-UNITYRUNTIME`
状态：`Active`
维护计数：`1/5`
最后更新：`2026-09-29`

## 任务路由

| 触发词 | 权威文档 |
|---|---|
| Unity 版本、Packages、Build Settings | `UnityRuntime_Guide.md` |
| 场景层级、启动组件、相机和地形 | `UnityRuntime_Guide.md` |
| Prefab、远端同步模型、事件资产 | `UnityRuntime_Guide.md` |
| 网络脚本运行行为 | `../Client/Client_Guide.md` 与 `../Networking/Networking_Guide.md` |

## 主要证据路径

- `ProjectSettings/`
- `Packages/manifest.json`
- `Assets/Core/Scenes/`
- `Assets/Core/Prefabs/`
- `Assets/Core/EventSOs/`

## 并发资源

- `workflow:UnityRuntime`
- `path:ProjectSettings`
- `path:Packages/manifest.json`
- `path:Assets/Core/Scenes`
- `path:Assets/Core/Prefabs`
- `path:Assets/Core/EventSOs`

## 能力边界

本域负责 Unity 序列化状态和项目配置。脚本内部业务逻辑仍路由到对应代码知识域。
