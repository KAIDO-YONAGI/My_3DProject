# Unity 工程与资产

文档 ID：`PROJECT-GUIDE`
状态：`Active`
最后核验：`2026-10-11`

## 渲染管线

工程已从内置渲染管线迁到 **URP 14.0.12**，并实装基于 `UnityURPToonLitShaderExample`（MIT）改造的三渲二着色器。

- 管线依赖：`Packages/manifest.json` 声明 `com.unity.render-pipelines.universal: 14.0.12`。同版本传递依赖为 `com.unity.render-pipelines.core` 与 `com.unity.shadergraph`（均 `14.0.12`），另有 `com.unity.render-pipelines.universal-config: 14.0.10`。
- 编译隔离：编辑器工具放在 `Assets/ToonEditor/`，带程序集定义 `Core.Toon.Editor.asmdef`，用 `versionDefines` 探测 URP（`expression: [14.0,999.0]` → `define: MYPROJECT_URP_PRESENT`）。URP 14.0 及以上存在时宏自动定义，**无需任何手工步骤**；URP 缺失或版本不够时宏不定义，`UrpAssetSetup.cs` 里引用 `UnityEngine.Rendering.Universal` 的代码被 `#if` 排除，工程不会因此编译失败。
  > 早期版本曾用工程根目录的 `csc.rsp` 手工开宏，该方案已废弃：**Unity 不读取工程根目录的 `csc.rsp`**（只读 `Assets/` 下的），实测编译器参数里没有该宏。`csc.rsp` 已删除。
- 管线资产：`Assets/Rendering/URP_Toon_PC.asset` 与 `URP_Toon_Renderer.asset`，绑定到 `GraphicsSettings` 与全部质量档位。
- 着色器：`Assets/Core/Shaders/Toon/ToonLit.shader`，着色器名 `My_3DProject/Toon/ToonLit (NiloCat URP)`，含 ForwardLit、Outline、ShadowCaster、DepthOnly、DepthNormalsOnly 五个 Pass。注意 Editor 下 `Shader.passCount` 报告为 `1` 属正常现象（URP 的 Lit/Unlit/Simple Lit 同样报 1，内置 `Standard` 报 5），不能据此判断 Pass 丢失。
- 编辑器工具菜单为 `My_3DProject/三渲二/`，含创建管线资产、批量材质转换、生成草地风动画材质三项。三个工具均幂等，可重复执行。
- 迁移与调参说明见 `Assets/Core/Shaders/Toon/TOON_MIGRATION.md`；上游来源与 MIT 许可见同目录 `THIRD_PARTY_NOTICES.md`。

迁移依据的工程事实：材质共 **154** 个（含迁移后新增），其中插件 116 个、项目自有 **38 个**（角色 23、草地 9、树与枝 6）。这 38 个材质的原着色器按 Unity 实际报告的名字分四类：`Standard` 23 个、`Legacy Shaders/Transparent/Cutout/Diffuse` 9 个（草地）、`Nature/Tree Soft Occlusion Leaves` 4 个、`Nature/Tree Soft Occlusion Bark` 2 个。
> 判定这些着色器名时必须用 Unity 报告的 `shader.name`，**不要按材质文件里的 `fileID` 反查**。第一版转换器按 fileID 反查，把草地误判成 `Nature/SpeedTree`、把树误判成 `Nature/SpeedTree8`/`Standard`，导致 15 个材质漏转。fileID 与内置 shader 名的对应关系容易记错。

四个场景均无烘焙光照数据，`m_AmbientMode: 0`（Skybox 环境光）配实时阴影，因此迁移不需要重烘焙。`Assets/Core` 下 53 个 C# 文件全量扫描，0 处使用 `OnRenderImage`、`Graphics.Blit`、`CommandBuffer`、`RenderingPath` 等管线相关 API。

## 版本与依赖

- Unity 编辑器：`2022.3.62f3c1`。
- Mirror：`96.11.2`。
- **URP：`14.0.12`**。
- Addressables：`1.22.3`。
- Input System：`1.14.2`。
- Terrain Tools：`5.0.6`。
- TextMeshPro：`3.0.7`。
- Timeline：`1.7.7`。
- UGUI：`1.0.0`。
- Visual Scripting：`1.9.4`。
- Unity MCP：Git 依赖的 `main` 分支。

## 资产目录布局

顶层 `Assets/` 目录：

| 目录 | 内容 |
|---|---|
| `Core/` | 项目自有代码、场景、Prefab 与模型 |
| `Materials/` | 地形植被与地表贴图等素材 |
| `Plugins/` | 第三方插件 |
| `Tests/` | EditMode 与 PlayMode 测试程序集 |
| `ToonEditor/` | 三渲二管线与材质迁移编辑器工具 |
| `Rendering/` | URP 管线与 Renderer 资产 |
| `Terrain/` | 玩法地形数据 |

- `Assets/Plugins/` 收纳 `Mirror/`、`ParrelSync/`、`TextMesh Pro/`、`AddressableAssetsData/`。Mirror 的脚本模板位于 `Assets/Plugins/Mirror/MirrorScriptTemplates/`。
- 自写代码位于 `Assets/Core/Shaders/Toon/`（三渲二着色器与说明）与 `Assets/ToonEditor/`（管线与材质迁移工具）。
- 单机框架代码位于 `Assets/Core/Scripts/GamePlayFrameWork/`，包含 `Scripts/`、`Teleport/` 与 `Samples/`。
- 分层状态机框架位于 `Assets/Core/Scripts/GamePlayFrameWork/Scripts/StateMachine/`，带运行时程序集 `Core.FrameWork.StateMachine`（纯 C#、不依赖 `UnityEngine`，`autoReferenced: true`）。它是工程内唯一的运行时自建程序集：预定义程序集 `Assembly-CSharp` 会自动引用它，业务代码可直接 `using Core.My_FrameWork.Scripts.StateMachine;`；测试程序集必须在自己的 `references` 中显式列出，`Core.EditorTests` 已加入该引用。
  > **asmdef 不能引用预定义程序集 `Assembly-CSharp`**，这就是 `Assets/Tests/Editor/` 下多数测试只能通过 `Type.GetType("..., Assembly-CSharp")` 反射访问运行时代码的原因。要让运行时代码被测试直接引用与继承，就必须像本框架这样先把它从 `Assembly-CSharp` 移入自带 asmdef 的程序集。
- 地形植被与地表贴图素材位于 `Assets/Materials/ForestMaterials/`。
- 角色脚本位于 `Assets/Core/Scripts/Character/` 下的 `Control/`、`Input/`、`Movement/`、`Movement/States/`、`Animation/`、`Presentation/`。`PlayerCharacterController` 位于 `Control/`，`CharacterMovementSettings` 位于 `Movement/`，动画配置位于 `Animation/`。
- 网络管理与同步脚本位于 `Assets/Core/Scripts/Networking/`。
- 测试程序集位于 `Assets/Tests/`：`Editor/` 为 `Core.EditorTests`，`PlayMode/Camera/` 为 `Core.Camera.PlayModeTests`，`PlayMode/Networking/` 为 `Core.Networking.PlayModeTests`。移动或重命名测试脚本时必须连同 `.meta` 一起移动。

## Addressables

- 配置目录为 `Assets/Plugins/AddressableAssetsData/`。
- 插件通过 GUID 链路解析配置。`ProjectSettings/EditorBuildSettings.asset` 的 `com.unity.addressableassets` 记录 `DefaultObject.asset` 的 GUID `00520c7ebfe0ced4ea09c7be32769208`，`DefaultObject.asset` 记录 `AddressableAssetSettings.asset` 的 GUID `3c0067e525af651409829bf27364034c`。
- 分组为内建的 `Built In Data` 和默认组 `Scenes`。
- 激活的构建脚本为 `BuildScriptPackedMode`。`Local.BuildPath` 与 `Local.LoadPath` 引用 `Addressables.BuildPath` 和 `Addressables.RuntimePath`，构建产物位于 `Library/com.unity.addressables/`。
- 内容状态文件位于 `Assets/Plugins/AddressableAssetsData/Windows/addressables_content_state.bin`，路径由 `GetContentStateBuildPath()` 按配置目录与平台子目录求值。
- 移动配置目录时，先让资源库完成该目录变更，再重新加载脚本域，然后核对 `ConfigFolder` 指向新目录。`AddressableAssetSettings` 的 `AssetPath` 与 `ConfigFolder` 在设置对象首次访问时求值，并在该脚本域内保持不变；在资源库完成目录变更前求值会得到变更前的位置，构建、Analyze 与内容更新随后按该位置创建目录。

## Player Settings 与输入

- `ProjectSettings/ProjectSettings.asset` 的 `activeInputHandler` 为 `1`，只启用 `Input System Package (New)`。旧 `UnityEngine.Input` API 在运行时不可用。
- 输入资产是 `Assets/Core/Input/PlayerControls.inputactions`，其导入器设置 `generateWrapperCode` 生成包装类 `Assets/Core/Input/PlayerControls.cs`，JSON 内嵌在代码里，不需要 Inspector 引用。
- `ProjectSettings/InputManager.asset` 保留旧轴定义，项目脚本不读取。
- `PersistentScene` 的 `EventSystem` 使用 `InputSystemUIInputModule`，动作资产为包的 `DefaultInputActions`，`CursorLockBehavior` 为默认的 `OutsideScreen`。
- `Assets/Plugins/Mirror/` 中有 27 个脚本使用旧 `Input` API，包括 `Components/GUIConsole.cs`、`Components/Profiling/ToggleHotkey.cs`、`Components/Profiling/RemoteStatistics.cs`。这些类型不在 `Assets/Core` 的场景、Prefab 或脚本引用内，也不在构建场景内；运行 Mirror 示例场景会在运行时报错。

## NetworkManager Prefab

路径：`Assets/Core/Prefabs/NetworkManager.prefab`

关键组件：

- `Mirror.NetworkManager`
- `Mirror.NetworkManagerHUD`
- `kcp2k.KcpTransport`
- `AutoStartClient`

关键配置：

- `networkAddress=127.0.0.1`
- `maxConnections=100`
- `sendRate=60`
- `runInBackground=true`
- `headlessStartMode=AutoStartServer`
- `onlineScene` 为空
- `offlineScene` 为空
- `playerPrefab=Assets/Core/Prefabs/NetworkPlayer.prefab`
- `autoCreatePlayer=true`
- `playerSpawnMethod=Random`

## NetworkPlayer Prefab

路径：`Assets/Core/Prefabs/NetworkPlayer.prefab`

根节点共 6 个组件，顺序如下：

1. `Transform`
2. `Mirror.NetworkIdentity`
3. `Mirror.NetworkTransformReliable`
4. `CharacterController`
5. `PlayerCharacterController`
6. `NetworkCharacterSync`

`NetworkTransformReliable` 使用 `ClientToServer`，同步位置和旋转，启用变化检测、旋转压缩和插值。`CharacterController` 的高度为 `2`，半径为 `0.5`，中心为 `(0, 1, 0)`。

`PlayerCharacterController` 的移动配置为运行速度 `6.8`、疾跑速度 `10.8`、加速度 `32`、减速度 `38`、空中控制 `0.45`、旋转速度 `360`、重力 `-28`、跳跃高度 `1.2`、接地吸附速度 `-2`。

## 角色表现资源

`NetworkCharacterManager` 的角色数组按下标配对：

| 角色编号 | 本地拥有者 Prefab | 远程同步 Prefab |
|---|---|---|
| `0` | `CharactersForLocal/娜娜莉（华丽飞踢）.prefab` | `CharactersForSync/娜娜莉（华丽飞踢）_Sync.prefab` |
| `1` | `CharactersForLocal/娜娜莉（学园之星）.prefab` | `CharactersForSync/娜娜莉（学园之星）_Sync.prefab` |

默认角色编号为 `1`。本地 Prefab 提供 Camera 和 `ThirdPersonCamera`。同步 Prefab 提供远程模型、Animator 和动画配置。

模型由 `NetworkCharacterManager` 挂在 `NetworkPlayer` 下，子模型的 `PlayerCharacterController` 与 `CharacterController` 停用，根节点承担网络 Transform、运动和碰撞。重绑只复制动画配置和本地相机参考，保留根 `CharacterMotor` 的速度与代码状态。

### 华丽飞踢模型与 Avatar 导入

模型配置文件为 `Assets/Core/Models/异环_娜娜莉（华丽飞踢）/Model/娜娜莉（华丽飞踢）.fbx.meta`，模型 GUID 为 `f75e3b29cf8cbb4458c41c2184aeb2da`。Local、Sync 两套华丽飞踢 Prefab 的 `m_Avatar` 均引用该 GUID、`fileID: 9000000`，共用同一模型 Avatar。

2026-10-11 为修复运行时嘴部下移，已通过 `ModelImporter` 固化原 Avatar 的 `humanDescription`，仅移除脸部控制根 `mouth` 的 `Jaw` 映射。导入类型保持 Humanoid（`animationType: 3`），人形映射由 55 项变为 54 项，其余映射保留；骨架描述显式保存 351 项，`autoGenerateAvatarMappingIfUnspecified: 0`，避免重新自动补入错误下颌映射。导入结果 `Avatar.isValid = true`、`Avatar.isHuman = true`、Jaw 映射数为零，模型 GUID 与两套 Prefab 的引用不变。

该模型的 `mouth` 不是通用人体下颌骨，而是嘴唇、嘴角和牙齿控制骨骼的父节点；映射为 Jaw 会在通用人形动画求值时引起面部蒙皮变形。重新导入或替换模型时，应检查显式映射、自动映射开关与两套 Prefab 的 Avatar 引用。根因对照与骨骼验证方法见 `../Character/Character_Guide.md` 的“人形映射与面部骨骼”，不要用运行时旋转补丁掩盖导入配置错误。

### 学园之星裙摆 Prefab 配置

2026-10-11 已同步调整 `CharactersForLocal/娜娜莉（学园之星）.prefab` 与 `CharactersForSync/娜娜莉（学园之星）_Sync.prefab`：8 个裙摆 DynamicBone 的起点由 `_001` 下移到 `_002`，参数为阻尼 `0.75`、弹性 `0.8`、刚度 `0.94`、平移跟随 `0.8`、粒子半径 `0.006 m`，前四项分布曲线均为常数 `1`。

两套 Prefab 各在左右大腿上新增 1 个 DynamicBoneCollider，Outside、X 轴、中心 `(-0.18217, 0, 0)`、半径 `0.065 m`、高度 `0.36 m`。每条裙链只引用骨盆与两侧大腿的 3 个碰撞器；其他动态骨骼仍保留原碰撞引用。每套 DynamicBone 总数仍为 28，Collider 总数由 6 增为 8，其他 20 个动态骨骼配置不变；FBX、Avatar、材质、插件源码和用户场景文件未修改。

重新加载保存后的 Prefab 并求值 22 个动画片段，结果与保存前候选配置一致；`PersistentScene` 的 8 条裙链已继承 `_002` 起点与新参数，场景保持未修改。裙腰不再发生模拟偏转，待机下缘最大位移约 `7 mm`，急停转身及腿部碰撞组合测试最大约 `7.3 cm`。大幅抬腿仍有局部穿模，骨端胶囊测试不等价于整片裙面无穿透；验证方法和边界见 `../Character/Character_Guide.md` 的“学园之星裙摆”。

## Inspector 配置提示

`NetworkManager` Prefab 上的 `AutoStartClient` 字段显示中文 Tooltip，说明自动连接的运行环境、地址配置入口和重试间隔。连接地址统一填写在 Mirror `NetworkManager` 的 `Network Address`，使用 IP 或主机名。自动连接和 HUD 连接共用 `networkAddress`，端口由同一 Prefab 上的 `KcpTransport.Port` 提供。

`PersistentScene` 中 `NetworkCharacterManager` 的两个角色数组与默认编号显示中文 Tooltip。数组下标从 `0` 开始，两套数组的同一下标表示同一角色；默认编号对应的两个位置均配置有效 Prefab。

`standalonePlayer` 引用常驻场景内的单机角色实例，指向场景内“娜娜莉（学园之星）”根对象 fileID `2132654451`，通过该引用直接启停单机角色。`standaloneCameras`、`standaloneCameraControllers`、`standaloneListeners` 引用场景组件，当前均绑定该角色内的 `Main Camera`。新加入的单机角色与相机需要手动配置引用。

## 验证覆盖

`Core.EditorTests` 覆盖单机角色引用绑定、表现模式切换、本地角色 Prefab 完整性和出生点高度。场景与 Prefab 的序列化引用均可解析。构建输出根目录为 `D:/Unity/Releases/3D_MultiplayerGame/`，客户端与专用服务器分别输出到 `Client/` 与 `Server/`。

2026-10-11 的华丽飞踢 Avatar 修复已核验导入有效性、Jaw 映射数与 Local、Sync Prefab 引用。隔离预览场景中，两套 Prefab 各播放 22 个配置片段，共 176 个采样点，嘴部旋转及嘴部引起的蒙皮差均为零；项目 `CharacterAnimator.Initialize` 的实际配置链路另各推进 120 帧，结果一致，Unity Console 为 0 错误、0 警告。上述验证未包含用户场景 PlayMode、双客户端联机或构建验收。过程证据见 `Logs/MouthAnimationFix_20261011.md`，关键配置与结果同时记录于本域 `DeveloperLog.md`。

## 维护触发

修改 Unity 或包版本、渲染管线、着色器、资产目录布局、Addressables 配置目录与分组、Player Settings、Prefab 组件组成、角色数组或移动配置时更新本文档。角色 FBX 导入、Humanoid Avatar 映射或模型 GUID 引用变化时，同时维护本文档与 `../Character/Character_Guide.md`。场景内容归 `../Scene/Scene_Guide.md`，Mirror 与 KCP 参数归 `Mirror_KCP_Config.md`。
