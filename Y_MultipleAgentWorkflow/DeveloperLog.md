# 根工作流开发记录

跨知识域结构变更和根索引维护记录在此；业务证据记录到对应知识域的 `DeveloperLog.md`。

## 2026-10-20：拆解 GamePlayFrameWork 并按功能重组 Core 目录

- 变动性质：跨功能域，涉及 Project 资产目录布局与 Scene 场景框架路径。
- 目录变化：`Assets/Core/Scripts/GamePlayFrameWork/` 拆解进 `Assets/Core/Scripts/` 的 `Audio/`、`Common/`、`Scene/`、`SO/`、`UI/`，原 `Scripts/Core/` 更名 `Common/`。事件通道资产移入 `Assets/Core/SO/Events/`，`SampleInput.inputactions` 与 `UI_ESCPress.asset` 移入 `Assets/Core/Input/`，`Teleport.prefab` 移入 `Assets/Core/Prefabs/`。`Samples/` 下的三个模板场景与三个模板场景数据 SO 已删除，框架说明更新为 `Assets/Core/Scripts/README.md`。
- 命名空间：`Core.Scripts.GamePlayFrameWork.Scripts.*` 改为 `Core.Scripts.*`，涉及框架内 26 个脚本与框架外 6 个引用文件，`AudioPlayerTests.cs` 的反射命名空间常量同步更新。
- 验证：38 个搬运资产的 `.meta` GUID 保持不变，消失的 15 个 GUID 全部属于已删除的 6 个模板资产与 9 个空目录；`Assets/` 与 `Y_MultipleAgentWorkflow/` 下 `GamePlayFrameWork` 零命中；`Assembly-CSharp.dll` 与 `Core.EditorTests.dll` 包含全部搬移后类型，两个程序集均不含旧命名空间字符串；`Assets/Core/Scenes/PersistentScene.unity` 的三个事件资产引用与 `UI_ESCPress.asset` 的输入资产引用均可解析；改写文件无编码损坏。
- 维护计数：`Project/Router.md` 递增为 `4/5`；`Scene/Router.md` 达到 `5/5`，复核 `Scene_Guide.md` 后重置为 `0/5`。

## 2026-10-11：工程迁移 URP 并实装三渲二着色器

- 变动性质：跨功能域，涉及工程依赖与渲染管线、角色渲染、场景光照与地形。
- 分支与提交：管线迁移在 `feature/urp-toon-migration` 上进行，提交 `19ae354`，随后以 `--no-ff` 合入 `main`，合并提交 `21b1020`。此前未提交的角色运动同步改动单独提交为 `b171cf0`，与迁移分离。
- 结论摘要：`Packages/manifest.json` 加入 URP `14.0.12`；新增 `ToonLit.shader` 与 5 个 HLSL 头文件；新增三个编辑器迁移工具；编译隔离由 `Assets/ToonEditor/Core.Toon.Editor.asmdef` 的 `versionDefines` 承担。
- 记录位置：技术细节写在 `Assets/Core/Shaders/Toon/TOON_MIGRATION.md`，与着色器一起版本控制。

## 2026-10-10：清理运动状态同步遗留残余

- 资产：`NetworkPlayer.prefab` 中 fileID `7814609285416083721`、脚本 GUID `0b9dca0ce9ed4d9094a8df5c8901df22` 的 Missing Script 记录及其 `m_Component` 数组引用已删除。孤儿资产 `Assets/Core/SO/EventSOs/BoolEventChannel.asset` 及其 `.meta`、变空的 `EventSOs/` 目录已删除，其脚本 `BoolEventChannelSO.cs` 在提交 `a148055` 中删除。
- 代码：`NetworkCharacterSync.cs` 中的半截注释残骸已删除；`PlayerCharacterController.cs` 中指向不存在组件的注释与前瞻表述已修正。
- 顺带核实：`MultiplayerSampleScene 1.unity` 由提交 `c5024fc` 注册进 Build Settings，与 `MultiplayerSampleScene` 互为往返传送的两张玩法图，两个场景的 `Teleport` 触发器交叉指向对方的 `GameSceneSO`，各 582 行，仅差该触发器对象。
- 验证：`Assets/Core` 脚本引用 213 处，缺失脚本 GUID 归零；`NetworkPlayer.prefab` 内部 7 个锚点的引用全部可解析；已删资产 GUID 全工程引用数为 0。

## 2026-10-08：脚本按知识域重组后的路径同步

- 依据提交 `7c5c8a5`、`21100d9` 的实际目录，同步根 `Project_Overview.md` 与各知识域 Router 中的路径引用。
- 目录变化：`Assets/Core/Scripts/Movement/Runtime/` 的相机脚本为 `Assets/Core/Scripts/Camera/Runtime/`；`Assets/Core/Scripts/Movement/` 为 `Assets/Core/Scripts/Character/`，保留 `Config/`、`Runtime/`；`Assets/Core/Scripts/Movement/DynamicBone/` 为 `Assets/Plugins/DynamicBone/`；`Assets/Core/Tests/PlayMode/Movement/` 为 `Assets/Core/Tests/PlayMode/Camera/`，程序集 `Core.Movement.PlayModeTests` 为 `Core.Camera.PlayModeTests`；联机用例位于 `Assets/Core/Tests/PlayMode/Networking/`。
- 第三方插件汇总在 `Assets/Plugins/`：`Mirror`、`ParrelSync`、`TextMesh Pro`、`AddressableAssetsData`、`DynamicBone`。
- 纯目录整理与文档维护，不改变业务实现。

## 2026-10-08：按整理后的目录更新路径引用

- 依据提交 `278c411 整理文件夹` 的实际目录，同步根路由、项目总览与各知识域文档中的路径引用。
- `Assets/FrameWork/` 为 `Assets/Core/My_FrameWork/`；第三方插件统一在 `Assets/Plugins/`；`Assets/forest/` 为 `Assets/Materials/ForestMaterials/`。
- 纯文档维护。

## 2026-09-29：按代码职责恢复模块化知识域

- 权威知识域为 `Workflow`、`Character`、`Multiplayer`、`Scene`、`Project`。
- `Character` 对应运动、玩家输入、动画和相机；`Multiplayer` 对应 Mirror 连接与玩家网络组件。

## 2026-09-29：重建权威文档

- 根据当前代码、场景、Prefab、构建结果和运行验证重建活动文档。
- 根路由、项目总览和知识域文档指向 `PersistentScene`、`MultiplayerSampleScene` 与当前角色 Prefab。
- `ProjectValidationMode=None`。

## 2026-09-29：知识域收敛

- 权威文档收敛为 `Project_Overview.md`、`Character/`、`Multiplayer/`、`Scene/`、`Project/` 与 `Workflow/`。
- Mirror 96.11.2 安装进工程，联机链路验证通过，配置记录在 `Project/Mirror_KCP_Config.md`。

## 2026-09-20：建立中文权威文档库

- 创建根路由、工作流指南、WorkingAgent 注册表和知识域。
- `ProjectValidationMode=None`。
