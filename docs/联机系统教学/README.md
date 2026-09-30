# 联机系统教学

本目录说明当前工程从场景启动、UDP socket、KCP、Mirror 连接、玩家生成、角色装配、物理移动到联机验收的完整链路。

阅读顺序：

1. [01-场景与资源配置](01-场景与资源配置.md)
2. [02-UDP与KCP传输链路](02-UDP与KCP传输链路.md)
3. [03-Mirror连接与玩家生命周期](03-Mirror连接与玩家生命周期.md)
4. [04-玩家同步与角色装配](04-玩家同步与角色装配.md)
5. [05-移动相机与物理](05-移动相机与物理.md)
6. [06-构建运行与联机验收](06-构建运行与联机验收.md)
7. [07-代码文件索引](07-代码文件索引.md)

## 从哪一篇开始

想理解场景为什么会出现单机角色、网络角色和相机，先读 `01` 与 `04`。

想理解 UDP 数据如何变成 Mirror 消息，先读 `02` 与 `03`。

想理解角色为什么遵循相机方向移动、怎样靠重力落地，先读 `05`。

想按源码逐个查找入口，直接读 `07`，再回到对应专题。

## 当前工程入口

| 内容 | 路径 |
|---|---|
| 构建入口场景 | `Assets/Core/Scenes/InitialScene.unity` |
| 常驻场景 | `Assets/Core/Scenes/PersistentScene.unity` |
| 玩法场景 | `Assets/Core/Scenes/MultiplayerSampleScene.unity` |
| Mirror 与 KCP 配置 | `Assets/Core/Prefabs/NetworkManager.prefab` |
| 网络玩家根 | `Assets/Core/Prefabs/Player_Network.prefab` |
| 本地角色表现 | `Assets/Core/Prefabs/CharactersForLocal/` |
| 远程角色表现 | `Assets/Core/Prefabs/CharactersForSync/` |
| 项目网络脚本 | `Assets/Core/Scripts/Networking/` |
| 项目移动脚本 | `Assets/Core/Scripts/Movement/` |
| Mirror KCP 源码 | `Assets/Mirror/Transports/KCP/` |
| Mirror 核心源码 | `Assets/Mirror/Core/` |
| Mirror 变换同步源码 | `Assets/Mirror/Components/NetworkTransform/` |

## 统一概念

`Player_Network` 是网络根，不是具体角色模型。

服务器创建一个网络根，客户端根据这个网络根的身份和 `characterId` 装配本地表现：

```text
Player_Network
  ├─ 本地拥有者：CharactersForLocal 中的完整角色
  │   ├─ 模型
  │   ├─ Animator
  │   ├─ Camera
  │   └─ ThirdPersonCamera
  │
  └─ 远程玩家：CharactersForSync 中的同步角色
      ├─ 模型
      └─ Animator
```

网络根负责 `NetworkIdentity`、变换同步、物理控制器、输入入口和角色编号同步。视觉角色负责模型、Animator 和本地相机资源。联机运行时只允许网络根驱动移动和碰撞，视觉子对象上的重复移动组件会被管理器停用。

工程当前权威配置见 `Y_MultipleAgentWorkflow/UnityRuntime/Mirror_KCP_Config.md`。本文档用于解释配置如何工作以及源码如何串联。
