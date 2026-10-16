# Project 开发记录

## 2026-10-16：全局音频系统与音频触发链路

- 播放：`AudioPlayerManager` 单例按 `AudioType` 从 `AudioConfigSO` 取片段，交给 `AudioPlayer` 对象池播放；背景音乐使用独立音源做淡入淡出，场景加载完成时播放加载音效。
- 资源：配置资产为 `Assets/Core/SO/Audio/AudioConfig.asset`，音效与背景音乐素材位于 `Assets/Materials/SFX/`。
- 触发：`AudioTriggerManager` 把音频类型、来源对象与世界坐标封装为 `AudioTriggerMessage`，经 `AudioTriggerEventSO` 广播。
- 入口：输入资产新增 `Test` 动作并绑定反引号键，`CharacterInputReader.TestPressed` 暴露该动作。
- 测试：新增 `Assets/Tests/Editor/AudioPlayerTests.cs`。
- 计数：`1/5 → 2/5`。

## 2026-10-13：配置角色共享动画逻辑与覆盖资产

- `Assets/Core/Animations/Characters/娜娜莉（华丽飞踢）.overrideController` 与 `娜娜莉（学园之星）.overrideController` 共用 `Assets/Core/Models/Resources/CharacterLocomotion.controller`，各绑定 22 个有效动画片段。
- 两个 Local、两个 Sync 和两个模型目录 Prefab 的 Animator 引用对应覆盖资产，Root Motion 关闭。角色动画片段通过覆盖资产 Inspector 配置，混合树和跳跃转换通过共用 Controller 的 Animator 窗口配置。
- `NetworkPlayer` 保存运动参数，模型挂载与切换绑定已配置的 Animator，保留根运动驱动。共用覆盖资产由资源系统管理，多个 Animator 独立保存参数。
- 验证：`Core.EditorTests` 34/34，`Core.Networking.PlayModeTests` 12/12，132 个槽位引用匹配迁移基线，六个 Prefab 的 30 项实际动画播放检查通过。证据见 `Logs/AnimatorEditorConfig/Verification.json`。
- Character、Project 两域 Guide 与 Router 同步，Project 维护计数为 `3/5`。

## 2026-10-11：修复三渲二着色器 LOD 交叉淡化的编译错误

- 根因：`NiloToonShared.hlsl` 的 `AlphaClipAndLODTest` 调用 `LODFadeCrossFade()`，而该函数只由 URP 的 `LODCrossFade.hlsl` 声明，本目录 5 个 HLSL 头文件都没有包含它。编译 `LOD_FADE_CROSSFADE` 变体时报 `undeclared identifier 'LODFadeCrossFade'`，位置 `Assets/Core/Shaders/Toon/NiloToonShared.hlsl(497)`，平台 d3d11。`ToonLit.shader` 的 ForwardLit、Outline、ShadowCaster、DepthOnly、DepthNormalsOnly 五个 Pass 都声明了 `#pragma multi_compile_fragment _ LOD_FADE_CROSSFADE`，该变体在播放器构建期参与编译。
- 修复：`NiloToonShared.hlsl` 按 URP `LitForwardPass.hlsl`、`ShadowCasterPass.hlsl` 的写法，在 `LOD_FADE_CROSSFADE` 定义时条件包含 `Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl`；`ShadeFinalColor` 在 `InitializeSurfaceData` 之后调用 `LODFadeCrossFade(input.positionCS)`，ForwardLit 与 Outline 两个颜色 Pass 的淡化裁剪因此与三个深度、阴影 Pass 一致。
- 验证：修复后 Unity 重新导入，`ToonLit.shader` 的 8 个变体（ForwardLit、Outline、ShadowCaster、DepthOnly 的顶点与片元程序）编译 `ok=1`，编译记录里没有指向该文件的报错行。同一工程、同一 URP 14.0.12 与 d3d11 平台下，URP 内置 `Lit` 的 `LOD_FADE_CROSSFADE` 变体（ForwardLit 4 个、ShadowCaster、DepthOnly、DepthNormals）编译 `ok=1`，同款包含方式与两个着色器共用的 `#pragma target 2.0` 组合可用。修复前的 22 条失败记录全部指向第 497 行，修复后没有出现指向新调用点的记录。
- 文档与计数：`Assets/Core/Shaders/Toon/THIRD_PARTY_NOTICES.md` 的改动清单补上该项。Project 维护计数 `1/5 -> 2/5`。

## 2026-10-11：新增运行时程序集 Core.FrameWork.StateMachine

- 变更：新增 `Assets/Core/Scripts/StateMachine/Core.FrameWork.StateMachine.asmdef`，收纳 `HierarchicalState.cs` 与 `HierarchicalStateMachine.cs`。该程序集为纯 C#，不引用 `UnityEngine`，`autoReferenced: true`，无 `references`，是工程内唯一的运行时自建程序集。预定义程序集 `Assembly-CSharp` 因此无法引用它，测试程序集需要直接引用业务类型时须走独立程序集。
- 引用关系：`Assembly-CSharp` 自动引用该程序集，业务代码可直接 `using Core.My_FrameWork.Scripts.StateMachine;`；`Assets/Tests/Editor/Core.EditorTests.asmdef` 的 `references` 为 `["Core.FrameWork.StateMachine"]`，测试可直接继承并实例化框架类型。
- 验证：Unity 强制刷新后 Console 为 0 错误、0 警告，业务代码与测试程序集两侧均编译通过；`Core.EditorTests` 27 个测试全部通过。
- 文档与计数：`Project_Guide.md` 的「资产目录布局」补充该程序集与上述引用约束。Project 维护计数 `0/5 → 1/5`。

## 2026-10-11：同步两套学园之星裙摆配置与碰撞组件

- Local、Sync 两套学园之星 Prefab 的 8 条裙摆 DynamicBone 起点为 `_002`，阻尼 `0.75`、弹性 `0.8`、刚度 `0.94`、平移跟随 `0.8`、粒子半径 `0.006 m`，前四项分布曲线为常数 `1`。
- 每套左右大腿各有 1 个 Outside 胶囊，X 轴、中心 `(-0.18217, 0, 0)`、半径 `0.065 m`、高度 `0.36 m`，裙摆只引用骨盆和两侧大腿。每套共 28 个 DynamicBone、8 个 Collider。
- 验证：两套各 22 个配置片段，每片段 180 帧，共 7920 帧，实际播放片段不匹配为零。裙腰相对动画姿势偏转为 `0°`，待机裙边最大偏移约 `7 mm`，组合运动测试最大下缘偏移 `7.3 cm`。常驻场景实例已继承新配置，Unity Console 为 0 错误、0 警告。
- 技术边界：抬腿画面存在局部穿模，骨端胶囊最大残留穿透约 `3.76 cm`。维护计数保持 `0/5`。
- 证据位于 `Logs/SkirtFix_20261011/`。

## 2026-10-11：固化华丽飞踢 Avatar 映射并同步嘴部修复事实

- 根因与修复：华丽飞踢 FBX 的自动 Humanoid 映射将脸部控制根 `mouth` 映射为 `Jaw`。实际待机动画使嘴部旋转 `10.28555°`，同姿势蒙皮对照显示最大向下位移 `0.0118413 m`；移除 Jaw 映射后，嘴部旋转与该骨骼引起的顶点差均为零。修复在模型导入层完成，没有运行时骨骼覆盖代码。
- 配置事实：`Assets/Core/Models/异环_娜娜莉（华丽飞踢）/Model/娜娜莉（华丽飞踢）.fbx.meta` 保持 `animationType: 3`，保留其余 54 项人形映射并显式保存 351 项骨架描述，`autoGenerateAvatarMappingIfUnspecified: 0`。导入后的 `Avatar.isValid`、`Avatar.isHuman` 均为真，Jaw 映射数为零。
- 引用事实：模型 GUID 保持 `f75e3b29cf8cbb4458c41c2184aeb2da`。`CharactersForLocal/娜娜莉（华丽飞踢）.prefab` 与 `CharactersForSync/娜娜莉（华丽飞踢）_Sync.prefab` 的 `m_Avatar` 引用该 GUID 的 `fileID: 9000000`。
- 验证：两套 Prefab 各播放全部 22 个配置片段、88 个采样点，共 176 个；嘴部旋转与嘴部引起的蒙皮差均为零，动画片段不匹配为零，身体动画正常求值。按实际配置调用项目 `CharacterAnimator.Initialize` 后各推进 120 帧，实际待机片段 `Idle_Wait_A` 的最大嘴部旋转仍为零。Unity Console 为 0 错误、0 警告。过程证据见 `Logs/MouthAnimationFix_20261011.md`。
- 维护复核：计数 `4/5 -> 5/5`。FBX 显式映射、自动映射开关、模型 GUID 与两套 Prefab 引用已复核，Project Guide 和 Router 补齐，Unity 与 Guide 所列相关包版本一致。完成复核更新后计数重置为 `0/5`。

## 2026-10-11：接入 URP 14.0.12 并实装三渲二着色器

- 管线：`Packages/manifest.json` 加入 `com.unity.render-pipelines.universal: 14.0.12`。`Packages/packages-lock.json` 的解析结果为 `universal`、`core`、`shadergraph` 均 `14.0.12`，另有 `universal-config: 14.0.10`、`burst: 1.8.21`、`mathematics: 1.2.6`、`settings-manager: 2.1.0`。Unity 自动生成 `Assets/UniversalRenderPipelineGlobalSettings.asset`。
- 编译隔离：`Assets/ToonEditor/Core.Toon.Editor.asmdef` 用 `versionDefines` 把引用 `UnityEngine.Rendering.Universal` 的编辑器脚本隔离在 `MYPROJECT_URP_PRESENT` 宏内，`expression: [14.0,999.0]`。URP 14.0 及以上存在时宏自动定义。
- 着色器：`Assets/Core/Shaders/Toon/` 下有 `ToonLit.shader` 与 5 个 HLSL 头文件，源自 `UnityURPToonLitShaderExample`，MIT，出处与改动清单见同目录 `THIRD_PARTY_NOTICES.md`。相对上游修正三处：补上 `.shader` 已声明但 `CBUFFER` 缺失的四个光照参数并接进方程；描边 ZOffset 遮罩改用 `_BaseMap_ST` 变换后的 uv 采样；混合模式读取 `[_SrcBlend] [_DstBlend]`，默认值保持 `One Zero`。
- 草地风动画：着色器含 `_UseGrassWind` 系列参数，按世界坐标做相位、顶点 Y 做高度权重，用双正弦叠加。
- 编辑器工具：`Assets/Core/ToonEditor/` 下三个脚本，菜单入口 `My_3DProject/三渲二/`。`UrpAssetSetup` 先把 `UniversalRendererData` 建出来落盘再读回其序列化内容，用它自身填好的运行时着色器列表创建管线资产。`ToonMaterialConverter` 处理 `Standard` 与 `Nature/SpeedTree*`，跳过 `Assets/Plugins/`，原材质备份到 `.toon-migration-backup/`。`GrassWindMaterialSetup` 按映射生成开启风动画的草地材质副本。
- 工程事实：材质 139 个，插件 101 个，需迁移 38 个，角色 23、草地 9、树与枝 6；23 个角色材质与 13 个植被材质声明了 `_Cutoff = 0.5` 一类取值，4 个材质为半透明；植被中的 9 个草地材质用 `Nature/SpeedTree`，fileID 51；四个场景均无烘焙光照数据；`Assets/Core` 下 53 个 C# 文件全量扫描，0 处使用 `OnRenderImage`、`Graphics.Blit`、`CommandBuffer`、`RenderingPath` 等管线相关 API。
- 记录位置：着色器与迁移步骤的完整说明放在 `Assets/Core/Shaders/Toon/TOON_MIGRATION.md`，随资产一起版本控制。
- 遗留事实：两个玩法场景的 TerrainData 无任何 TerrainLayer 引用，9 个草地材质未被任何资产引用，地形与草地不出画，需要自行绘制。`My_3DProject_clone_0` 是 ParrelSync 克隆工程，其 `Assets` 是指向主工程的 Junction 软链接，`Packages` 与 `ProjectSettings` 独立，需单独加 URP 依赖并重做管线绑定。
- 维护计数保持 `4/5`。

## 2026-10-10：清理运动同步残留组件与孤儿事件资产

- `NetworkPlayer.prefab` 的根节点为 6 个组件：`Transform`、`NetworkIdentity`、`NetworkTransformReliable`、`CharacterController`、`PlayerCharacterController`、`NetworkCharacterSync`，顺序未变、末尾为 `NetworkCharacterSync`。fileID `7814609285416083721`、GUID `0b9dca0ce9ed4d9094a8df5c8901df22` 的 Missing Script 记录及其 `m_Component` 引用已删除。
- `Assets/Core/SO/EventSOs/BoolEventChannel.asset` 及其 `.meta`、变空的 `EventSOs/` 目录已删除。其脚本 `BoolEventChannelSO.cs` 在提交 `a148055` 中删除，资产 GUID `0ae334929aa1e354d818f91aeeac1bc9` 在全工程零引用，`AddressableAssetsData` 无该路径规则。
- `UnityRuntime_Guide.md` 补记 Build Settings 第 4 项 `MultiplayerSampleScene 1.unity` 及两张图互为往返传送的关系；`Router.md` 移除已失效的 `Assets/Core/SO/EventSOs/` 证据路径与并发资源。
- 验证：`Assets/Core` 脚本引用 213 处，缺失脚本 GUID 归零；Prefab 内部 7 个锚点引用无悬空。
- 维护计数 `3/5 -> 4/5`。

## 2026-10-10：角色功能路径与资产待处理项同步

- Router 与 Guide 更新角色功能目录，移除当前入口中 `Character/Runtime/` 路径；原 Networking 路径保留。11 个迁移脚本 GUID 对照 `HEAD` 一致。
- 主编辑器 EditMode 10/10 通过。本次纯文档维护，计数保持 `3/5`。

## 2026-10-08：常驻场景显式绑定单机相机组件

- `PersistentScene` 的管理器有三组相机组件引用，绑定“娜娜莉（学园之星）”内的 `Main Camera`；源组件 fileID 经编辑器查询确认，角色 Prefab 未改写。
- 场景测试覆盖引用可解析、停用后可恢复、未配置对象不受切换影响。EditMode 8/8；PlayMode 网络与相机任务成功，完成 49 项、未报告失败；控制台无错误。
- 新场景相机需要显式配置。维护计数 `2/5 -> 3/5`。

## 2026-10-08：绑定场景单机角色引用

- `PersistentScene` 的 `NetworkCharacterManager.standalonePlayer` 绑定现有“娜娜莉（学园之星）”实例根，fileID `2132654451`，对象名与 Prefab 未改动。
- 主编辑器重新加载磁盘场景后，引用非空、指向该场景角色、停用后恢复和不影响未引用角色的测试通过；EditMode 7/7、网络表现 PlayMode 9/9，无失败或跳过。
- 维护计数 `1/5 -> 2/5`。

## 2026-10-08：脚本目录重组后的路径同步

- 证据：`Assets/Core/Scripts/Camera/Runtime/`、`Assets/Core/Scripts/Character/`、`Assets/Plugins/DynamicBone/` 的实际磁盘清单；Unity Console 0 error；PlayMode 49/49 与 EditMode 5/5 通过。
- 相机脚本移入 `Assets/Core/Scripts/Camera/Runtime/`，角色脚本目录 `Movement/` 更名 `Character/`，第三方 `DynamicBone` 移入 `Assets/Plugins/`（无 asmdef，编译进 `Assembly-CSharp-firstpass`）。移动连同 `.meta` 一起执行，脚本 GUID 不变，编辑器内实测三个含相机的 Prefab 与角色 Prefab 的组件引用正常解析。
- 序列化字段、Prefab 与场景内容均未改动，本次只同步 `Router.md` 的证据路径与并发资源，不增加维护计数。

## 2026-10-08：相机运行时拆分与资产兼容验证

- 证据：主编辑器 `My_3DProject@6d686e37950b774c` 的编译、Console、PlayMode 49/49、EditMode 5/5，以及两个本地角色 Prefab 的实时 `SerializedObject` 检查；测试无失败或跳过。
- `ThirdPersonCamera` 的 17 个序列化字段及脚本 GUID 不变；两个 Prefab 的 `targetRotateSmooth=15`、`targetRotateSpeed=360`，四个辅助类不作为组件，场景与 Prefab 未改动。
- 父级转向耦合、最终候选避让和光标生命周期已修复；独立复审后补充外部解锁再捕获、起始重叠安全退出及双墙裁剪回归。
- 维护计数 `0/5 -> 1/5`。

## 2026-10-08：Addressables 配置目录

- 证据：Unity 编辑器 `My_3DProject@6d686e37950b774c` 的实机探针读取 `AssetDatabase.GUIDToAssetPath`、`AddressableAssetSettingsDefaultObject.Settings`、`ConfigFolder` 与 `GetContentStateBuildPath`，以及 `Assets/Plugins/AddressableAssetsData/` 的磁盘清单。
- Addressables 配置位于 `Assets/Plugins/AddressableAssetsData/`，随工程一起入库。
- 插件按 GUID 解析配置，设置对象、两个分组与 `BuildScriptPackedMode` 正常解析，Console 0 error。
- `ConfigFolder` 与 `GetContentStateBuildPath()` 指向 `Assets/Plugins/AddressableAssetsData/`，与 `Windows/addressables_content_state.bin` 的位置一致。
- 目录迁移步骤与缓存求值条件写入 `Project_Guide.md` 的 Addressables 章节，`Router.md` 补充证据路径与并发资源。
- 维护计数保持 `0/5`。

## 2026-10-08：切换到 Input System 后端并记录旧输入边界

- 证据：`ProjectSettings/ProjectSettings.asset` 的 `activeInputHandler: 1`，编辑器内 `SerializedObject` 实时读取同为 `1`；`Assets/Core/Input/`；全 `Assets` 旧输入 API 扫描；构建场景表。
- Active Input Handling 为 `Input System Package (New)`；`ProjectSettings/InputManager.asset` 的旧轴定义保留，项目脚本读取 Input System。
- 全 `Assets` 扫描 639 个 `.cs` 文件：`Assets/Mirror/` 下 27 个文件使用旧输入 API，其中运行时组件 `Components/GUIConsole.cs`、`Components/Profiling/ToggleHotkey.cs`、`Components/Profiling/RemoteStatistics.cs` 未被 `Assets/Core`、`Assets/FrameWork` 的场景、Prefab 或脚本引用；Build Settings 只包含 4 个 `Assets/Core/Scenes` 场景。
- `PersistentScene` 的 `EventSystem` 使用 `InputSystemUIInputModule` 与默认 `CursorLockBehavior`，`OutsideScreen`。
- 维护计数 `4/5 -> 5/5`；达到阈值后复核 `Project_Guide.md` 与实际配置并更新该文档，计数归零 `0/5`。

## 2026-09-30：修复单机表现与联机第三角色

- `NetworkCharacterManager` 在 `NetworkClient.isConnected` 后禁用 PersistentScene 的单机角色和相机，连接重试阶段保留单机表现。
- `PlayerCharacterController` 使用同一连接完成条件限制输入，未连接时单机重力不因 KCP 重试暂停。
- 无服务端验证单机角色自然落地并保持唯一活动场景相机；双编辑器验证每端仅显示两个网络角色。
- `Core.EditorTests` 为 4/4 通过。

## 2026-09-30：修复联机出生高度与相机归属

- 修正 `PersistentScene` 的本地角色 Prefab 根引用和两个 `NetworkStartPosition`，出生点位于 `TerrainCollider` 上方约 `0.2m`。
- `NetworkCharacterManager` 在 Additive 场景加载后重新禁用非网络 Camera、`ThirdPersonCamera` 和 `AudioListener`。
- 通过 Unity MCP 重建 `Server_12_7`，再以两个 Unity 编辑器连接验证；本地角色稳定落地，只有本地网络角色相机启用。

## 2026-09-30：确认玩家 Prefab 与本地模型目录配置

- `NetworkManager.playerPrefab` 为玩家网络根 Prefab；`PersistentScene` 中的 NetworkManager 实例保持启用。
- `PersistentScene/Managers/NetworkCharacterManager.characterPrefabs[]` 使用 `NetworkCharacterSync` 的角色编号加载 `CharactersForSync` 远程角色，角色内的 `ThirdPersonCamera` 和 Animator 不迁移到网络根对象。
- `NetworkManager.prefab` 默认关闭 `autoConnectInEditor`，避免单机编辑器 Play 被自动连接流程干扰；联机仍可通过 HUD 或显式启动 Host。
- 在 Unity 实例 `My_3DProject@6d686e37950b774c` 中完成 Host、模型切换和截图验证。

## 2026-09-29：更新为当前联机入口与玩家同步方案

- 联机入口为 `PersistentScene`，`onlineScene` 指向 `MultiplayerSampleScene`，Build Settings 同时启用两者。
- NetworkManager 的玩家 Prefab 为 `Assets/Core/Prefabs/CharactersForSync/娜娜莉（华丽飞踢）.prefab`。
- `AutoStartClient` 使编辑器和普通客户端连接 `127.0.0.1`，每 3 秒重试，批处理和已启动网络端不重复连接。
- `PlayerCharacterController` 为普通 `MonoBehaviour`，通过可选 `NetworkIdentity` 限制联机输入归属；`NetworkTransformReliable` 使用 `ClientToServer`。
- 客户端 `Client_5_0`、服务器 `Server_7_0` 构建成功；服务端配合编辑器验证角色移动约 `2.82m` 且 2 秒后未回弹，Console 0 error。

## 2026-09-29：整理脚本目录

- 证据：`Assets/Core/Scripts/` 目录现状、Unity Console 编译 0 error。
- `BoolEventChannelSO.cs` 位于 `Assets/Core/FrameWork/Scripts/SO/`，GUID 与资产绑定保持完好。
- `Networking/` 3 个 Mirror 脚本保持原位，被 `Player_Network.prefab` 和 `LobbyScene.unity` 按 GUID 引用。

## 2026-09-29：安装 Mirror 并建立联机链路

- 证据：`Assets/Mirror/` 96.11.2、`LobbyScene.unity`、`Player_Network.prefab`、`Assets/Core/Scripts/Networking/` 三个脚本、构建产物、连接日志。
- Mirror 96.11.2 安装进工程；`LobbyScene` 含 NetworkManager 与 KcpTransport 7777；Build Settings 场景表配置为 LobbyScene 与 MultiplayerSampleScene。
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
