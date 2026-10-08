# Client 知识域路由

文档 ID：`BUS-CLIENT`
状态：`Active`
维护计数：`0/5`
最后更新：`2026-10-08`

| 任务 | 必读文档 |
|---|---|
| 客户端启动、输入、移动、动画、本地相机 | `Client_Guide.md` |
| 角色运动算法和输入数据流 | `Client_Guide.md`，并读取 `Assets/Core/Scripts/Character/` |
| 输入资产、动作表、相机朝向参数与光标规则 | `Client_Guide.md`，并读取 `Assets/Core/Input/` |
| 相机轨道状态、世界姿态、最终候选避让与转向积分 | `Client_Guide.md`，并读取 `Assets/Core/Scripts/Camera/Runtime/` 与相机回归测试 |
| Mirror 本地玩家归属、连接和玩家网络行为 | `../Networking/Networking_Guide.md` |
| 场景、Prefab、Build Settings | `../UnityRuntime/UnityRuntime_Guide.md` |

## 主要证据路径

- `Assets/Core/Scripts/Character/`
- `Assets/Core/Scripts/Camera/`
- `Assets/Core/Input/`
- `Assets/Core/Tests/PlayMode/Camera/`
- `Assets/Core/Prefabs/CharactersForSync/`
- `Assets/Core/Scenes/MultiplayerSampleScene.unity`

## 并发资源

- `workflow:Client`
- `path:Assets/Core/Scripts/Character`
- `path:Assets/Core/Scripts/Camera`
- `path:Assets/Plugins/DynamicBone`
- `path:Assets/Core/Input`
- `path:Assets/Core/Prefabs/CharactersForSync`
