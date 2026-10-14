# Unity 工程与资产

文档 ID：`PROJECT-GUIDE`
状态：`Active`
最后核验：`2026-10-14`

## 渲染管线

工程使用 **URP 14.0.12**，并实装基于 `UnityURPToonLitShaderExample` 改造的三渲二着色器，上游许可为 MIT。

- 管线依赖：`Packages/manifest.json` 声明 `com.unity.render-pipelines.universal`，并带入同版本的 `com.unity.render-pipelines.core`、`com.unity.shadergraph` 与 `com.unity.render-pipelines.universal-config`。
- 编译隔离：编辑器工具放在 `Assets/ToonEditor/`，带程序集定义 `Core.Toon.Editor.asmdef`，用 `versionDefines` 探测 URP 版本并映射条件宏。URP 版本达标时宏自动定义，不需要手工步骤；URP 缺失或版本不够时宏不定义，`UrpAssetSetup.cs` 里引用 `UnityEngine.Rendering.Universal` 的代码被 `#if` 排除。
- 管线资产：`Assets/Rendering/URP_Toon_PC.asset` 与 `URP_Toon_Renderer.asset`，绑定到 `GraphicsSettings` 与全部质量档位。
- 着色器：`Assets/Core/Shaders/Toon/ToonLit.shader`，着色器名 `My_3DProject/Toon/ToonLit (NiloCat URP)`，含 ForwardLit、Outline、ShadowCaster、DepthOnly、DepthNormalsOnly 五个 Pass。Editor 下 `Shader.passCount` 报告为 `1`，判定 Pass 数量以源码为准。
- 编辑器工具菜单为 `My_3DProject/三渲二/`，含创建管线资产、批量材质转换、生成草地风动画材质三项。三个工具均幂等，可重复执行。
- 调参说明见 `Assets/Core/Shaders/Toon/TOON_MIGRATION.md`；上游来源与 MIT 许可见同目录 `THIRD_PARTY_NOTICES.md`。

判定材质使用哪个着色器时，一律读 Unity 报告的 `shader.name`，不按材质文件里的 `fileID` 反查，两者对应关系容易记错。项目代码不使用 `OnRenderImage`、`Graphics.Blit`、`CommandBuffer` 这类管线相关 API。

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
- 自写代码包括 `Assets/Core/Shaders/Toon/` 的三渲二着色器与说明，以及 `Assets/ToonEditor/` 的管线与材质迁移工具。
- 单机框架代码位于 `Assets/Core/Scripts/GamePlayFrameWork/`，包含 `Scripts/` 与 `Samples/`。
- 分层状态机框架位于 `Assets/Core/Scripts/StateMachine/`，带运行时程序集 `Core.FrameWork.StateMachine`，纯 C#、不依赖 `UnityEngine`，`autoReferenced: true`。它是工程内唯一的运行时自建程序集：预定义程序集 `Assembly-CSharp` 自动引用它，业务代码可直接 `using Core.Scripts.StateMachine;`；测试程序集必须在自己配置的 `references` 中显式列出，`Core.EditorTests` 已加入该引用。
- 程序集定义文件不能引用预定义程序集 `Assembly-CSharp`，`Assets/Tests/Editor/` 下多数测试因此通过 `Type.GetType("..., Assembly-CSharp")` 反射访问运行时代码。运行时代码需要被测试直接引用与继承时，把它从 `Assembly-CSharp` 移入自带 asmdef 的程序集。
- 地形植被与地表贴图素材位于 `Assets/Materials/ForestMaterials/`。
- 角色脚本位于 `Assets/Core/Scripts/Character/` 下的 `Control/`、`Input/`、`Movement/`、`Movement/States/`、`Animation/`、`Presentation/`。`PlayerCharacterController` 位于 `Control/`，`CharacterMovementSettings` 位于 `Movement/`，动画驱动位于 `Animation/`，角色覆盖资产位于 `Assets/Core/Animations/Characters/`。
- 网络管理与同步脚本位于 `Assets/Core/Scripts/Networking/`。
- 测试程序集位于 `Assets/Tests/`：`Editor/` 为 `Core.EditorTests`，`PlayMode/Camera/` 为 `Core.Camera.PlayModeTests`，`PlayMode/Networking/` 为 `Core.Networking.PlayModeTests`。移动或重命名测试脚本时必须连同 `.meta` 一起移动。

## Addressables

- 配置目录为 `Assets/Plugins/AddressableAssetsData/`。
- 插件通过 GUID 链路解析配置：`ProjectSettings/EditorBuildSettings.asset` 记录 `DefaultObject.asset`，后者记录 `AddressableAssetSettings.asset`。
- 分组为内建的 `Built In Data` 和默认组 `Scenes`。
- 激活的构建脚本为 `BuildScriptPackedMode`。`Local.BuildPath` 与 `Local.LoadPath` 引用 `Addressables.BuildPath` 和 `Addressables.RuntimePath`，构建产物位于 `Library/com.unity.addressables/`。
- 内容状态文件位于 `Assets/Plugins/AddressableAssetsData/Windows/addressables_content_state.bin`，路径由 `GetContentStateBuildPath()` 按配置目录与平台子目录求值。
- 移动配置目录时，先让资源库完成该目录变更，再重新加载脚本域，然后核对 `ConfigFolder` 指向新目录。`AddressableAssetSettings` 的 `AssetPath` 与 `ConfigFolder` 在设置对象首次访问时求值，并在该脚本域内保持不变；在资源库完成目录变更前求值会得到变更前的位置，构建、Analyze 与内容更新随后按该位置创建目录。

## Player Settings 与输入

- `ProjectSettings/ProjectSettings.asset` 的 `activeInputHandler` 为 `1`，只启用 `Input System Package (New)`。旧 `UnityEngine.Input` API 在运行时不可用。
- 输入资产是 `Assets/Core/Input/PlayerControls.inputactions`，其导入器设置 `generateWrapperCode` 生成包装类 `Assets/Core/Input/PlayerControls.cs`，JSON 内嵌在代码里，不需要 Inspector 引用。
- `ProjectSettings/InputManager.asset` 保留旧轴定义，项目脚本不读取。
- `PersistentScene` 的 `EventSystem` 使用 `InputSystemUIInputModule`，动作资产为包的 `DefaultInputActions`，`CursorLockBehavior` 为默认的 `OutsideScreen`。
- `Assets/Plugins/Mirror/` 中有若干脚本使用旧 `Input` API，这些类型不在 `Assets/Core` 的场景、Prefab 或脚本引用内，也不在构建场景内。

## NetworkManager Prefab

路径：`Assets/Core/Prefabs/NetworkManager.prefab`

承载 `Mirror.NetworkManager`、`Mirror.NetworkManagerHUD`、`kcp2k.KcpTransport` 与 `AutoStartClient`，是传输参数、连接地址、玩家 Prefab、出生点模式和自动连接的配置入口。参数值见 `Mirror_KCP_Config.md`。

## NetworkPlayer Prefab

路径：`Assets/Core/Prefabs/NetworkPlayer.prefab`

根节点共 7 个组件，顺序如下：

1. `Transform`
2. `Mirror.NetworkIdentity`
3. `Mirror.NetworkTransformReliable`
4. `CharacterController`
5. `PlayerCharacterController`
6. `CharacterIdSync`
7. `CharacterMotionSync`

`NetworkTransformReliable` 使用 `ClientToServer`，同步位置和旋转，启用变化检测、旋转压缩和插值。根节点承担网络身份、网络同步与碰撞，`PlayerCharacterController` 持有移动参数。

## 角色表现资源

`NetworkCharacterManager.characters` 按角色编号保存完整角色 Prefab，每个角色只维护一份：

| 角色编号 | 完整角色 Prefab |
|---|---|
| `0` | `Assets/Core/Prefabs/Characters/娜娜莉（华丽飞踢）.prefab` |
| `1` | `Assets/Core/Prefabs/Characters/娜娜莉（学园之星）.prefab` |

完整角色 Prefab 同时包含模型、Animator、相机、`AudioListener`、`ThirdPersonCamera`、`PlayerCharacterController`、`CharacterController`、骨骼跟随和 DynamicBone 配置。单机直接使用完整 Prefab；联机实例化后由 `NetworkCharacterManager` 根据身份启停子组件。这样身份差异属于运行时状态，不再通过复制 Prefab 表达。

默认编号取客户端本机的 `NetworkCharacterManager.defaultCharacterId`，由本地玩家启动时上报，服务器进程内的该字段是兜底编号。

模型由 `NetworkCharacterManager` 挂在 `NetworkPlayer` 下，子模型的 `PlayerCharacterController` 与 `CharacterController` 停用，根节点承担网络 Transform、运动和碰撞。重绑直接绑定视觉 Prefab 已配置的 Animator，保留根 `CharacterMotor` 的速度与代码状态。

### 角色动画 Controller

`Assets/Core/Models/Resources/CharacterLocomotion.controller` 是共用逻辑资产，Animator 窗口维护 Grounded 内的 Direct 混合树、Run 与 Sprint 的方向混合树，以及 Grounded 与 Jump 之间的转换。参数、过渡时长和落地行为见 `../Character/Character_Guide.md` 的“编辑器动画配置”。

| 角色 | Animator Controller 引用 |
|---|---|
| 华丽飞踢 | `Assets/Core/Animations/Characters/娜娜莉（华丽飞踢）.overrideController` |
| 学园之星 | `Assets/Core/Animations/Characters/娜娜莉（学园之星）.overrideController` |

两份 `AnimatorOverrideController` 共用逻辑资产，各覆盖待机、跳跃和两组十方向移动，共 22 个有效片段槽位。角色片段通过覆盖资产 Inspector 配置，每个完整角色 Prefab 的 Animator 引用对应覆盖资产。角色 Prefab 的 Root Motion 均关闭，模型目录只保存 FBX、Avatar、材质和贴图等导入资源。

`CharacterAnimator` 使用 Animator 已绑定的 Controller 执行初始化、参数写入与落地首帧处理。模型重绑与销毁保持共用覆盖资产引用有效，各 Animator 的参数独立。

### 华丽飞踢模型与 Avatar 导入

模型配置文件为 `Assets/Core/Models/异环_娜娜莉（华丽飞踢）/Model/娜娜莉（华丽飞踢）.fbx.meta`，角色视觉源和各用途 Variant 引用同一模型 Avatar。

该文件的 Avatar 固化原 `humanDescription`，脸部控制根 `mouth` 不映射为 `Jaw`。导入类型为 Humanoid，人形映射保留，`autoGenerateAvatarMappingIfUnspecified` 关闭，导入后 Avatar 有效、为 Humanoid，Jaw 映射数为零。

`mouth` 是嘴唇、嘴角和牙齿控制骨骼的父节点。映射为 Jaw 会在通用人形动画求值时引起面部蒙皮变形。重新导入或替换模型时核对显式映射、自动映射开关与两套 Prefab 的 Avatar 引用。骨骼验证方法见 `../Character/Character_Guide.md` 的“人形映射与面部骨骼”。

### 学园之星裙摆 Prefab 配置

`Assets/Core/Prefabs/Characters/娜娜莉（学园之星）.prefab` 保存裙摆 DynamicBone 与碰撞器配置，模拟起点取裙骨链下段，每条裙链引用骨盆与两侧大腿的碰撞器。

裙腰不产生模拟偏转，大幅抬腿存在局部穿模，骨端胶囊测试不覆盖整片裙面。参数与边界见 `../Character/Character_Guide.md` 的“学园之星裙摆”。

## Inspector 配置提示

`NetworkManager` Prefab 上的 `AutoStartClient` 字段显示中文 Tooltip，说明自动连接的运行环境、地址配置入口和重试间隔。连接地址统一填写在 Mirror `NetworkManager` 的 `Network Address`，使用 IP 或主机名。自动连接和 HUD 连接共用 `networkAddress`，端口由同一 Prefab 上的 `KcpTransport.Port` 提供。

`PersistentScene` 中 `NetworkCharacterManager` 的 `characters` 数组与默认编号显示中文 Tooltip。数组下标从 `0` 开始，每个位置配置一份完整角色 Prefab；单机、联机本地和远程身份共用该位置的 Prefab。

`standalonePlayer` 引用常驻场景内的单机角色实例，通过该引用直接启停单机角色。单机相机、控制器和监听器的引用当前均绑定该角色内的 `Main Camera`。新加入的单机角色与相机需要手动配置引用。

## 验证覆盖

`Core.EditorTests` 覆盖单机角色引用绑定、表现模式切换、两份完整角色 Prefab、出生点高度、两份角色动画覆盖资产的完整性和 `characters` 数组绑定。`Core.Networking.PlayModeTests` 覆盖共享角色 Prefab 在单机、联机本地和远程身份下的组件启停、模型延迟挂载后的 Controller 绑定与初始化。场景与 Prefab 的序列化引用均可解析。构建输出根目录为 `D:/Unity/Releases/3D_MultiplayerGame/`，客户端与专用服务器分别输出到 `Client/` 与 `Server/`。

## 维护触发

修改 Unity 或包版本、渲染管线、着色器、资产目录布局、Addressables 配置目录与分组、Player Settings、Prefab 组件组成、角色数组或移动配置时更新本文档。共用动画 Controller、角色覆盖资产、Prefab 动画绑定、角色 FBX 导入、Humanoid Avatar 映射或模型 GUID 引用变化时，同时维护本文档与 `../Character/Character_Guide.md`。场景内容归 `../Scene/Scene_Guide.md`，Mirror 与 KCP 参数归 `Mirror_KCP_Config.md`。
