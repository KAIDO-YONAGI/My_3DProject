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

## 2026-09-29：整理散落脚本目录

- 证据：`git mv` 记录、`Assets/Core/Scripts/` 目录现状、Unity Console（刷新编译后 0 error）。
- `Assets/Core/Scripts/Events/BoolEventChannelSO.cs`（含 `.meta`）移入 `Assets/Core/FrameWork/Scripts/SO/`，与同类事件通道脚本（`FloatEventSO`、`IntEventSO`、`VoidEventSO` 等）统一归位；GUID `2e72fff1...` 随 meta 保留，`BoolEventChannel.asset` 绑定完好。
- 删除清空后的 `Assets/Core/Scripts/Events/` 目录及其 `.meta`。
- 其余脚本位置经核验维持不变：`Networking/` 3 个 Mirror 脚本被 `Player_Network.prefab` 和 `LobbyScene.unity` 按 GUID 引用，位置合理；`Models/_SharedDependencies` 的 Movement/DynamicBone 为第三方共享库且存在 `Resources.Load` 硬编码路径，不动。
- `UnityRuntime_Guide.md` 已同步更新脚本目录现状与事件资产章节；UnityRuntime 域维护计数 `2/5`。

## 2026-09-29：重新安装 Mirror 96.11.2 并建立最小联机链路

- 证据：`Assets/Mirror/`（96.11.2 完整插件）、`LobbyScene.unity`、`Player_Network.prefab`、`Assets/Core/Scripts/Networking/` 三个脚本、两份构建产物、连接日志。
- Mirror 恢复：从本机 Asset Store 缓存解压包 `D:/Unity/Temp/Mirror-96.11.2-extracted` 按 `pathname` 清单还原完整插件（580 个 C#，文件/meta 配对校验通过）并复制进工程；删除旧的 `Assets/ScriptTemplates/` 残留（与 Mirror 自带工具产生 CS0101 重复定义）。
- 新增脚本：`NetworkPlayerController`（isLocalPlayer 输入守卫 + 服务器执行移动）、`LocalPlayerCamera`（远端禁用相机/监听器）、`AutoStartServerBuild`（batch 自动 StartServer 带端口重试；客户端构建自动连 `127.0.0.1`，断线重连）。
- 场景：新建 `LobbyScene`（NetworkManager + KcpTransport 7777 + Ground + 光照）；Build Settings 加入 LobbyScene（0 号）与 MultiplayerSampleScene；PlayerSettings.runInBackground = true。
- 构建：客户端 `Client/Client_3_0`（187MB，含 Mirror 程序集）、专用服务器 `Server/Server_3_0`（subtarget=server，114MB）。
- 连接排障结论：客户端用 `localhost` 解析为 `::1`，服务器 KCP DualMode socket 在该机器降级为纯 IPv4 时握手包永远丢失；改用 `127.0.0.1` 后双客户端完成 KCP 握手（服务器日志 `Server: OnConnected` x2，客户端 `Client: OnConnected` 各 1）。KcpTransport.debugLog 已开启用于联调。
- 新增权威文档 `UnityRuntime/Mirror_KCP_Config.md`（配置事实、安装重装、构建命令、排障表）；UnityRuntime 域维护计数 `3/5`。
