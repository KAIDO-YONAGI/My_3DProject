# Networking 开发记录

## 2026-09-29：建立 Mirror 网络模块权威入口

- 依据 `Assets/Core/Scripts/Networking/` 和 PersistentScene 建立模块文档。
- 当前网络实现由 Mirror 组件直接同步承载，玩家同步方向为 `ClientToServer`。
- 明确 `AutoStartClient`、`NetworkPlayerController`、`LocalPlayerCamera` 的职责和旧新构建混用风险。

