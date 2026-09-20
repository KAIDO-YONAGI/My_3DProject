# Unity 运行时、场景与资产

文档 ID：`UNITY-GUIDE`
状态：`Active`
最后核验：`2026-09-20`

## 版本与依赖

- Unity 编辑器版本：`2022.3.62f3c1`。
- Addressables：`1.22.3`。
- Unity MCP：从 GitHub `main` 分支引用，不是固定提交。
- 其他显式包包括 Terrain Tools `5.0.6`、TextMeshPro `3.0.7`、Timeline `1.7.7`、UGUI `1.0.0`、Visual Scripting `1.9.4`。
- Manifest 未显式声明第三方网络框架。

## 构建与场景

- `ProjectSettings/EditorBuildSettings.asset` 的场景列表为空，当前没有可确认的 Player 构建首场景。
- `Assets/Core/Scenes/Main.unity` 是空场景。
- `MultiplayerSampleScene.unity` 的根节点为 `Defaults`、`Managers`、`Character`。

## MultiplayerSampleScene

- `Defaults` 包含方向光、EventSystem、禁用的场景相机和地形。
- `Managers` 包含玩家位置管理对象和 `NetManager`。
- 玩家管理器的模型列表引用两个 `CharactersForSync` Prefab。
- `Character` 下有两个本地角色实例：“学园之星”禁用，“华丽飞踢”启用。
- 启用角色实例把初始化数据覆盖为 `modelID=1`、`health=10`、`damage=2`。

## Prefab 角色分工

- 本地角色 Prefab 启用角色相机、AudioListener、CharacterController、移动组件和网络同步组件。
- 两个本地角色的网络同步组件都引用同一个 `BoolEventChannel.asset`，重连延迟为 3 秒。
- `CharactersForSync` 下的远端角色 Prefab 禁用 CharacterController 和本地移动组件，符合远端表现模型用途。
- 本地角色 Prefab 与同步 Prefab 分别直接派生自相同底层角色模型，不是彼此派生。

## 事件资产

- `BoolEventChannel.asset` 正确绑定 `BoolEventChannelSO`，用于发布布尔连接结果。
- `Assets/Core/Scripts/Events/Assets/ConnectResultChannel.asset` 的脚本 GUID 为全零，应视为丢失脚本或不可解析资产。
- 当前场景和本地角色实际引用的是有效的 `BoolEventChannel.asset`，不是上述损坏资产。

## 已确认风险

- Build Settings 没有场景，无法确认构建后的启动入口。
- 场景相机禁用，视角依赖启用角色 Prefab 内的相机。
- NavMesh 配置没有绑定烘焙后的 `NavMeshData`。
- Unity MCP 跟踪远端 `main`，依赖解析结果可能随上游变化。
- 当前没有经过用户确认的 Unity 构建、测试或批处理命令。

## 维护触发

修改 Unity 版本、包版本、Build Settings、场景层级、Prefab 绑定或事件资产时更新本文档。
