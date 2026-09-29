# Assets 脚本整理计划

## 现状结论（已核实）

全项目自有脚本共 45 个，分布如下：

| 位置 | 数量 | 状态 |
|---|---|---|
| `Assets/Core/FrameWork/Scripts/{Core,Scene,SO,UI}/` | 23 | ✅ 已按 UI/Scene → SO → Core 分层，不动 |
| `Assets/Core/Scripts/Networking/` | 3 | ✅ Mirror 联机脚本（NetworkPlayerController、LocalPlayerCamera、AutoStartServerBuild），已被 `Player_Network.prefab` 和 `LobbyScene.unity` 按 GUID 引用，位置合理，**不动** |
| `Assets/Core/Models/_SharedDependencies/` | 16 | 第三方共享库（Movement/DynamicBone），用户已确认不动；`Movement/Resources/CharacterLocomotion.controller` 被 `CharacterAnimator.cs` 以硬编码路径 Resources.Load，移动会崩 |
| `Assets/Core/FrameWork/Samples/Scripts/` | 2 | 示例脚本，跟随 Samples 不动 |
| `Assets/Core/Scripts/Events/BoolEventChannelSO.cs` | 1 | ❌ **唯一错位脚本**，与 FrameWork/SO 下的 FloatEventSO、IntEventSO、VoidEventSO 同类却散落在外 |

`Assets/forest` 为纯地形植被资源（无脚本），本次不动。

## 执行步骤

1. **申请租约**（按 `Workflow/Concurrency_Guide.md`，用 `WorkingAgent.ps1`，Acquire 带 SessionId + Model）：
   - 写范围：`Assets/Core/Scripts/**`、`Assets/Core/FrameWork/Scripts/SO/**`、`Y_MultipleAgentWorkflow/UnityRuntime/**`

2. **移动脚本**（文件与 `.meta` 一起 `git mv`，GUID `2e72fff1...` 随 meta 保留，引用自动不破）：
   - `Assets/Core/Scripts/Events/BoolEventChannelSO.cs`(+.meta) → `Assets/Core/FrameWork/Scripts/SO/`
   - 删除清空后的 `Assets/Core/Scripts/Events/` 目录及其 `.meta`
   - 不修改任何 .cs 内容

3. **Unity 验证**（编辑器在线，经 MCP）：
   - `refresh_unity` 强制刷新 + 编译，读 Console 确认无报错
   - 检查 `Core/EventSOs/BoolEventChannel.asset` 脚本绑定未丢失（GUID 不变，绑定应完好）

4. **同步权威文档**（按根路由要求）：
   - `UnityRuntime_Guide.md`：「事件资产」节脚本路径改为新位置；「脚本目录现状」节注明 Events 已并入 FrameWork/Scripts/SO；更新「最后核验」
   - `UnityRuntime/DeveloperLog.md` 追加一条记录

5. **释放租约**，报告结果。不提交 git（未收到提交指令）。

## 不做 / 后续可选

- `Core/Prefabs` 根层两个「娜娜莉」prefab 与 `CharactersForSync` 下的「副本 1」命名混乱——属资产重命名，不在本次脚本整理范围
- `AGENTS.md` 项目结构一节仍描述已退役的 `Client/Net/LocalServer` 布局，与现状不符；本次只动 UnityRuntime 域文档，是否重写 AGENTS.md 由你决定
