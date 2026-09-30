# Mirror 网络运行时

文档 ID：`NETWORKING-GUIDE`
状态：`Active`
最后核验：`2026-09-29`

## 当前组件

- `AutoStartClient`：编辑器和普通客户端连接 `127.0.0.1`，断线后每 3 秒重试；批处理、Server active 或 Client active 时不重复连接。
- `NetworkPlayerController`：当前工程保留的最小联机玩家控制器，输入只由 `isLocalPlayer` 实例采集；未来计划由 `InputFrame` 替换。
- `LocalPlayerCamera`：远端玩家禁用相机和 AudioListener，本地玩家保留相机。
- 当前玩家 Prefab 使用 `NetworkIdentity`、`NetworkTransformReliable`、`CharacterController`、`PlayerCharacterController` 和 `ThirdPersonCamera`。

## 当前同步边界

`NetworkTransformReliable.SyncDirection=ClientToServer`。本地玩家的移动结果上传服务端，再由 Mirror 广播；当前没有自定义 NetworkMessage、Command/Rpc 协议层。

当前网络组件和通道事实以 `../UnityRuntime/Mirror_KCP_Config.md` 为准。

## 已验证

- `PersistentScene` 可连接到 `MultiplayerSampleScene`。
- 玩家生成后具有本地所有权，移动没有在等待 2 秒后被拉回。
- 混用新旧玩家组件布局曾导致 `OnDeserialize` / `EndOfStreamException`，客户端与服务端必须使用同一组件布局构建。

## 维护触发

修改 `Assets/Core/Scripts/Networking/`、Mirror 组件列表、同步方向、连接重试策略或玩家网络归属时更新本文档。

