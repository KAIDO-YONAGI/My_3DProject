# UnityRuntime 开发记录

## 2026-09-20：建立 Unity 运行时权威指南

- 证据：Unity 版本、Packages、Build Settings、场景、Prefab 和事件资产。
- 记录了场景层级、本地与远端角色 Prefab 分工、连接事件资产和构建场景状态。
- 标记了空 Build Settings、损坏事件资产、禁用场景相机和未固定 Unity MCP 提交等风险。
- 只读分析和纯文档维护不增加维护计数，当前为 `0/5`。

## 2026-09-29：清理网络退役后的场景与资产

- 证据：`MultiplayerSampleScene.unity`、两个角色 Prefab、`Assets/Core/Scripts/` 目录状态（提交 `5c2b019`）。
- 场景删除 `NetManager`、`PlayerPositionManager` 节点；`Managers` 现为空节点。
- 两个角色 Prefab 剥离 `SyncCharacter` 组件引用；相机、CharacterController 等单机组件保留作为原型基底。
- 删除全零 GUID 孤立损坏资产 `ConnectResultChannel.asset`；`BoolEventChannel.asset` 无引用但保留备用。
- 核验发现磁盘上 `Assets/Mirror` 不存在（仅 csproj 与 ScriptTemplates 残留），阶段一需重新导入。
- `UnityRuntime_Guide.md` 已同步更新；本次为业务实现实质变更，UnityRuntime 域维护计数 `1/5`。
