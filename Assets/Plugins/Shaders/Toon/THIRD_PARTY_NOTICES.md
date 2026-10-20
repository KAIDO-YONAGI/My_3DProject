# 第三方着色器来源与许可

## UnityURPToonLitShaderExample（NiloCat）

- 上游仓库：<https://github.com/ColinLeung-NiloCat/UnityURPToonLitShaderExample>
- 作者：ColinLeung-NiloCat
- 许可：**MIT License**
- 引入日期：以 `git log` 中本次迁移提交为准

本目录下的以下文件源自该仓库，按 MIT 许可引入本项目并做了适配修改：

| 本目录文件 | 上游文件 |
|---|---|
| `NiloOutlineUtil.hlsl` | `NiloOutlineUtil.hlsl` |
| `NiloZOffset.hlsl` | `NiloZOffset.hlsl` |
| `NiloInvLerpRemap.hlsl` | `NiloInvLerpRemap.hlsl` |
| `NiloToonShared.hlsl` | `SimpleURPToonLitOutlineExample_Shared.hlsl` |
| `NiloToonLightingEquation.hlsl` | `SimpleURPToonLitOutlineExample_LightingEquation.hlsl` |
| `ToonLit.shader` | `SimpleURPToonLitOutlineExample.shader` |

### 本项目相对上游的改动

1. 目标环境固定为 Unity 2022.3.62f3c1 + URP 14.0.12。
2. 补上上游 `.shader` 已声明、但 `Shared.hlsl` 的 `CBUFFER` 里缺失的四个参数
   （`_IndirectLightMultiplier`、`_DirectLightMultiplier`、`_MainLightIgnoreCelShade`、
   `_AdditionalLightIgnoreCelShade`），并把它们真正接进光照方程。
3. 描边 ZOffset 遮罩贴图改用 `_BaseMap_ST` 变换后的 uv 采样，使遮罩跟随主贴图的
   Scale/Offset（上游用原始 uv，主贴图带 Scale/Offset 时遮罩会错位）。
4. 追加草地顶点风动画（`_UseGrassWind` / `_GrassWind*` 系列参数）。
5. 裁掉 `DepthNormalsOnly` 里的视差贴图与法线贴图分支（本项目角色与植被都不使用），
   需要时可从上游补回。
6. 混合模式由写死的 `Blend One Zero` 改为读取材质属性 `[_SrcBlend] [_DstBlend]`，
   默认值仍是 `One Zero`，行为与上游一致；半透明材质（例如眼睛高光）可单独覆盖。
7. `LOD_FADE_CROSSFADE` 生效时包含 URP 的 `LODCrossFade.hlsl`，颜色 Pass 同样调用
   `LODFadeCrossFade()`；上游直接用 `LODFadeCrossFade()` 却没有包含该文件，LOD 变体无法编译。

### 注意

上游 README 明确说明该仓库是**教学示例**，不是完整版 NiloToonURP。
完整版 NiloToonURP 为闭源商业授权，本目录内容与它无关。

上游 `README.md` 放在仓库根目录，未随本节引入；如需查阅请访问上面的仓库地址。
