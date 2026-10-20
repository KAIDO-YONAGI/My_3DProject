# Unity 工程与资产

文档 ID：`PROJECT-GUIDE`
状态：`Active`
最后核验：`2026-10-19`

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
| `Art/` | 经典石材纹理、URP 材质、预切 Cube 模型与粒子资源 |
| `Materials/` | 地形植被与地表贴图、音效与背景音乐等素材 |
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
- `Assets/Art/MinecraftClassic/` 保存经典石材、Blender 预切模型与粒子资源，方块 Prefab 位于 `Assets/Core/Prefabs/MinecraftClassic/`，结构见本文“经典石材与不规则预切资源”。
- 角色脚本位于 `Assets/Core/Scripts/Character/` 下的 `Control/`、`Input/`、`Movement/`、`Movement/States/`、`Animation/`、`Presentation/`。`PlayerCharacterController` 位于 `Control/`，`CharacterMovementSettings` 位于 `Movement/`，动画驱动位于 `Animation/`，角色覆盖资产位于 `Assets/Core/Animations/Characters/`。
- 网络管理与同步脚本位于 `Assets/Core/Scripts/Networking/`，按 `Connection/`、`Sync/`、`CharacterPresentation/` 分目录。
- 全局音频由 `Assets/Core/Scripts/GamePlayFrameWork/Scripts/Audio/` 的 `AudioPlayerManager`、`AudioPlayer` 与 `AudioConfigSO` 组成，配置资产位于 `Assets/Core/SO/Audio/`，音效与背景音乐素材位于 `Assets/Materials/SFX/`。
- 测试程序集位于 `Assets/Tests/`：`Editor/` 为 `Core.EditorTests`，`PlayMode/Camera/` 为 `Core.Camera.PlayModeTests`，`PlayMode/Networking/` 为 `Core.Networking.PlayModeTests`。移动或重命名测试脚本时必须连同 `.meta` 一起移动。

## 经典石材与不规则预切资源

`Assets/Art/MinecraftClassic/Textures/` 保存 Mojang 官方 Minecraft Java Edition 1.12.2 客户端中的经典石材纹理，`Materials/` 保存引用这些纹理的 URP Lit 材质。纹理来源、客户端校验信息与包内路径见 `Assets/Art/MinecraftClassic/README.md`，纹理导入设置以各 PNG 的导入器为准。

| 纹理与材质名 | 方块 |
|---|---|
| `stone` | 石头 |
| `stonebrick` | 石砖 |
| `stonebrick_mossy` | 苔藓石砖 |
| `stonebrick_cracked` | 裂纹石砖 |

### Cube 层级与材质映射

`Assets/Art/MinecraftClassic/Models/Cube_FractureTemplate.fbx` 是共享的不规则预切模型。`Assets/Core/Prefabs/MinecraftClassic/` 中的 `Cube_` Prefab 分别引用各石材材质，使用相同的层级职责：

```text
Cube_<材质名>
  Intact
  Fragments
    Node_###
```

`Intact` 承载完整方块的网格、渲染器与 BoxCollider。`Fragments` 承载封闭的凸多面体碎块，每个 `Node_###` 有独立网格、渲染器和凸 MeshCollider。Cube 根节点保留方块容器身份，碎块名提供容器内的稳定标识，局部位置保存装配关系，碎块原点位于各自的体积质心。

碎块由 Blender 官方 Cell Fracture 扩展的 Voronoi 单元算法生成，装配后共同填满原始 Cube。外表面保留完整 Cube 的 UV，切口使用石头材质。FBX 的材质槽顺序随节点变化，Prefab 材质映射按源材质名识别：`stonebrick` 对应当前方块的外表面材质，`stone` 对应切口材质。

### 圆形平台资源

`Assets/Core/Prefabs/MinecraftClassic/Platform_Circle_R25_Authoring.prefab` 保存编辑用平台，由四种 `Cube_` Prefab 随机混铺组成。根节点下的 `CenterDisk` 收纳中心圆盘内的方块，`OuterDisk` 收纳外围方块。`Cube_X_<x>_Z_<z>` 名称保存网格坐标，各 Cube 保留 `Intact`、`Fragments` 与 `Node_###` 层级和嵌套 Prefab 引用。

`Platform_Circle_R25.prefab` 是运行用平台。根节点的 `Assets/Core/Scripts/Scene/VoxelPlatform/VoxelPlatformRenderer.cs` 保存方块位置、变换、材质、共享网格和碎块 Prefab 引用，`CombinedSurface` 承载烘焙外表面，`CombinedCollision` 承载分块非凸 MeshCollider。完整方块由轻量数据表示，运行版层级由合并视觉与合并碰撞组成，碎块在破坏时创建于 `FragmentsRuntime` 下。

平台入口位于 `Assets/Core/Scripts/Scene/VoxelPlatform/VoxelPlatformRenderer.cs`，承担生命周期、序列化入口和公开接口。协作类采用普通类组合，按功能归入 `Scene/VoxelPlatform/` 的子目录：

| 子目录 | 职责 |
|---|---|
| `Data/` | 方块、烘焙表面、碰撞与分块记录 |
| `Runtime/` | 方块状态、占据索引和受影响分块调度 |
| `Rendering/` | 单位方块判定、邻面掩码、网格变体与视觉合并 |
| `Collision/` | 分块碰撞生成、碰撞网格生命周期与命中定位 |
| `Fragments/` | 原 Cube 引用、延迟碎块实例与容器变换 |
| `Baking/` | 编辑布局收集、烘焙记录生成与网格结果整理 |

`Assets/Editor/PlatformOptimization/VoxelPlatformRendererBaker.cs` 保存编辑器生成的分块网格与平台资源。邻接判断使用平台局部空间中的实际中心与朝向，对齐且紧邻的完整单位方块互相剔除接触面，高度差与方块间隙保留外露面。分块尺度以 `VoxelPlatformGeometry` 源码为准。视觉按分块、按材质合并，碰撞按分块合并完整方块网格。烘焙网格保存在 `Assets/Art/MinecraftClassic/Generated/`，启用平台时直接复用。

在平台组件的菜单中执行 `Bake Platform Surface`，由编辑用 Prefab 生成运行用 Prefab。场景内编辑完成的平台使用 `My_3DProject/Platform/Bake Active Scene Surfaces` 生成独立烘焙数据，平台外层 Prefab 实例在转换时解包，各层场景布局独立保存。参与烘焙的方块转换为方块数据、合并外表面与分块碰撞对象。

`VoxelPlatform/Baking/VoxelPlatformBakeExclusion.cs` 提供区域标记，标记节点的全部子节点保留独立 Renderer、Collider 和源方块层级。源几何收集、邻面遮挡与烘焙清理共同跳过该区域。`HasBakeSources` 只检查参与烘焙的源方块；剩余源对象全部属于排除区域时，场景批量烘焙跳过平台，运行状态继续加载已有烘焙数据。独立区域的破坏和碰撞由各 Cube 的交互脚本负责，烘焙破坏接口管理参与合并的方块。

平台采用独立区域直接编辑与合并区域源布局编辑两种流程。独立区域通过 Cube 容器调整完整块和碎块的共同变换，编辑结果保存到场景或所属编辑用资源。烘焙区域的逐块修改作用于该区域对应的完整源布局，再生成运行数据。每次烘焙整体替换该平台的方块与分块记录，输入包含全部需要合并的源方块；排除区域按层级标记保留自身对象。

Prefab 烘焙读取已保存的配对编辑用资源，输出运行用资源并保留编辑用源布局。场景烘焙直接转换场景实例，各层源布局通过各自的编辑用资源保存，转换结果拥有独立的场景烘焙数据。重新编辑场景烘焙区域时，在原平台根节点下补齐对应的完整合并源布局后生成结果；独立区域沿用其场景编辑内容。公共运行用 Prefab 与独立场景平台分别通过各自的烘焙入口更新。

`BreakBlock(Vector3 localPosition)` 与 `RestoreBlock(Vector3 localPosition)` 使用平台局部空间中的方块中心坐标。游戏逻辑通过射线命中 `CombinedCollision` 后调用 `BreakBlockAtHit(RaycastHit)`，或通过 `TryGetBlockPosition` 保存局部中心再调用破坏与恢复接口。组件根据碰撞点反查方块位置，再同步更新当前分块及邻接分块的外表面和碰撞。状态设置接口找到方块时返回成功，重复设置相同状态保持幂等。

`BreakBlocks(IReadOnlyList<Vector3>)` 按平台局部方块中心批量破坏，`BreakBlocksIn(IReadOnlyList<VoxelPlatformBlast>)` 破坏与世界空间体积相交的方块。两者在一次调用内只重建一次受影响分块，同一方块被多份体积覆盖只处理一次。方块一侧取旋转后的世界包围盒，整数 90 度旋转时与方块本身重合；标记碰撞体取世界包围盒，球形碰撞体按缩放后的半径判定。两者都接受可选的 `collisionDuration`，指定碎块从生成起保持碰撞的时长，省略时沿用 Prefab 上的配置。

`VoxelPlatformDestruction.Break(...)` 立即结算一份或一组体积，按标记碰撞体、世界盒或球形体积构造，返回实际破坏的方块数量。平台在启用时登记、停用时注销，结算按登记表遍历，不扫描场景。

`VoxelPlatformBlastTrigger` 是触发器驱动的入口：挂在带触发器碰撞体的物体上，`OnTriggerEnter` 时把标记碰撞体交给 `Break`，同步完成破坏；`regions` 留空时用子物体上的碰撞体，`collisionDuration` 传给 `Break`，`oneShot` 打开时只响应第一次触发。触发器要与静态平台产生物理事件，触发器自身或进入它的物体上需要有 Rigidbody。

破坏从原始 `Cube_` Prefab 创建碎块实例，按 Cube 容器变换放置，关闭 `Intact` 后启用 `Fragments`，恢复时销毁对应实例。碎块保留封闭网格、独立渲染器与凸 MeshCollider。碎块实例整体位于 `Debris` 层，层名由 `VoxelPlatformRenderer` 提供，破坏检测按层排除这些碰撞体；层缺失时碎块保持原有层。破碎表现脚本挂在源 Cube 的 `Fragments` 节点，通过 `OnEnable` 接管已就位的碎块，负责刚体分组、受力和寿命；指定 `collisionDuration` 时碎块生成即有碰撞并在到期后关闭，未指定时按 `escapeTime` 先关闭再恢复。组件停用时清理临时网格和碎块，再次启用时恢复烘焙状态。源对象编辑流程通过 `Rebuild()` 收集完整方块，`RefreshChangedBlocks()` 刷新显示状态与变换；运行用平台的 `Rebuild()` 复用烘焙数据。

两种平台的编辑步骤、源布局保存、Prefab 与场景烘焙细节、算法说明和破碎脚本接法见 [平台合批、碰撞与破碎接入](../../docs/VoxelPlatformRenderer.md)。

平台场景入口为 `Assets/Core/Scenes/VoxelArena.unity`，场景职责见 `../Scene/Scene_Guide.md`。平台范围与方块配置以 Prefab 为准。

### Blender 制作入口

本机 Blender 源文件为 `C:/Users/12248/.codex/tools/blender-platform-setup/MinecraftClassic.blend`，包含材质样块、装配状态的切分模板与展开的碎块预览。同目录 `build_samples.py` 调用 Cell Fracture 算法、生成网格并导出共享 FBX，切分参数与导出设置以该脚本为准。

Blender 启用 `BlenderMCP` 与 `Cell Fracture`，交互使用 `Industry_Compatible` 键位。BlenderMCP 提供编辑器控制接口，Cell Fracture 提供不规则单元切分能力。

### 粒子表现资源

`Assets/Art/MinecraftClassic/Particles/Prefabs/` 保存从 `D:/Unity/Projects/GameJam2607/Assets/Prefabs/Particles/` 迁入的 `HurtParticle`、`IceParticle`、`LuoDi`、`RunParticle` 与 `ShootingParticle`。发射、颜色、形状和运动行为由各 Prefab 内的 ParticleSystem 配置承载。

`Particles/Materials/` 中的 `Particle`、`Trails` 与 `Ground Particles` 使用 `Universal Render Pipeline/Particles/Unlit`。受击与拖尾材质引用 URP 默认粒子贴图，地面粒子材质使用白色贴图。预览入口及样本分组职责见 `../Scene/Scene_Guide.md` 的 `MinecraftClassicPreview`。

## 音效播放与回收

`AudioPlayerManager.PlaySfx(AudioType audioType, Vector3? worldPosition = null)` 从 `AudioConfigSO` 选取片段，再交给 `AudioPlayer` 播放。省略坐标时使用 2D 音效；传入世界坐标时将音源放到该位置，使用 3D 音效。传入角色的 `transform.position` 表示调用时的角色位置。

每个 `AudioPlayer` 管理一个带 `AudioSource` 的场景 GameObject，预热与按需创建的对象统一位于 `AudioPlayerManager` 节点下。每次播放都重新设置位置与 `spatialBlend`，同一实例可以交替承载 2D 与 3D 音效。播放结束或主动停止时，管理器停用对象并将其归还空闲队列；重复停止保持每个实例只入队一次。背景音乐使用管理器上的独立循环音源。

`AudioTriggerManager` 在本地输入触发时记录声源对象与触发位置，先由本机 `AudioPlayerManager` 播放一次，再经 `FeedbackEventSO` 通知本地网络玩家。`NetworkAudioPlayer` 只允许本地拥有者的实例调用 `CommandPlayAudio`，服务器收到后经 `RpcPlayAudio` 转发给其他观察者，并通过 `includeOwner = false` 排除已经本地播放过的拥有者。远端客户端使用消息中的世界坐标播放，不读取自身角色位置。

`Assets/Tests/Editor/AudioPlayerTests.cs` 覆盖世界原点、带位移与旋转的管理器下的世界坐标、不同位置间复用、3D 切回 2D、重复停止与播放结束回收，以及音频事件消息的声源和位置传递、触发器输入资源释放。

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

根节点共 8 个组件，顺序如下：

1. `Transform`
2. `Mirror.NetworkIdentity`
3. `Mirror.NetworkTransformReliable`
4. `CharacterController`
5. `PlayerCharacterController`
6. `CharacterIdSync`
7. `CharacterMotionSync`
8. `NetworkAudioPlayer`

`NetworkTransformReliable` 使用 `ClientToServer`，同步位置和旋转，启用变化检测、旋转压缩和插值。根节点承担网络身份、网络同步与碰撞，`PlayerCharacterController` 持有移动参数，`NetworkAudioPlayer` 位于网络行为末尾，负责音频事件的拥有者上行和远端播放。

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

经典石材来源、Blender 制作入口、预切 Cube 层级、圆形平台分组与合批方式、材质映射或粒子渲染依赖变化时更新本文档；平台与资源预览场景的引用与分组变化时，同时维护 `../Scene/Scene_Guide.md`。
