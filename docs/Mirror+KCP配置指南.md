# Mirror + KCP 快速上手配置指南

更新时间：2026-09-30

本文是当前项目的快速配置入口。完整的单机、联机和代码调用链说明见：

`D:\Unity\Projects\My_3DProject\docs\网络角色开发指南.md`

## 1. 当前项目固定结构

| 项目 | 当前配置 |
|---|---|
| 持久场景 | `Assets/Core/Scenes/PersistentScene.unity` |
| 联机场景 | `Assets/Core/Scenes/MultiplayerSampleScene.unity` |
| 网络管理器 | `Assets/Core/Prefabs/NetworkManager.prefab` |
| 玩家网络预制体 | `Assets/Core/Prefabs/Player_Network.prefab` |
| 联机角色管理器 | `PersistentScene/Managers/NetworkCharacterManager` |
| 单机玩家 | `PersistentScene/LocalPlayer` |
| 传输层 | `kcp2k.KcpTransport` |
| UDP 端口 | `7777` |
| 默认客户端地址 | `127.0.0.1` |
| 本地联机模型来源 | `CharactersForLocal` 下的本地角色 Prefab |
| 远程联机模型来源 | `CharactersForSync` 下的 `_Sync.prefab` |

## 2. 必须保持的配置

### 2.1 NetworkManager

打开 `Assets/Core/Prefabs/NetworkManager.prefab`，确认：

```text
playerPrefab  -> Assets/Core/Prefabs/Player_Network.prefab
autoCreatePlayer = true
onlineScene = 空
dontDestroyOnLoad = false
Transport = KcpTransport
```

`playerPrefab` 决定客户端加入服务器后生成哪一个网络玩家对象。不能指向单机角色预制体。

### 2.2 KcpTransport

当前项目使用 KCP 承载 UDP：

| 参数 | 当前值 | 说明 |
|---|---:|---|
| Port | `7777` | 服务端监听端口，客户端连接同一端口 |
| Dual Mode | `true` | 保持当前项目配置 |
| No Delay | `true` | 保持当前项目配置 |
| Timeout | `10000` | 连接超时毫秒数 |
| Fast Resend | `2` | 快速重传阈值 |
| Send/Receive Window | `4096` | 当前窗口配置 |
| Max Retransmit | `40` | 最大重传次数 |

调试阶段可以打开 KCP 日志；确认连接稳定后关闭详细日志，避免控制台被网络日志淹没。

### 2.3 PersistentScene 中的 NetworkCharacterManager

打开：

`PersistentScene/Managers/NetworkCharacterManager`

确认：

```text
localCharacterPrefabs[0] -> CharactersForLocal/娜娜莉（华丽飞踢）.prefab
localCharacterPrefabs[1] -> CharactersForLocal/娜娜莉（学园之星）.prefab
characterPrefabs[0] -> CharactersForSync/娜娜莉（华丽飞踢）_Sync.prefab
characterPrefabs[1] -> CharactersForSync/娜娜莉（学园之星）_Sync.prefab
defaultCharacterId   -> 0
```

这里的数组就是跨客户端共享的角色编号表：

```text
编号 0 = 数组第 0 项
编号 1 = 数组第 1 项
```

所有客户端和服务端必须保持相同数组顺序。编号只代表本地数组索引，网络上传输的是编号，不传输模型资源。

必须遵守：

- `localCharacterPrefabs[]` 只放 `CharactersForLocal`；`characterPrefabs[]` 只放 `CharactersForSync`；不要交叉放置。
- 管理器必须挂在 `PersistentScene` 的场景对象上。
- 不要运行时创建管理器。
- 不要使用 `DontDestroyOnLoad` 保存管理器。

## 3. Player_Network 预制体

打开：

`Assets/Core/Prefabs/Player_Network.prefab`

根对象应包含：

```text
NetworkIdentity
NetworkTransformReliable
CharacterController
PlayerCharacterController
NetworkCharacterSync
```

各组件作用：

- `NetworkIdentity`：让对象成为 Mirror 网络对象。
- `NetworkTransformReliable`：同步网络根对象的位置和旋转。
- `CharacterController`：提供角色碰撞和移动基础。
- `PlayerCharacterController`：只有本地拥有权对象读取输入并移动。
- `NetworkCharacterSync`：发送和接收角色编号。

不要在 `Player_Network` 根对象上放单机角色模型、单机摄像机或本地专用角色切换脚本。联机模型由 `NetworkCharacterManager` 统一加载。

## 4. 单机快速流程

单机开发只关注：

```text
PersistentScene/LocalPlayer
    -> 本地模型
    -> 本地 Animator
    -> PlayerCharacterController
    -> 本地 Camera / AudioListener
```

单机更换模型时修改 `LocalPlayer`，不要修改 `NetworkCharacterManager.characterPrefabs`。点击 Play 后先确认单机角色正常显示、摄像机正常跟随、输入可以移动。

## 5. 联机快速流程

### 服务端

1. 打开 `NetworkManager`。
2. 确认 Transport 为 `KcpTransport`。
3. 确认端口为 `7777`。
4. 启动 Server。

### 客户端

1. 地址填写 `127.0.0.1`，或填写服务端局域网 IP。
2. 端口填写 `7777`。
3. 启动 Client。
4. 检查连接日志和场景中的 `Player_Network`。

### 角色显示验收

1. 客户端加入后生成 `Player_Network`。
2. 服务端给玩家设置默认 `characterId`。
3. 客户端收到 `SyncVar characterId`。
4. `NetworkCharacterManager` 根据“本地拥有者/远程拥有者”选择 `CharactersForLocal` 或 `CharactersForSync`。
5. 本地对象使用 `CharactersForLocal` 自带的摄像机，远程对象不生成摄像机和 AudioListener。
6. 两个客户端应看到彼此的角色模型。

## 6. 从 UDP 方案迁移后的配置对应关系

当前方案不是另起一套自定义 UDP 协议，而是把原来 UDP 方案中的职责交给 Mirror API：

| 原 UDP 配置 | 当前 Mirror/KCP 配置 |
|---|---|
| UDP 连接和监听 | `NetworkManager` + `KcpTransport` |
| UDP 端口 | `KcpTransport.port = 7777` |
| 角色编号数据包 | `NetworkCharacterSync.CmdSetCharacter(int)` |
| 服务端记录角色编号 | `NetworkCharacterSync.characterId` |
| 广播角色编号 | `[SyncVar]` 自动同步 |
| 客户端收包回调 | `OnCharacterIdChanged` |
| 远程按编号加载模型 | `NetworkCharacterManager.ApplyCharacter` |
| 自定义位置包 | `NetworkTransformReliable` |

实际角色切换链路：

```text
SetLocalCharacter(id)
    -> CmdSetCharacter(id)
    -> 服务器验证编号
    -> 写入 characterId
    -> SyncVar 同步
    -> OnCharacterIdChanged
    -> NetworkCharacterManager.ApplyCharacter
    -> localCharacterPrefabs[id]（本地拥有者）
    -> characterPrefabs[id]（远程拥有者）
```

项目中没有独立 UDP Socket，也没有当前角色编号专用的 `NetworkMessage`。KCP 使用 UDP 传输，但业务层由 Mirror 管理连接、对象、状态同步和调用权限。

## 7. 构建前检查

构建服务端和客户端前，逐项确认：

- `NetworkManager.playerPrefab` 指向 `Player_Network.prefab`。
- `Player_Network` 具备全部五个根组件。
- `PersistentScene` 在构建场景列表中。
- `MultiplayerSampleScene` 在构建场景列表中。
- `NetworkCharacterManager` 位于 `PersistentScene`，不是运行时生成对象。
- `localCharacterPrefabs[]` 中全部是 `CharactersForLocal` 本地预制体，`characterPrefabs[]` 中全部是 `CharactersForSync/*_Sync.prefab`。
- 每个客户端的角色数组顺序一致。
- 服务端和客户端使用同一个端口 `7777`。
- 客户端地址不是错误的旧地址。
- 服务端专用构建不依赖本地摄像机。

## 8. 常见问题

| 问题 | 检查方法 |
|---|---|
| 客户端加入但没有角色 | 检查 `NetworkManager.playerPrefab` 是否指向 `Player_Network` |
| 角色根对象存在但没有模型 | 检查 `NetworkCharacterManager` 是否在 `PersistentScene`，以及数组是否有对应编号 |
| 远程显示本地模型 | 从 `characterPrefabs[]` 移除本地模型，只保留 `CharactersForSync/*_Sync.prefab` |
| 角色不能动 | 检查 `CharacterController`、`PlayerCharacterController` 和本地拥有权 |
| 两端显示不同模型 | 检查两端 `characterPrefabs[]` 的数组顺序 |
| 相机出现多个 | 检查 `NetworkCharacterManager` 是否只给本地角色启用相机和 AudioListener |
| 端口连接失败 | 检查服务端和客户端是否都是 `7777`，以及防火墙和局域网地址 |
| Play 后单机角色消失 | 检查 `PersistentScene/LocalPlayer`，不要用联机流程替代单机对象 |

## 9. 最小成功标准

配置完成后应满足：

```text
单机 Play：
LocalPlayer 可见、可移动、摄像机正常。

联机：
服务端启动成功；
两个客户端都能连接；
双方都生成 Player_Network；
本地玩家使用 CharactersForLocal 模型，远程角色使用 CharactersForSync 模型；
角色编号变化后双方显示一致；
本地和远程摄像机不会同时启用。
```
