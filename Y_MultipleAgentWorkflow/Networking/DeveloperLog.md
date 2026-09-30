# Networking 开发记录

## 2026-09-29：建立 Mirror 网络模块权威入口

- 依据 `Assets/Core/Scripts/Networking/` 和 PersistentScene 建立模块文档。
- 当前网络实现由 Mirror 组件直接同步承载，玩家同步方向为 `ClientToServer`。
- 明确 `AutoStartClient`、`NetworkPlayerController`、`LocalPlayerCamera` 的职责和旧新构建混用风险。

## 2026-09-30：修复联机模型切换后的 Animator 失效

- `PersistentScene/Managers/NetworkCharacterManager` 按 `NetworkCharacterSync` 同步的 `characterId` 分流加载：本地拥有者使用 `CharactersForLocal`，远程拥有者使用 `CharactersForSync`，网络根对象不再持有固定角色视觉。
- 模型销毁前先清空 `PlayerCharacterController` 的旧动画驱动，模型实例化后再绑定新 `Animator`。
- Host 模式实测 `characterId 0 -> 1 -> 0`，角色持续可见，控制台无 `MissingReferenceException`。

## 2026-09-30：双编辑器联机与后加载相机修复

- 通过重建后的 `Server_12_7` 专用服务器验证两个 Unity 编辑器同时连接，KCP 7777 握手和玩家生成均成功。
- 修正本地角色 Prefab 根引用和出生点高度，避免网络角色从地形下方生成后持续下落。
- `NetworkCharacterManager` 增加 `SceneManager.sceneLoaded` 处理，解决 Additive 玩法场景的非网络相机在联机状态下仍保持启用的问题。

## 2026-09-30：连接状态与单机入口分流修复

- 联机表现不再读取连接尝试阶段也会变化的 `NetworkClient.active`，改为只在 `NetworkClient.isConnected=true` 时启用。
- 已连接后禁用 PersistentScene 的场景单机角色，消除两个网络玩家之外的第三个角色。
- 未连接和重试阶段保留单机角色、输入、相机及物理更新，避免 `No cameras rendering` 和角色重力暂停。
