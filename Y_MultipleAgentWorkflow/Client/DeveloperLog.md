# Client 开发记录

## 2026-09-29：建立客户端模块权威入口

- 依据 `Assets/Core/Scripts/Movement/` 实际代码建立客户端模块。
- 记录输入、运动、动画和相机职责；未把未来固定 Tick 设计写成当前能力。
- 记录联机时通过可选 `NetworkIdentity` 限制本地玩家输入，保持 `PlayerCharacterController` 为普通 `MonoBehaviour`。

