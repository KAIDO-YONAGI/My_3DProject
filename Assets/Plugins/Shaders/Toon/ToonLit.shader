// 来源：https://github.com/ColinLeung-NiloCat/UnityURPToonLitShaderExample
// 许可：MIT License（详见同目录 THIRD_PARTY_NOTICES.md）
//
// 本文件按 MIT 许可引入并适配 My_3DProject：
// - Unity 2022.3.62f3c1 / URP 14.0.12
// - 追加草地顶点风动画 _UseGrassWind 系列参数
// - 描边 ZOffset 遮罩改用变换后的 uv 采样
//
// 5 个 Pass 的职责：
// 0. ForwardLit       主光照渲染
// 1. Outline          沿法线外扩的背面描边
// 2. ShadowCaster     投射阴影
// 3. DepthOnly        深度预通道
// 4. DepthNormalsOnly 深度+法线预通道（SSAO 等功能需要）
//
// 说明：上游为避免多贴图读取，把大部分特性开关做成 uniform 分支而不是 shader_feature，
// 只有 Alpha Clipping 走 shader_feature（它能提前 clip 掉像素，收益最大）。

Shader "My_3DProject/Toon/ToonLit (NiloCat URP)"
{
    Properties
    {
        [Header(High Level Setting)]
        [ToggleUI]_IsFace("Is Face? (face/eye/mouth)", Float) = 0

        [Header(Base Color)]
        [MainTexture]_BaseMap("Base Map", 2D) = "white" {}
        [HDR][MainColor]_BaseColor("Base Color", Color) = (1,1,1,1)

        [Header(Alpha Clipping)]
        [Toggle(_UseAlphaClipping)]_UseAlphaClipping("Enable?", Float) = 0
        _Cutoff("    Cutoff", Range(0.0, 1.0)) = 0.5

        [Header(Emission)]
        [Toggle]_UseEmission("Enable?", Float) = 0
        [HDR] _EmissionColor("    Color", Color) = (0,0,0)
        _EmissionMulByBaseColor("    Mul Base Color", Range(0,1)) = 0
        [NoScaleOffset]_EmissionMap("    Emission Map", 2D) = "white" {}
        _EmissionMapChannelMask("        ChannelMask", Vector) = (1,1,1,0)

        [Header(Occlusion)]
        [Toggle]_UseOcclusion("Enable?", Float) = 0
        _OcclusionStrength("    Strength", Range(0.0, 1.0)) = 1.0
        [NoScaleOffset]_OcclusionMap("    OcclusionMap", 2D) = "white" {}
        _OcclusionMapChannelMask("        ChannelMask", Vector) = (1,0,0,0)
        _OcclusionRemapStart("        RemapStart", Range(0,1)) = 0
        _OcclusionRemapEnd("        RemapEnd", Range(0,1)) = 1

        [Header(Indirect Light)]
        _IndirectLightMinColor("Min Color", Color) = (0.1,0.1,0.1,1)
        _IndirectLightMultiplier("Multiplier", Range(0,1)) = 1

        [Header(Direct Light)]
        _DirectLightMultiplier("Brightness", Range(0,1)) = 1
        _CelShadeMidPoint("MidPoint", Range(-1,1)) = -0.5
        _CelShadeSoftness("Softness", Range(0,1)) = 0.05
        _MainLightIgnoreCelShade("Ignore Cel Shade", Range(0,1)) = 0

        [Header(Additional Light)]
        _AdditionalLightIgnoreCelShade("Ignore Cel Shade", Range(0,1)) = 0.9

        [Header(Shadow Mapping)]
        _ReceiveShadowMappingAmount("Strength", Range(0,1)) = 0.65
        _ShadowMapColor("    Shadow Color", Color) = (1,0.825,0.78)
        _ReceiveShadowMappingPosOffset("    Depth Bias", Float) = 0

        [Header(Outline)]
        _OutlineWidth("Width", Range(0,4)) = 1
        _OutlineColor("Color", Color) = (0.5,0.5,0.5,1)

        [Header(Outline ZOffset)]
        _OutlineZOffset("ZOffset (View Space)", Range(0,1)) = 0.0001
        [NoScaleOffset]_OutlineZOffsetMaskTex("    Mask (black is apply ZOffset)", 2D) = "black" {}
        _OutlineZOffsetMaskRemapStart("    RemapStart", Range(0,1)) = 0
        _OutlineZOffsetMaskRemapEnd("    RemapEnd", Range(0,1)) = 1

        [Header(Grass Wind)] // 只对草地生效；非植被模型请保持 Enable 为 0
        [Toggle]_UseGrassWind("Enable?", Float) = 0
        _GrassWindStrength("    Strength (WS)", Range(0,1)) = 0.08
        _GrassWindSpeed("    Speed", Range(0,10)) = 1.5
        _GrassWindFrequency("    Frequency", Range(0,10)) = 1.0
        _GrassWindDirection("    Direction (XZ)", Vector) = (1,0,0,1)

        [Header(Render State)] // 半透明材质（如眼睛高光）才需要改这里
        [HideInInspector]_SrcBlend("__src", Float) = 1.0
        [HideInInspector]_DstBlend("__dst", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            // RenderPipeline 标签的值是 "UniversalPipeline"，不是 "UniversalRenderPipeline"。
            // 只有 GraphicsSettings 里挂了 URP Asset 时这个 SubShader 才会被使用。
            "RenderPipeline" = "UniversalPipeline"

            "RenderType" = "Opaque"
            "IgnoreProjector" = "True"
            "UniversalMaterialType" = "ComplexLit"
            "Queue" = "Geometry"
        }

        LOD 100

        // 所有 Pass 共享的 HLSL 前置块，避免逐 Pass 重复声明。
        HLSLINCLUDE

        // Alpha Clipping 是本着色器唯一的 shader_feature：
        // 它能提前丢弃像素，配合下面各个 Pass 的 clip() 有实际收益。
        #pragma shader_feature_local_fragment _UseAlphaClipping

        ENDHLSL

        // [#0 Pass - ForwardLit]
        Pass
        {
            Name "ForwardLit"
            Tags
            {
                // 用 UniversalForwardOnly 而不是 UniversalForward：
                // 这样即使项目切到延迟渲染，本着色器依然能以正向路径渲染，
                // 不需要额外实现 GBuffer Pass。
                "LightMode" = "UniversalForwardOnly"
            }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite On
            Cull Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 2.0

            #pragma vertex VertexShaderWork
            #pragma fragment ShadeFinalColor

            // Universal Pipeline keywords（照抄 URP ComplexLit.shader 的组合，
            // 构建时 URP 会按 URP Asset 的设置自动剔除用不到的变体）
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _FORWARD_PLUS
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            // Unity defined keywords
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ DEBUG_DISPLAY

            // GPU Instancing
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "NiloToonShared.hlsl"

            ENDHLSL
        }

        // [#1 Pass - Outline]
        // 第二遍渲染，沿法线把顶点向外推，并用 Cull Front 只画背面。
        Pass
        {
            Name "Outline"
            Tags
            {
                // 注意：自定义 Pass 千万不要写 LightMode，否则 URP 不会渲染这个 Pass。
                // 想让它参与 SRP Batcher，需要配合自定义 RendererFeature + cmd.DrawRenderers()。
            }

            Blend [_SrcBlend] [_DstBlend]
            ZWrite On
            Cull Front
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 2.0

            #pragma vertex VertexShaderWork
            #pragma fragment ShadeFinalColor

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fragment _ _DBUFFER_MRT1 _DBUFFER_MRT2 _DBUFFER_MRT3
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _FORWARD_PLUS
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ DEBUG_DISPLAY

            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // 定义 ToonShaderIsOutline，让共享顶点/片元函数注入描边逻辑。
            #define ToonShaderIsOutline

            #include "NiloToonShared.hlsl"

            ENDHLSL
        }

        // [#2 Pass - ShadowCaster]
        Pass
        {
            Name "ShadowCaster"
            Tags
            {
                "LightMode" = "ShadowCaster"
            }

            ZWrite On
            ZTest LEqual
            ColorMask 0 // 阴影贴图只关心深度，屏蔽颜色写入可省带宽
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0

            #pragma vertex VertexShaderWork
            #pragma fragment AlphaClipAndLODTest // 只做 clip，不做着色

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            // 方向光与点/聚光阴影使用不同的 Normal Bias 公式，靠这个关键字区分
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            // 定义 ToonShaderApplyShadowBiasFix，修正自阴影产生的痤疮伪影。
            #define ToonShaderApplyShadowBiasFix

            #include "NiloToonShared.hlsl"

            ENDHLSL
        }

        // [#3 Pass - DepthOnly]
        Pass
        {
            Name "DepthOnly"
            Tags
            {
                "LightMode" = "DepthOnly"
            }

            ZWrite On
            ZTest LEqual
            ColorMask R
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0

            #pragma vertex VertexShaderWork
            #pragma fragment DepthOnlyFragment

            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            // 描边区域也要写进深度，所以这里同样要外扩顶点。
            #define ToonShaderIsOutline

            #include "NiloToonShared.hlsl"

            ENDHLSL
        }

        // [#4 Pass - DepthNormalsOnly]
        Pass
        {
            Name "DepthNormalsOnly"
            Tags
            {
                "LightMode" = "DepthNormalsOnly"
            }

            ZWrite On
            ZTest LEqual
            ColorMask RGBA
            Cull Off

            HLSLPROGRAM
            #pragma target 2.0

            #pragma vertex VertexShaderWork
            #pragma fragment DepthNormalsFragment

            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

            #pragma multi_compile_fragment _ LOD_FADE_CROSSFADE

            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "NiloToonShared.hlsl"

            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
