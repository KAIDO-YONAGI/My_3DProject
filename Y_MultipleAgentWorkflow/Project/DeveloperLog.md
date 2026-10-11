# Project 开发记录

## 2026-10-11：接入 URP 14.0.12 并实装三渲二着色器

- 依据：用户指定迁移路线为「全量迁 URP」，作用范围为「角色 + 场景植被全量三渲二」，草地保留风动画。
- 管线：`Packages/manifest.json` 加入 `com.unity.render-pipelines.universal: 14.0.12`。Unity 实际解析结果记录在 `Packages/packages-lock.json`：`universal`、`core`、`shadergraph` 均为 `14.0.12`，另有 `universal-config: 14.0.10`、`burst: 1.8.21`、`mathematics: 1.2.6`、`settings-manager: 2.1.0`。`Library/PackageCache` 下已存在对应目录，Unity 另自动生成了 `Assets/UniversalRenderPipelineGlobalSettings.asset`。
- 编译隔离：新增工程根 `csc.rsp`，用 `DSH_URP_PRESENT` 把引用 `UnityEngine.Rendering.Universal` 的编辑器脚本排除在编译之外，避免 URP 未装好时全工程编译失败。该宏默认注释，需在 URP 编译通过后手动启用。
- 着色器：`Assets/Core/Shaders/Toon/` 下新增 `ToonLit.shader` 与 5 个 HLSL 头文件，源自 `UnityURPToonLitShaderExample`（MIT，出处与改动清单见同目录 `THIRD_PARTY_NOTICES.md`）。相对上游修正三处：补上 `.shader` 已声明但 `CBUFFER` 缺失的四个光照参数并接进方程；描边 ZOffset 遮罩改用 `_BaseMap_ST` 变换后的 uv 采样；混合模式改为读取 `[_SrcBlend] [_DstBlend]`，默认值保持 `One Zero`。
- 草地风动画：着色器新增 `_UseGrassWind` 系列参数，按世界坐标做相位、顶点 Y 做高度权重，用双正弦叠加避免机械往复。
- 编辑器工具：`Assets/Core/Editor/Toon/` 下新增三个脚本，菜单入口 `My_3DProject/三渲二/`。`UrpAssetSetup` 先把 `UniversalRendererData` 建出来落盘再读回其序列化内容，用它自身填好的运行时着色器列表创建管线资产，避免手工猜 URP 内部文件 ID。`ToonMaterialConverter` 只处理 `Standard` 与 `Nature/SpeedTree*`，跳过 `Assets/Plugins/`，原材质备份到 `.toon-migration-backup/`。`GrassWindMaterialSetup` 按映射生成开启风动画的草地材质副本。
- 迁移依据的工程事实：材质 139 个，插件 101 个，需迁移 38 个（角色 23、草地 9、树与枝 6）；23 个角色材质与 13 个植被材质声明了 `_Cutoff = 0.5` 一类取值，仅 4 个材质为半透明；植被中的 9 个草地材质原用已废弃的 `Nature/SpeedTree`（fileID 51）；四个场景均无烘焙光照数据，迁移不需要重烘焙；`Assets/Core` 下 53 个 C# 文件全量扫描，0 处使用 `OnRenderImage`、`Graphics.Blit`、`CommandBuffer`、`RenderingPath` 等管线相关 API。
- 范围外改动：本条目同时提交了此前工作区里未提交的角色运动状态网络同步改动（新组件 `NetworkCharacterMovementSync`），提交号 `b171cf0`，避免与管线迁移混在同一次变更里无法区分。
- 记录位置：着色器与迁移步骤的完整说明放在 `Assets/Core/Shaders/Toon/TOON_MIGRATION.md`，随资产一起版本控制；本文档只保留结论与依据。
- 未完成：新写的着色器与三个编辑器脚本**尚未经过 Unity 编译与渲染验证**。Unity 那次成功编译发生在这些文件写入之前，不能算作它们的验证。需要在编辑器中依次执行「启用 `csc.rsp` 宏 → 创建并绑定 URP Asset → 转换材质 → 生成草地风动画材质」，再核对控制台的着色器编译结果、两套娜娜莉在 `PersistentScene` 与 `MultiplayerSampleScene` 的表现、本地与远程 Sync 两条 Prefab 链路，以及专用服务器 Headless 构建能否出包。
- 已知遗留：两个玩法场景的 TerrainData 无任何 TerrainLayer 引用，9 个草地材质也未被任何资产引用，地形与草地当前都不出画，需要自行绘制。`My_3DProject_clone_0`（ParrelSync 克隆工程）的 `Assets` 是指向主工程的 Junction 软链接，但 `Packages` 与 `ProjectSettings` 独立，需单独加 URP 依赖并重做管线绑定。
- 维护计数保持 `4/5`：本次管线改造已实质改变工程依赖与渲染实现，但成果尚未在编辑器中验证通过，按「验证高于文档」的口径不计为已验收的成功业务改造。

## 2026-10-10：清理运动同步残留组件与孤儿事件资产

- `NetworkPlayer.prefab` 删除 fileID `7814609285416083721`、GUID `0b9dca0ce9ed4d9094a8df5c8901df22` 的 Missing Script 记录及其 `m_Component` 引用。根节点现为 6 个组件：`Transform`、`NetworkIdentity`、`NetworkTransformReliable`、`CharacterController`、`PlayerCharacterController`、`NetworkCharacterSync`，顺序未变、末尾仍为 `NetworkCharacterSync`。
- 删除 `Assets/Core/SO/EventSOs/BoolEventChannel.asset` 及其 `.meta`，并删除变空的 `EventSOs/` 目录。证据：其脚本 `BoolEventChannelSO.cs` 已于提交 `a148055` 删除；资产 GUID `0ae334929aa1e354d818f91aeeac1bc9` 在全工程零引用；`AddressableAssetsData` 无该路径规则。
- `UnityRuntime_Guide.md` 删除“待后续确认清理”的待办表述，补记 Build Settings 第 4 项 `MultiplayerSampleScene 1.unity`（提交 `c5024fc` 显式注册）及两张图互为往返传送的实测关系；`Router.md` 移除已失效的 `Assets/Core/SO/EventSOs/` 证据路径与并发资源。
- 验证：`Assets/Core` 脚本引用 213 处，缺失脚本 GUID 归零；Prefab 内部 7 个锚点引用无悬空。Unity 编辑器未运行，未执行编译或 PlayMode 回归，未重新构建。
- 维护计数 `3/5 -> 4/5`。

## 2026-10-10：角色功能路径与资产待处理项同步

- Router 与 Guide 更新角色功能目录，移除当前入口中 `Character/Runtime/` 路径；原 Networking 路径保留。11 个迁移脚本 GUID 对照 `HEAD` 一致。
- 实际磁盘 `NetworkPlayer.prefab` 留有 fileID `7814609285416083721`、GUID `0b9dca0ce9ed4d9094a8df5c8901df22` 的同步组件记录，对应源码已不存在。按用户要求只记录资产待处理问题，不修改网络实现或 Prefab，不宣称全量 Missing Script 检查通过。
- 主编辑器 EditMode 10/10 通过；未重新执行 PlayMode、联机或构建。历史记录保持原文，本次纯文档维护，计数保持 `3/5`。

## 2026-10-08：常驻场景显式绑定单机相机组件

- `PersistentScene` 的管理器新增三组相机组件引用，绑定“娜娜莉（学园之星）”内的 `Main Camera`；源组件 fileID 经编辑器查询确认，未改写角色 Prefab。
- 场景测试确认引用可解析、停用后可恢复，未配置对象不受切换影响。EditMode 8/8；PlayMode 网络与相机任务成功，完成 49 项、未报告失败；控制台无错误。未重新构建或验证发布产物。
- 新场景相机需要显式配置，不再自动发现。同步运行时与 Mirror 配置说明，维护计数 `2/5 -> 3/5`。

## 2026-10-08：绑定场景单机角色引用

- `PersistentScene` 的 `NetworkCharacterManager.standalonePlayer` 绑定现有“娜娜莉（学园之星）”实例根（fileID `2132654451`），不重命名对象或修改 Prefab。
- 主编辑器重新加载磁盘场景后，引用非空、指向该场景角色、停用后恢复和不影响未引用角色的测试通过；EditMode 7/7、网络表现 PlayMode 9/9，无失败或跳过。
- 原有相机扫描保留；本次未重新构建或进行双客户端验证。维护计数 `1/5 -> 2/5`。

## 2026-10-08：脚本目录重组后的路径同步

- 证据：`Assets/Core/Scripts/Camera/Runtime/`、`Assets/Core/Scripts/Character/`、`Assets/Plugins/DynamicBone/` 的实际磁盘清单；Unity Console 0 error；PlayMode 49/49 与 EditMode 5/5 通过（细节见 `../Client/DeveloperLog.md`）。
- 相机脚本移入 `Assets/Core/Scripts/Camera/Runtime/`，角色脚本目录 `Movement/` 更名 `Character/`，第三方 `DynamicBone` 移入 `Assets/Plugins/`（无 asmdef，编译进 `Assembly-CSharp-firstpass`）。移动连同 `.meta` 一起执行，脚本 GUID 不变，编辑器内实测三个含相机的 Prefab 与角色 Prefab 的组件引用正常解析。
- 序列化字段、Prefab 与场景内容均未改动，本次只同步 `Router.md` 的证据路径与并发资源，不增加维护计数。

## 2026-10-08：相机运行时拆分与资产兼容验证

- 证据：主编辑器 `My_3DProject@6d686e37950b774c` 的编译、Console、PlayMode 49/49、EditMode 5/5，以及两个本地角色 Prefab 的实时 `SerializedObject` 检查；测试无失败或跳过，结束后编辑器未播放、未编译。
- `ThirdPersonCamera` 的 17 个序列化字段及脚本 GUID 不变；两个 Prefab 的 `targetRotateSmooth=15`、`targetRotateSpeed=360`，四个辅助类不作为组件，不修改任何场景或 Prefab。
- 修复父级转向耦合、最终候选避让和光标生命周期；独立复审后补充外部解锁再捕获、起始重叠安全退出及双墙裁剪回归。参数优先级与目标失效重绑行为同步到 Guide，算法事实维护在 Client。
- 本次仅编译、自动化回归与资产读取，不包含重新构建、双客户端联机或人工手感验证。维护计数 `0/5 -> 1/5`。

## 2026-10-08：Addressables 配置目录

- 证据：Unity 编辑器 `My_3DProject@6d686e37950b774c` 的实机探针读取 `AssetDatabase.GUIDToAssetPath`、`AddressableAssetSettingsDefaultObject.Settings`、`ConfigFolder` 与 `GetContentStateBuildPath`，以及 `Assets/Plugins/AddressableAssetsData/` 的磁盘清单。
- Addressables 配置位于 `Assets/Plugins/AddressableAssetsData/`，随工程一起入库。
- 插件按 GUID 解析配置，设置对象、两个分组与 `BuildScriptPackedMode` 正常解析，Console 0 error。
- `ConfigFolder` 与 `GetContentStateBuildPath()` 指向 `Assets/Plugins/AddressableAssetsData/`，与 `Windows/addressables_content_state.bin` 的位置一致。
- 目录迁移步骤与缓存求值条件写入 `UnityRuntime_Guide.md` 的 Addressables 章节，`Router.md` 补充证据路径与并发资源。
- 维护计数保持 `0/5`。

## 2026-10-08：切换到 Input System 后端并记录旧输入边界

- 证据：`ProjectSettings/ProjectSettings.asset` 的 `activeInputHandler: 1`（编辑器内 `SerializedObject` 实时读取同为 `1`）、`Assets/Core/Input/`、全 `Assets` 旧输入 API 扫描、构建场景表。
- Active Input Handling 改为 `Input System Package (New)`；`ProjectSettings/InputManager.asset` 的旧轴定义保留，但项目脚本不再读取。
- 全 `Assets` 扫描 639 个 `.cs` 文件：只有 `Assets/Mirror/` 下 27 个文件仍使用旧输入 API，其中运行时组件 `Components/GUIConsole.cs`、`Components/Profiling/ToggleHotkey.cs`、`Components/Profiling/RemoteStatistics.cs` 未被 `Assets/Core`、`Assets/FrameWork` 的场景、Prefab 或脚本引用；Build Settings 只包含 4 个 `Assets/Core/Scenes` 场景。
- `PersistentScene` 的 `EventSystem` 保持 `InputSystemUIInputModule` 与默认 `CursorLockBehavior`（`OutsideScreen`），未改动 UI 模块配置。
- 维护计数 `4/5 -> 5/5`；达到阈值后复核 `UnityRuntime_Guide.md` 与实际配置，本次已更新该文档（新增“Player Settings 与输入”章节并更新核验日期），计数归零 `0/5`。

## 2026-09-30：修复单机表现与联机第三角色

- `NetworkCharacterManager` 仅在 `NetworkClient.isConnected` 后禁用 PersistentScene 的单机角色和相机，连接重试阶段继续保留单机表现。
- `PlayerCharacterController` 使用同一连接完成条件限制输入，未连接时单机重力不再因 KCP 重试暂停。
- 无服务端验证单机角色自然落地并保持唯一活动场景相机；双编辑器验证每端仅显示两个网络角色。
- 新增编辑器回归测试，当前 `Core.EditorTests` 为 4/4 通过。

## 2026-09-30：修复联机出生高度与相机归属

- 修正 `PersistentScene` 的本地角色 Prefab 根引用和两个 `NetworkStartPosition`，出生点位于 `TerrainCollider` 上方约 `0.2m`。
- `NetworkCharacterManager` 在 Additive 场景加载后重新禁用非网络 Camera、`ThirdPersonCamera` 和 `AudioListener`。
- 通过 Unity MCP 重建 `Server_12_7`，再以两个 Unity 编辑器连接验证；本地角色稳定落地，模型未悬空，只有本地网络角色相机启用。
- 验证结束后两个编辑器均退出 Play，专用服务器进程已停止。

## 2026-09-30：确认 Player_Network 与本地模型目录配置

- `NetworkManager.playerPrefab` 更新为 `Assets/Core/Prefabs/Player_Network.prefab`；`PersistentScene` 中的 NetworkManager 实例保持启用。
- `PersistentScene/Managers/NetworkCharacterManager.characterPrefabs[]` 使用 `NetworkCharacterSync` 的角色编号加载 `CharactersForSync` 远程角色，角色内的 `ThirdPersonCamera` 和 Animator 不迁移到网络根对象。
- `NetworkManager.prefab` 默认关闭 `autoConnectInEditor`，避免单机编辑器 Play 被自动连接流程干扰；需要联机时仍可通过 HUD 或显式启动 Host。
- 2026-09-30 在正确 Unity 实例 `My_3DProject@6d686e37950b774c` 中完成 Host、模型切换和截图验证。

## 2026-09-29：更新为当前联机入口与玩家同步方案

- 本条取代 `安装 Mirror 并建立联机链路` 条中的 `LobbyScene`、`Player_Network.prefab`、`Client_3_0` 和 `Server_3_0` 当前状态描述；旧条目仅保留为历史过程。
- 联机入口改为 `PersistentScene`，`onlineScene` 指向 `MultiplayerSampleScene`，Build Settings 同时启用两者。
- NetworkManager 当前玩家 Prefab 为 `Assets/Core/Prefabs/CharactersForSync/娜娜莉（华丽飞踢）.prefab`。
- 新增 `AutoStartClient`：编辑器和普通客户端连接 `127.0.0.1`，每 3 秒重试，批处理和已启动网络端不重复连接。
- `PlayerCharacterController` 保持普通 `MonoBehaviour`，通过可选 `NetworkIdentity` 限制联机输入归属；`NetworkTransformReliable` 改为 `ClientToServer`。
- 客户端 `Client_5_0`、服务器 `Server_7_0` 构建成功；服务端配合编辑器验证角色移动约 `2.82m` 且 2 秒后未回弹，Console 0 error。
- 验证结束后已停止服务器、清理 UDP 7777 监听并退出 Editor Play。

## 2026-09-29：整理脚本目录

- 证据：`Assets/Core/Scripts/` 目录现状、Unity Console 编译 0 error。
- `BoolEventChannelSO.cs` 移入 `Assets/Core/FrameWork/Scripts/SO/`，GUID 与资产绑定保持完好。
- `Networking/` 3 个 Mirror 脚本保持原位，被 `Player_Network.prefab` 和 `LobbyScene.unity` 按 GUID 引用。

## 2026-09-29：安装 Mirror 并建立联机链路

- 证据：`Assets/Mirror/`（96.11.2）、`LobbyScene.unity`、`Player_Network.prefab`、`Assets/Core/Scripts/Networking/` 三个脚本、构建产物、连接日志。
- Mirror 96.11.2 安装进工程；新建 `LobbyScene`（NetworkManager + KcpTransport 7777）；Build Settings 场景表配置为 LobbyScene 与 MultiplayerSampleScene。
- 新增脚本：`NetworkPlayerController`、`LocalPlayerCamera`、`AutoStartServerBuild`。
- 构建客户端 `Client_3_0` 与专用服务器 `Server_3_0`；双客户端连接服务器验证通过。
- 连接地址使用 `127.0.0.1`：`localhost` 解析为 `::1`，服务器 KCP socket 降级为 IPv4 绑定时握手包丢失。
- 新增 `UnityRuntime/Mirror_KCP_Config.md`。维护计数 `3/5`。

## 2026-09-29：清理网络退役后的场景与资产

- 证据：`MultiplayerSampleScene.unity`、角色 Prefab、`Assets/Core/Scripts/` 目录状态。
- 场景删除 `NetManager`、`PlayerPositionManager` 节点；角色 Prefab 剥离 `SyncCharacter` 组件引用。
- 删除损坏资产 `ConnectResultChannel.asset`。

## 2026-09-20：建立 Unity 运行时权威指南

- 证据：Unity 版本、Packages、Build Settings、场景、Prefab 和事件资产。
- 记录了场景层级、Prefab 分工、事件资产和构建场景状态。
