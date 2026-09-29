# Unity 运行时、场景与资产

文档 ID：`UNITY-GUIDE`
状态：`Active`
最后核验：`2026-09-29`

## 版本与依赖

- Unity 编辑器版本：`2022.3.62f3c1`。
- Addressables：`1.22.3`。
- Unity MCP：从 GitHub `main` 分支引用，不是固定提交。
- 其他显式包包括 Terrain Tools `5.0.6`、TextMeshPro `3.0.7`、Timeline `1.7.7`、UGUI `1.0.0`、Visual Scripting `1.9.4`。
- Mirror：计划采用本地插件方式（`.gitignore` 忽略 `/Assets/Mirror/` 与 `/Assets/Plugins/`，不入库）。**2026-09-29 核验时磁盘上 `Assets/Mirror` 不存在**，仅有历史 csproj、`ScriptTemplates/` 下的 Mirror 项模板和 `MirrorExamplesPipelineConverted.txt`（2026-09-15 转换记录）残留，说明曾导入过后被移除；阶段一接入时需重新导入。仓库中当前没有可编译的 Mirror 程序集。

## 构建与场景

- `ProjectSettings/EditorBuildSettings.asset` 的场景列表为空，当前没有可确认的 Player 构建首场景。
- `Assets/Core/Scenes/Main.unity` 是空场景。
- `MultiplayerSampleScene.unity` 根节点为 `Defaults`、`Managers`、`Character`、`TerrainGroup_0`（地形）。

## MultiplayerSampleScene（2026-09-29 退役后状态）

- `Defaults` 包含方向光、EventSystem、禁用的场景相机和地形引用。
- `Managers` 现为**空节点**：原 `NetManager`、`PlayerPositionManager` 两个子节点已随自研网络栈退役删除。
- `Character` 下有两个本地角色实例："学园之星"禁用，"华丽飞踢"启用；启用实例的初始化数据残留字段（`modelID=1`、`health=10`、`damage=2`）属于已删除组件的序列化痕迹，已随组件剥离清理。
- 本场景保留作为派对游戏原型场景基底（地形、光照、角色 Prefab 引用可复用）。

## Prefab 角色分工

- 两个本地角色 Prefab（"学园之星""华丽飞踢"）的 `SyncCharacter` 组件引用已剥离（2026-09-29）；角色相机、AudioListener、CharacterController、移动组件保留。
- Prefab 中的角色模型层级、动画组件未改动。
- 远端表现 Prefab（`CharactersForSync`）仍禁用 CharacterController 和本地移动组件，可继续用作远端表现模型。

## 事件资产

- `Assets/Core/EventSOs/BoolEventChannel.asset` 绑定 `BoolEventChannelSO`（`Assets/Core/Scripts/Events/BoolEventChannelSO.cs`，仓库中仅剩的玩法脚本），当前无场景或 Prefab 引用，作为通用事件通道保留备用。
- 原 `Assets/Core/Scripts/Events/Assets/ConnectResultChannel.asset`（脚本 GUID 全零的损坏资产，无任何引用）已于 2026-09-29 一并删除。

## 脚本目录现状

- `Assets/Core/Scripts/`：仅剩 `Events/`。
- `Assets/Core/Scripts/Client/` 目录及其 `Net/` 子目录已删除。
- `LocalServer/` 独立服务端工程已删除。

## 已确认风险

- Build Settings 没有场景，无法确认构建后的启动入口。
- 场景相机禁用，视角依赖启用角色 Prefab 内的相机。
- NavMesh 配置没有绑定烘焙后的 `NavMeshData`。
- Unity MCP 跟踪远端 `main`，依赖解析结果可能随上游变化。
- Mirror 未在磁盘上，阶段一第一步是重新导入并验证编译。
- 当前没有经过用户确认的 Unity 构建、测试或批处理命令。

## 维护触发

修改 Unity 版本、包版本、Build Settings、场景层级、Prefab 绑定或事件资产时更新本文档。
