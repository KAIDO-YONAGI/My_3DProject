using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
#if DSH_URP_PRESENT
using UnityEngine.Rendering.Universal;
#endif

namespace Core.Editor.Toon
{
    /// <summary>
    /// 创建并配置 URP 渲染管线资产，然后绑定到 GraphicsSettings 与各质量档位。
    ///
    /// 用法：菜单 My_3DProject/三渲二/创建并绑定 URP Asset
    ///
    /// 为什么需要它：
    /// URP Asset 依赖 URP 包内部的运行时着色器引用，这些引用在导入 URP 之前无法确定，
    /// 也不适合手工全局搜索文件来找。这里改为先把一个 UniversalRendererData 建出来存盘，
    /// 再把它序列化后的内容读回来，原样取用其中的着色器引用 —— 这样拿到的引用一定正确。
    ///
    /// 关于 #if DSH_URP_PRESENT：
    /// URP 包尚未安装时，下面的 URP 类型不存在，本文件必须能跳过编译，
    /// 否则整个工程会因为"命名空间 UnityEngine.Rendering.Universal 不存在"而编译失败。
    /// 该宏由工程根目录的 csc.rsp 定义（注意是工程根目录，不是 Assets 目录）；
    /// URP 装好并导入完成后，请把它启用（见 TOON_MIGRATION.md）。
    /// </summary>
    public static class UrpAssetSetup
    {
        const string RendererAssetPath = "Assets/Core/Rendering/URP_Toon_Renderer.asset";
        const string PipelineAssetPath = "Assets/Core/Rendering/URP_Toon_PC.asset";

        [MenuItem("My_3DProject/三渲二/创建并绑定 URP Asset", priority = 101)]
        public static void CreateAndAssign()
        {
#if !DSH_URP_PRESENT
            Debug.LogError(
                "[三渲二] 本工具尚未启用：编译时没有定义 DSH_URP_PRESENT。\n\n" +
                "启用步骤：\n" +
                "1. 确认 Packages/manifest.json 里有 com.unity.render-pipelines.universal，\n" +
                "   并等待 Unity 把包装好、编译完成。\n" +
                "2. 打开工程根目录（Assets 的上一级）的 csc.rsp，去掉 define 那一行前面的 # 注释。\n" +
                "3. 回到 Unity 等待重新编译，然后重新执行本菜单。\n\n" +
                "详见 Assets/Core/Shaders/Toon/TOON_MIGRATION.md");
#else
            EnsureFolder("Assets/Core/Rendering");

            // ---- 1. 建 Renderer Data，先落盘 ----
            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            rendererData.name = "URP_Toon_Renderer";

            if (File.Exists(RendererAssetPath))
            {
                AssetDatabase.DeleteAsset(RendererAssetPath);
            }

            AssetDatabase.CreateAsset(rendererData, RendererAssetPath);
            AssetDatabase.SaveAssets();

            // ---- 2. 把它读回来，取用 URP 自己填好的运行时着色器引用 ----
            var shaders = new List<Shader>();
            var persisted = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererAssetPath);

            var field = typeof(UniversalRendererData).GetField(
                "shaders", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            if (field != null && persisted != null)
            {
                var list = field.GetValue(persisted) as List<Shader>;
                if (list != null)
                {
                    foreach (Shader s in list)
                    {
                        if (s != null)
                        {
                            shaders.Add(s);
                        }
                    }
                }
            }

            if (shaders.Count == 0)
            {
                Debug.LogWarning(
                    "[三渲二] 没能从 Renderer Data 里读到运行时着色器列表。\n" +
                    "这通常意味着 URP 版本的字段名与预期不同。URP Asset 仍然会创建，" +
                    "但请在 Inspector 里检查 Renderer Data 是否报错，必要时删掉重建。");
            }
            else
            {
                Debug.Log($"[三渲二] 已从 Renderer Data 取到 {shaders.Count} 个运行时着色器引用。");
            }

            // ---- 3. 建 URP Asset ----
            var pipelineAsset = ScriptableObject.CreateInstance<UniversalRenderPipelineAsset>();
            pipelineAsset.name = "URP_Toon_PC";

            var rendererList = new List<ScriptableRendererData> { persisted };
            var rendererListField = typeof(UniversalRenderPipelineAsset).GetField(
                "m_RendererDataList", BindingFlags.Instance | BindingFlags.NonPublic);

            if (rendererListField != null)
            {
                rendererListField.SetValue(pipelineAsset, rendererList.ToArray());
            }
            else
            {
                Debug.LogWarning("[三渲二] 找不到 m_RendererDataList 字段，渲染器列表未能写入。");
            }

            if (File.Exists(PipelineAssetPath))
            {
                AssetDatabase.DeleteAsset(PipelineAssetPath);
            }

            AssetDatabase.CreateAsset(pipelineAsset, PipelineAssetPath);
            AssetDatabase.SaveAssets();

            // ---- 4. 常用参数（用反射按属性名设置，属性名对不上时只告警不中断）----
            SetIfExists(pipelineAsset, "supportsHDR", true);
            SetIfExists(pipelineAsset, "msaaSampleCount", 4);
            SetIfExists(pipelineAsset, "shadowDistance", 50f);
            SetIfExists(pipelineAsset, "shadowCascadeCount", 2);
            SetIfExists(pipelineAsset, "supportsCameraDepthTexture", true);
            SetIfExists(pipelineAsset, "supportsCameraOpaqueTexture", false);
            SetIfExists(pipelineAsset, "renderScale", 1f);

            EditorUtility.SetDirty(pipelineAsset);
            AssetDatabase.SaveAssets();

            // ---- 5. 绑定到 GraphicsSettings 与各质量档位 ----
            GraphicsSettings.defaultRenderPipeline = pipelineAsset;

            int qualityCount = QualitySettings.names.Length;
            for (int i = 0; i < qualityCount; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipelineAsset;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "[三渲二] URP Asset 已创建并绑定完成。\n" +
                $"  管线资产：{PipelineAssetPath}\n" +
                $"  Renderer：{RendererAssetPath}\n" +
                $"  已绑定 GraphicsSettings 与 {qualityCount} 个质量档位\n\n" +
                "注意：GraphicsSettings 与 QualitySettings 的改动已写入 ProjectSettings，" +
                "但当前质量档位的选择也被改动了。如需固定到某一档，请在 Project Settings > Quality 里手动选回。");
#endif
        }

#if DSH_URP_PRESENT
        /// <summary>
        /// 按属性名设置值。URP 各小版本属性名偶有出入，找不到就告警跳过，不中断整个流程。
        /// </summary>
        static void SetIfExists(object target, string propertyName, object value)
        {
            PropertyInfo prop = target.GetType().GetProperty(
                propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (prop == null || !prop.CanWrite)
            {
                Debug.LogWarning($"[三渲二] URP Asset 上没有可写属性 {propertyName}，已跳过。");
                return;
            }

            try
            {
                prop.SetValue(target, value);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[三渲二] 设置 {propertyName} = {value} 失败：{e.Message}");
            }
        }
#endif

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = Path.GetFileName(folder);

            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, leaf);
        }
    }
}
