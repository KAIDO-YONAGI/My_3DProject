# 根工作流开发记录

跨知识域结构变更和根索引维护记录在此；业务证据记录到对应知识域的 `DeveloperLog.md`。

## 2026-10-11：工程迁移 URP 并实装三渲二着色器

- 变动性质：跨功能域。涉及工程依赖与渲染管线（Project）、角色渲染（Character）、场景光照与地形（Scene）。
- 用户决策：迁移路线选「全量迁 URP」，作用范围选「角色 + 场景植被全量三渲二」，草地保留风动画。用户同时授权代为提交此前工作区里未完成的网络同步改动。
- 分支与提交：管线迁移在 `feature/urp-toon-migration` 上进行（提交 `19ae354`），随后以 `--no-ff` 合入 `main`（合并提交 `21b1020`）。此前未提交的角色运动同步改动单独提交为 `b171cf0`，与迁移分离，便于各自回退。
- 结论摘要：`Packages/manifest.json` 加入 URP `14.0.12`，Unity 已实际解析并下载成功；新增 `ToonLit.shader` 与 5 个 HLSL 头文件（源自 `UnityURPToonLitShaderExample`，MIT）；新增三个编辑器迁移工具；新增工程根 `csc.rsp` 用于编译隔离。
- 记录位置：**技术细节写在 `Assets/Core/Shaders/Toon/TOON_MIGRATION.md`，与着色器一起版本控制**，便于随资产迁移。本次只在知识域 `DeveloperLog.md` 保留结论、依据与未完成项，避免同一套步骤在根日志与知识域日志里维护两份。
- 未完成：新写的着色器与编辑器脚本尚未在 Unity 中编译与渲染验证，管线资产也尚未创建与绑定。需在编辑器中执行四个菜单步骤后确认，详见 `../Assets/Core/Shaders/Toon/TOON_MIGRATION.md` 与 `Project/DeveloperLog.md` 的同日条目。
- 各知识域 `DeveloperLog.md` 因成果尚未验证通过，均未增加维护计数。

## 2026-10-10：清理运动状态同步遗留残余

- 背景：运动状态同步按用户要求暂缓后，代码、资产与文档中残留了指向未实现组件的记录。本轮按用户明确要求一次性清除。开工前工作区为干净状态（`HEAD` 为 `0a6afc2`），原计划文档所述“基于未提交改动继续”的前提不成立。
- 资产：删除 `NetworkPlayer.prefab` 中 fileID `7814609285416083721`、脚本 GUID `0b9dca0ce9ed4d9094a8df5c8901df22` 的 Missing Script 记录，并同步移除其在 `m_Component` 数组中的引用（只删组件块会留下悬空引用）。删除孤儿资产 `Assets/Core/SO/EventSOs/BoolEventChannel.asset`：其脚本 `BoolEventChannelSO.cs` 已于提交 `a148055` 删除，该资产 GUID 在全工程零引用，`AddressableAssetsData` 也未引用该路径；连带删除其 `.meta` 与随之变空的 `EventSOs/` 目录。
- 代码：删除 `NetworkCharacterSync.cs` 第 26-30 行半截注释残骸（`// [SyncVar(hook = nameof(OnCharacterMovementStateChanged))]`、`// private ` 等，语法本身不完整）；修正 `PlayerCharacterController.cs` 中指向不存在组件的注释与“首个快照尚未到达”的前瞻表述。
- 文档：同步 `Project_Overview.md`、`Networking/Networking_Guide.md`、`Protocol/Protocol_Guide.md`、`UnityRuntime/UnityRuntime_Guide.md`、`UnityRuntime/Router.md`；`docs/网络同步速查手册.md` 第三节新增勘误，撤回把 `NetworkCharacterMovementSync` 写成已存在组件的表格行与段落。
- 顺带核实：`MultiplayerSampleScene 1.unity` **不是**误复制残留。它由提交 `c5024fc` 显式注册进 Build Settings，与 `MultiplayerSampleScene` 互为往返传送的两张玩法图，两个场景的 `Teleport` 触发器交叉指向对方的 `GameSceneSO`（各 582 行，仅差该触发器对象）。已补入 `UnityRuntime_Guide.md`。
- 验证：`Assets/Core` 脚本引用 213 处，缺失脚本 GUID 由 2 种归零；`NetworkPlayer.prefab` 内部 7 个锚点的引用全部可解析，无悬空；已删资产 GUID 全工程引用数为 0。Unity 编辑器未运行（仅 Unity Hub 在运行），未执行编译、PlayMode 或联机验收。
- 本次为资产与注释清理，不改变业务实现；跨域变更按维护规则记入各知识域 `DeveloperLog.md`。

## 2026-10-08：脚本按知识域重组后的路径同步

- 依据提交 `7c5c8a5`、`21100d9` 的实际目录，同步根 `Project_Overview.md`、`Client` 与 `UnityRuntime` 的 `Router.md`、`Client_Guide.md`、`README.md` 中的路径引用。
- 目录变化：`Assets/Core/Scripts/Movement/Runtime/` 的相机脚本现为 `Assets/Core/Scripts/Camera/Runtime/`；`Assets/Core/Scripts/Movement/` 现为 `Assets/Core/Scripts/Character/`（保留 `Config/`、`Runtime/`）；`Assets/Core/Scripts/Movement/DynamicBone/` 现为 `Assets/Plugins/DynamicBone/`；`Assets/Core/Tests/PlayMode/Movement/` 现为 `Assets/Core/Tests/PlayMode/Camera/`，程序集 `Core.Movement.PlayModeTests` 现为 `Core.Camera.PlayModeTests`；联机用例移入 `Assets/Core/Tests/PlayMode/Networking/`，程序集名不变。
- 第三方插件汇总在 `Assets/Plugins/`（`Mirror`、`ParrelSync`、`TextMesh Pro`、`AddressableAssetsData`、`DynamicBone`），当前均已纳入版本控制。
- 既有 `DeveloperLog` 条目中的旧路径继续保留原文，作为当时状态的证据，不做改写；新旧映射以本条与 `Client/DeveloperLog.md` 的对应条目为准。
- 纯目录整理与文档维护，不改变业务实现，不增加各知识域维护计数（`Client` 保持 `0/5`）。

## 2026-10-08：按整理后的目录更新路径引用

- 依据提交 `278c411 整理文件夹` 的实际目录，同步根路由、项目总览、UnityRuntime、Client、Networking、Protocol 文档中的路径引用。
- `Assets/FrameWork/` 现为 `Assets/Core/My_FrameWork/`；第三方插件统一在 `Assets/Plugins/`（`Mirror`、`ParrelSync`、`TextMesh Pro`、`AddressableAssetsData`，由 `.gitignore` 忽略）；`Assets/forest/` 现为 `Assets/Materials/ForestMaterials/`。
- 既有 DeveloperLog 条目中的旧路径保留原文，作为当时状态的证据，不做改写。
- 纯文档维护，不增加各知识域维护计数。

## 2026-09-29：按代码职责恢复模块化知识域

- 修正更早的 `重建权威文档` 条中“仅保留 `Workflow` 与 `UnityRuntime`”的过度收敛。
- 权威知识域恢复为 `Workflow`、`Client`、`Networking`、`Server`、`Protocol`、`UnityRuntime`。
- `Client` 对应 Movement、玩家输入、动画和相机；`Networking` 对应 Mirror 连接与玩家网络组件。
- `Server` 和 `Protocol` 当前没有独立源码目录，分别由 Mirror Headless/NetworkManager 与 Mirror 内置消息/序列化边界承载；文档明确记录这一事实，不虚构实现。

## 2026-09-29：重建权威文档

- 根据当前代码、场景、Prefab、构建结果和运行验证重建活动文档，未读取、迁移或修改 `docs/`。
- 根路由、项目总览和 UnityRuntime 文档统一指向 `PersistentScene`、`MultiplayerSampleScene` 与当前角色 Prefab。
- 将固定 Tick、`InputFrame`、快照和预测明确标记为后续计划，避免把计划描述为已实现事实。
- 工作流实例知识域清单收敛为 `Workflow` 与 `UnityRuntime`，`ProjectValidationMode=None` 保持不变。

## 2026-09-29：知识域收敛

- 项目转型为物理派对游戏，`Client`、`Networking`、`Server`、`Protocol` 四个知识域从权威文档库移除，旧网络栈实现通过 git tag `v0.2-selfbuilt-net` 查阅。
- 权威文档收敛为 `Project_Overview.md`、`UnityRuntime/`（场景资产 + Mirror 配置）与 `Workflow/`。
- Mirror 96.11.2 安装进工程，联机链路验证通过，配置记录在 `UnityRuntime/Mirror_KCP_Config.md`。
- 教学与联调类文档位于 `docs/`。

## 2026-09-20：建立中文权威文档库

- 创建根路由、工作流指南、WorkingAgent 注册表和知识域。
- `ProjectValidationMode=None`，未推断项目构建、启动或测试命令。
