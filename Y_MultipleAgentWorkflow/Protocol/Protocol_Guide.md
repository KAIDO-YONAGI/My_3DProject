# 网络协议边界

文档 ID：`PROTOCOL-GUIDE`
状态：`Active`
最后核验：`2026-09-30`

## 协议分层

```text
UDP Datagram
  → kcp2k 通道头与连接 Cookie
  → KCP 可靠传输或非可靠直传
  → Mirror Transport 事件
  → Mirror 连接、Ready、AddPlayer、Spawn 和状态消息
  → NetworkTransformReliable 变换数据
  → NetworkCharacterSync 角色编号
```

## 传输层

- 服务器监听 UDP `7777`。
- KCP 可靠通道负责握手、确认、重传、窗口和分片。
- 非可靠通道保留 kcp2k 通道头与 Cookie，数据直接交给上层。
- KCP 客户端和服务器在 EarlyUpdate 接收数据，在 LateUpdate 刷新发送。

## Mirror 消息层

- 连接建立后，Mirror 完成认证并进入 Ready。
- `AddPlayerMessage` 请求服务器创建当前连接的玩家对象。
- Spawn 数据携带 `NetworkIdentity`、初始 Transform 和 NetworkBehaviour 初始状态。
- Entity State 数据携带 NetworkBehaviour 的增量状态。

## 项目同步字段

### 角色变换

`NetworkTransformReliable` 使用 `ClientToServer` 方向。本地拥有者序列化位置和旋转，服务器接收后广播，远程客户端通过快照缓冲插值显示。

### 角色编号

`NetworkCharacterSync.characterId` 是 `SyncVar`。服务器持有最终值，客户端 Hook 收到变化后重新装配角色表现。

本地拥有者调用 `SetLocalCharacter` 时：

```text
SetLocalCharacter
  → CommandSetCharacter
  → 服务器校验角色编号
  → 更新 characterId
  → SyncVar Hook
  → 各客户端重新装配对应表现
```

Host 和远端客户端通过同一个 `CommandSetCharacter` 入口提交角色选择。服务器校验角色编号后写入 `characterId`，表现由 SyncVar hook 驱动。客户端装配状态按实例、编号、来源 Prefab 和本地身份判断复用与刷新。

## 兼容性规则

客户端与服务器使用相同的玩家 Prefab 组件布局、NetworkBehaviour 顺序、同步字段和序列化配置。网络组件或同步字段发生变化时，同时构建并部署客户端与服务器。

## 维护触发

修改传输参数、Mirror 消息流程、Command、SyncVar、NetworkTransform、序列化字段或兼容规则时更新本文档，并同步 Networking、Server 和 UnityRuntime 文档。
