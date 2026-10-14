# Multiplayer 知识域路由

文档 ID：`BUS-MULTIPLAYER`
状态：`Active`
维护计数：`3/5`
最后更新：`2026-10-14`

## 任务路由

| 任务 | 权威文档 |
|---|---|
| Mirror 组件、连接、自动连接、玩家网络行为 | `Multiplayer_Guide.md` |
| 客户端与服务器消息边界、同步字段和兼容规则 | `Sync_Protocol.md` |
| 专用服务器启动、Headless 构建、监听和部署 | `Dedicated_Server.md` |
| KCP 参数、NetworkManager、玩家 Prefab 与构建 | `../Project/Mirror_KCP_Config.md` |
| 客户端输入和角色移动 | `../Character/Character_Guide.md` |
| 角色模型装配与相机归属 | `../Scene/Scene_Guide.md` |
| 场景列表与 Build Settings | `../Scene/Scene_Guide.md` |

## 下级导航

| 子类 | Router |
|---|---|
| 无 | 无 |

## 主要证据路径

- `Assets/Core/Scripts/Networking/`
- `Assets/Core/Prefabs/NetworkPlayer.prefab`
- `Assets/Core/Prefabs/NetworkManager.prefab`
- `Assets/Core/Prefabs/Characters/`
- `Assets/Plugins/Mirror/`
- `Assets/Tests/PlayMode/Networking/`

## 并发资源

- `workflow:Multiplayer`
- `path:Assets/Core/Scripts/Networking`
- `path:Assets/Core/Prefabs/NetworkPlayer.prefab`
- `path:Assets/Core/Prefabs/NetworkManager.prefab`
- `path:Assets/Plugins/Mirror`

## 能力边界

维护 Mirror 连接与玩家生成、角色编号同步、变换同步边界和专用服务器运行。运动表现状态的网络同步线格式与约束记录在 `Sync_Protocol.md` 的「运动表现」一节。
