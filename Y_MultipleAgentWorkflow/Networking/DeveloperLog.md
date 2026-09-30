# Networking 开发记录

## 2026-09-29：建立 Mirror 网络模块权威入口

- 依据 `Assets/Core/Scripts/Networking/` 和 PersistentScene 建立模块文档。
- 当前网络实现由 Mirror 组件直接同步承载，玩家同步方向为 `ClientToServer`。
- 明确 `AutoStartClient`、`NetworkPlayerController`、`LocalPlayerCamera` 的职责和旧新构建混用风险。

## 2026-09-30：修复联机模型切换后的 Animator 失效

- `PersistentScene/Managers/NetworkCharacterManager` 按 `NetworkCharacterSync` 同步的 `characterId` 分流加载：本地拥有者使用 `CharactersForLocal`，远程拥有者使用 `CharactersForSync`，网络根对象不再持有固定角色视觉。
- 模型销毁前先清空 `PlayerCharacterController` 的旧动画驱动，模型实例化后再绑定新 `Animator`。
- Host 模式实测 `characterId 0 -> 1 -> 0`，角色持续可见，控制台无 `MissingReferenceException`。
