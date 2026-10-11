# 三渲二（Toon）迁移与使用说明

本文档说明本工程从**内置渲染管线**迁移到 **URP**、并实装 NiloCat 风格三渲二着色器的过程、使用方式与调参入口。

- 目标环境：Unity `2022.3.62f3c1` + URP `14.0.12`
- 着色器位置：`Assets/Core/Shaders/Toon/ToonLit.shader`
- 着色器名：`My_3DProject/Toon/ToonLit (NiloCat URP)`
- 上游来源与许可：见同目录 `THIRD_PARTY_NOTICES.md`（MIT）

---

## 一、迁移前的工程事实（已核实）

这些是本次迁移的判断依据，改动前后都成立，便于日后回溯：

| 项目 | 迁移前状态 |
|---|---|
| 渲染管线 | Built-in。`ProjectSettings/GraphicsSettings.asset` 的 `m_CustomRenderPipeline` 为 `{fileID: 0}` |
| 自写着色器 | 一个都没有。`Assets/Core` 下 21 个 `.shader/.hlsl` 全部属于 Mirror 与 TextMesh Pro 插件 |
| 材质 | 共 139 个。其中 133 个用内置 `Standard`，6 个属插件（TMP、Mirror 性能面板）<br>按目录排除插件后，需要迁移的是 **38 个** |
| 角色 | 2 套娜娜莉模型，23 个材质，全部 `Standard` |
| 植被 | 9 个草地材质用 **已废弃的 `Nature/SpeedTree`**（fileID 51）<br>6 个树/枝材质（4 个 `Nature/SpeedTree8` + 2 个 `Standard`） |
| 场景光照 | 4 个场景均无烘焙光照数据；`m_AmbientMode: 0`（Skybox 环境光）+ 实时阴影 → **不需要重烘焙** |
| 后处理 | 无 PostProcessing Stack v2、无 Volume、无反射探针 |
| C# 代码 | 53 个文件全量扫描，**0 处**使用 `OnRenderImage`/`Graphics.Blit`/`CommandBuffer`/`RenderingPath` 等管线相关 API → **迁移无编译风险** |
| 地形 | 两个玩法场景各有一个 Terrain，但 **TerrainData 里没有任何 TerrainLayer 引用**，地形是裸的 |

> 最后一条很重要：那 9 个草地材质**没有被工程里任何场景、Prefab、TerrainLayer 引用**，草地当前不会渲染出来。风动画着色器已经实现，但要看效果必须先把草画到地形上。

---

## 二、迁移操作步骤

### 步骤 1：等 Unity 装好 URP

`Packages/manifest.json` 已加入：

```json
"com.unity.render-pipelines.universal": "14.0.12",
```

打开工程后 Unity 会解析并下载 URP 及其依赖（`com.unity.render-pipelines.core`、`com.unity.shadergraph`）。**这一步必须要能联网。**

### 步骤 2：（无需操作）编辑器工具会自动就绪

迁移工具放在 `Assets/Core/ToonEditor/`，带一个程序集定义 `Core.Toon.Editor.asmdef`。它用 `versionDefines` 探测 URP：

```json
"versionDefines": [
  { "name": "com.unity.render-pipelines.universal", "expression": "[14.0,999.0]", "define": "MYPROJECT_URP_PRESENT" }
]
```

只要工程里存在 URP 14.0 及以上版本，`MYPROJECT_URP_PRESENT` 就会**自动定义**，一步都不用手工做。URP 缺失或版本不够时宏不定义，`UrpAssetSetup.cs` 里引用 `UnityEngine.Rendering.Universal` 的那段代码被 `#if` 排除，整个工程不会因为命名空间不存在而编译失败。

> 早先的版本用工程根目录的 `csc.rsp` 手工开宏，那个方案已废弃：**Unity 不读取工程根目录的 `csc.rsp`**（只读 `Assets/` 下的），实测编译器参数里根本没有该宏，工具会一直提示"尚未启用"。改用 asmdef 的 `versionDefines` 后既可靠又免手工。

### 步骤 3：创建并绑定 URP Asset

菜单 **`My_3DProject/三渲二/创建并绑定 URP Asset`**

它会：
1. 创建 `Assets/Core/Rendering/URP_Toon_Renderer.asset` 与 `URP_Toon_PC.asset`
2. 把管线资产绑定到 `GraphicsSettings` 与全部质量档位

> 关于时机：这一步做完，画面会变成**统一的粉色**——因为此时 38 个材质还是内置 `Standard` 着色器，URP 下无法渲染。这是预期行为，用来确认管线已被接管。紧接着做步骤 4 即可。

### 步骤 4：转换材质

菜单 **`My_3DProject/三渲二/转换材质为 ToonLit`**

它会：
- 扫描 `Assets` 下**除 `Plugins` 之外**的所有材质
- 只处理下面这四类内置管线着色器（按 Unity 实际报告的名字，不要按材质文件里的 fileID 反查，内置 shader 的 fileID 与名字对应关系容易记错）：
  - `Standard` —— 角色 23 个
  - `Legacy Shaders/Transparent/Cutout/Diffuse` —— 草地 9 个
  - `Nature/Tree Soft Occlusion Leaves` —— 树枝与松针 4 个
  - `Nature/Tree Soft Occlusion Bark` —— 树皮 2 个
- 换 shader 前把原 `.mat` 另存到 `.toon-migration-backup/`（该目录在 `.gitignore` 内，不入库）
- 按下面的对照表搬运属性
- 日志写到 `Logs/ToonMigration/material-convert-log.txt`（`Logs/` 同样已被 `.gitignore` 忽略）

> `.toon-migration-backup/` 只是逐文件回退的便利手段，不是唯一的回退途径：迁移前的原始材质在 git 提交 `04acdf9` 里完整可取回，所以该目录删掉也不影响回退。工具下次运行时会自动重建。

### 步骤 5：生成草地风动画材质

菜单 **`My_3DProject/三渲二/生成草地风动画材质`**

在 `Assets/Materials/ForestMaterials/grass/Materials/Toon/` 下生成 9 个开启 `_UseGrassWind` 的材质副本。

工具自己扫描草地目录发现源材质，**不依赖任何外部映射文件**。可重复执行：已存在的副本只刷新风参数，不会重复新建。

风参数默认值：强度 `0.08`、速度 `1.5`、频率 `1.0`、方向 `(1, 0, 0)`。改这几个值在 `GrassWindMaterialSetup.cs` 顶部的常量里。

> 这 9 个材质至今未被任何场景引用。要看到草地，需要把草画到地形上（Terrain → Paint Texture / Paint Details），再把地形图层指向 `.../grass/Materials/Toon/` 下的材质。

### 步骤 6：验证

- **控制台无着色器编译错误**（最关键的检查项）
- 两套娜娜莉在 `PersistentScene`、`MultiplayerSampleScene` 下的表现
- 本地角色与远程 Sync 角色两条 Prefab 链路
- 睫毛、眼睛、脸部 Mask 的 Alpha 与描边是否正常
- **专用服务器 Headless 构建**能否出包

---

## 三、属性对照表

材质转换器按此表搬运，只列出名字真的变了的属性，其余同名直传。

| 旧（Standard） | 新（ToonLit） | 说明 |
|---|---|---|
| `_MainTex` | `_BaseMap` | 贴图引用按 guid 原样搬运 |
| `_MainTex` 的 Scale/Offset | `_BaseMap_ST` | **必须搬，否则贴图会整体错位** |
| `_Color` | `_BaseColor` | |
| `_Cutoff` | `_Cutoff` | 同名 |
| `_EmissionMap` / `_EmissionColor` | 同名 | |
| `_OcclusionMap` / `_OcclusionStrength` | 同名 | |
| `_Mode` = 1 或含 `_ALPHATEST_ON` | 开 `_UseAlphaClipping` 关键字 | |
| `_Mode` = 2/3 或含 `_ALPHABLEND_ON` | `_SrcBlend`=5、`_DstBlend`=10、队列 3000 | 半透明 |
| — | `_IsFace` | 脸部/眼睛/嘴巴设为 1，脸部不用 NdotL 硬交界 |

项目里真实存在的非默认 ST（容易被忽略，改着色器时最容易出错的地方）：

| 材质 | `_MainTex` 的 Scale / Offset |
|---|---|
| `eye_touming.mat` | Scale `x=0.5`（双眼共用半张贴图） |
| `pine.mat` / `bark.mat` / 各 `grass_*.mat` | Offset `y=-1` |

---

## 四、调参入口

### 最常调的参数

| 参数 | 作用 | 建议起点 |
|---|---|---|
| `_CelShadeMidPoint` | 明暗交界线位置。值越小亮面越大 | 角色 `0`；想更硬朗可试 `-0.1` |
| `_CelShadeSoftness` | 交界硬度。`0` 是纯硬边，越大越柔 | `0.05` |
| `_IsFace` | 脸部/眼睛/嘴巴设 1，跳过 NdotL 硬交界 | 脸部材质设 `1` |
| `_OutlineWidth` | 描边宽度 | `1`；嫌粗调 `0.5` |
| `_OutlineColor` | 描边颜色 | 深灰更自然；纯黑对比过强 |
| `_OutlineZOffset` | 描边深度偏移，用来把描边推进去 | `0.0001` |
| `_ShadowMapColor` | 阴影色，带暖调更像动漫 | `(1, 0.825, 0.78)` |
| `_ReceiveShadowMappingAmount` | 接收阴影贴图的强度 | `0.65` |
| `_IndirectLightMinColor` | 环境光下限。**防止没烘焙光照探针时全黑** | `(0.1,0.1,0.1)` |
| `_UseGrassWind` 系列 | 草地顶点风动画 | 强度 `0.08`、速度 `1.5`、频率 `1.0` |

### 关于 `_MainLightIgnoreCelShade` / `_AdditionalLightIgnoreCelShade`

上游仓库在 `.shader` 里声明了这两个参数，但 `Shared.hlsl` 的 `CBUFFER` 里**没有定义**，等于没接线。本项目已补上并真正接进光照方程：

- `0` = 完全按卡通硬交界线
- `1` = 完全忽略交界线，用连续的 NdotL 光照

默认主光 `0`、额外光 `0.9`。

### 想改画面观感，优先改这里

`Assets/Core/Shaders/Toon/NiloToonLightingEquation.hlsl`

整套三渲二的光照方程都在这一个文件里。三个关键函数的职责：

- `ShadeGI` —— 间接光。**故意丢弃 SH 细节项，只取 `SampleSH(0)` 平均环境色**，这是消除"3D 立体感"、逼出平面二次元感的关键一笔
- `ShadeSingleLight` —— 单光源的卡通明暗。核心就一行 `smoothstep(中点-柔和度, 中点+柔和度, NdotL)`
- `CompositeAllLightResults` —— 合成。`max(间接光, 直接光) + 自发光`，既保色相又防过曝

### 描边与深度偏移的原理（管线无关，值得记住）

- `NiloOutlineUtil.hlsl`：用 `unity_CameraProjection._m11` 反解相机 FOV，乘视空间深度，让描边**在远近和不同 FOV 下屏幕宽度一致**；正交相机另走一套
- `NiloZOffset.hlsl`：只覆写 `positionCS.z`，用"虚拟顶点推到相机前"骗过深度测试。专治眉毛被刘海插进去、脸部描边难看、Z-Fighting

---

## 五、已知事项与后续工作

1. **地形没有绘制任何图层**。迁 URP 后地形会用 URP 的默认地形材质渲染，是裸的。要草地或地表贴图，需要自己画到地形上。
2. **草地材质未被引用**。风动画着色器已实现，但需要手动挂到地形才会显现。
3. **`eye_touming.mat` 是半透明**（`_Color.a = 0.22`、队列 3000、`ZWrite On`）。眼睛高光一般不希望参与描边，若发现眼睛边缘有多余描边，把该材质 `_OutlineWidth` 调到 `0`。
4. **着色器用 `Cull Off` 双面渲染**（上游设计如此，方便睫毛、发片、草叶单面模型）。角色是封闭网格，会因背面多一倍像素开销。如果压性能，把 `ToonLit.shader` 三个渲染 Pass 的 `Cull Off` 改成 `Cull Back`，再给草叶单独留一个双面变体。
5. **专用服务器构建**：URP 会进 server 构建。若包体敏感，在 URP Asset 里精简特性，或为 server 平台配 Shader Stripping。
6. **ParrelSync 克隆工程**：`My_3DProject_clone_0` 的 `Assets` 是指向主工程的 Junction 软链接，但 `Packages` 与 `ProjectSettings` 是独立的。克隆工程需要单独加 URP 依赖并重做管线绑定。

---

## 六、回退方式

迁移在独立分支 `feature/urp-toon-migration` 上进行，按提交粒度回退即可。

素材级回退：

- 草地、角色、植被的原始 `.mat` 都在 git 里，直接 `git checkout <提交> -- <路径>` 取回
- 迁移前的完整状态在提交 `04acdf9`；转换后的状态在 `19ae354`（迁移合并）之后
- 转换工具另存到 `.toon-migration-backup/` 的副本只是逐文件回退的便利手段，该目录已按清理要求删除，工具下次运行时自动重建

---

## 七、相关文件

| 路径 | 作用 |
|---|---|
| `Assets/Core/Shaders/Toon/ToonLit.shader` | 主着色器，5 个 Pass |
| `Assets/Core/Shaders/Toon/NiloToonShared.hlsl` | 共享顶点/片元流程、CBUFFER、草地风动画 |
| `Assets/Core/Shaders/Toon/NiloToonLightingEquation.hlsl` | 光照方程，**调参主入口** |
| `Assets/Core/Shaders/Toon/NiloOutlineUtil.hlsl` | 描边宽度屏幕恒定 |
| `Assets/Core/Shaders/Toon/NiloZOffset.hlsl` | 深度偏移 |
| `Assets/Core/Shaders/Toon/NiloInvLerpRemap.hlsl` | 重映射工具函数 |
| `Assets/Core/Shaders/Toon/THIRD_PARTY_NOTICES.md` | 上游出处与 MIT 许可、改动清单 |
| `Assets/Core/ToonEditor/Core.Toon.Editor.asmdef` | 编辑器工具程序集；用 versionDefines 探测 URP 自动定义宏 |
| `Assets/Core/ToonEditor/UrpAssetSetup.cs` | 创建并绑定 URP Asset |
| `Assets/Core/ToonEditor/ToonMaterialConverter.cs` | 批量材质转换 |
| `Assets/Core/ToonEditor/GrassWindMaterialSetup.cs` | 草地风动画材质生成 |
| `Assets/Core/Rendering/URP_Toon_PC.asset` | URP 管线资产（运行菜单后生成） |
| `Assets/Core/Rendering/URP_Toon_Renderer.asset` | Universal Renderer Data（运行菜单后生成） |
| `Assets/Core/Prefabs/Lights/CharacterRimLight.prefab` | 角色轮廓光，嵌套在 4 个角色 Prefab 下 |

> 早期曾用 PowerShell 脚本走纯文本改写 `.mat` 的路线，已废弃并删除。原因：内置默认贴图要写成 `{fileID: 0}` 而不是贴图名，`_BaseMap_ST` 这类 Vector 属性也不该放进 `m_TexEnvs` 段，手写 Unity 序列化格式坑太多。现在全部交给 `ToonMaterialConverter.cs`，由 Unity 自己的序列化器落盘。历史实现可在 git 提交 `19ae354` 的 `Tools/ToonMigration/` 下查到。
