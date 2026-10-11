using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Core.Editor.Toon
{
    /// <summary>
    /// 把项目自有的内置管线材质批量转换为三渲二着色器材质。
    ///
    /// 覆盖范围：Assets 下除 Plugins 之外、当前使用内置 Standard 或 Nature/SpeedTree 着色器的材质。
    /// 第三方插件（Mirror、TextMesh Pro 等）的材质不在处理范围内。
    ///
    /// 用法：菜单 My_3DProject/三渲二/转换材质为 ToonLit
    ///
    /// 为什么用 C# 而不是直接改 .mat 文本：
    /// 材质是 Unity 序列化资产，_BaseMap_ST 这类 Vector 属性、内置默认贴图的引用写法
    /// 都有固定格式，手写 YAML 很容易写出 Unity 解析不了的内容。交给 Unity 自己的
    /// 序列化器最稳妥，也能顺便让 Inspector 立刻反映出真实结果。
    /// </summary>
    public static class ToonMaterialConverter
    {
        const string ToonShaderName = "My_3DProject/Toon/ToonLit (NiloCat URP)";
        const string BackupFolder = "Assets/../.toon-migration-backup";
        const string LogPath = "Tools/ToonMigration/material-convert-log.txt";

        /// <summary>旧属性名 -> ToonLit 属性名。只列出名字真的变了的那些。</summary>
        static readonly Dictionary<string, string> RenamedProperties = new Dictionary<string, string>
        {
            { "_MainTex", "_BaseMap" },
            { "_Color", "_BaseColor" },
        };

        /// <summary>迁移时原样搬运的贴图属性。</summary>
        static readonly string[] TexturePropertiesToCopy =
        {
            "_MainTex", "_BaseMap", "_EmissionMap", "_OcclusionMap",
        };

        /// <summary>迁移时原样搬运的浮点属性。</summary>
        static readonly string[] FloatPropertiesToCopy =
        {
            "_Cutoff", "_Metallic", "_Glossiness", "_BumpScale", "_OcclusionStrength",
        };

        /// <summary>迁移时原样搬运的颜色属性。</summary>
        static readonly string[] ColorPropertiesToCopy =
        {
            "_Color", "_BaseColor", "_EmissionColor",
        };

        [MenuItem("My_3DProject/三渲二/转换材质为 ToonLit", priority = 100)]
        public static void ConvertMaterials()
        {
            Shader toonShader = Shader.Find(ToonShaderName);
            if (toonShader == null)
            {
                Debug.LogError(
                    $"[三渲二] 找不到着色器 \"{ToonShaderName}\"。\n" +
                    "请确认 Assets/Core/Shaders/Toon/ToonLit.shader 存在、" +
                    "已 import 完成，且 Packages/manifest.json 已加入 URP 且包已装好。");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "转换材质为三渲二",
                    "将把项目自有的 Standard / Nature.SpeedTree 材质批量换成 ToonLit 着色器。\n\n" +
                    "原材质会先备份到 .toon-migration-backup/。\n" +
                    "第三方插件材质不受影响。\n\n是否继续？",
                    "开始转换", "取消"))
            {
                return;
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string backupRoot = Path.Combine(projectRoot, ".toon-migration-backup");
            Directory.CreateDirectory(backupRoot);

            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
            var log = new StringBuilder();
            var converted = new List<string>();
            var skipped = new List<string>();
            int failed = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);

                    // 第三方插件材质不动
                    if (path.Replace('\\', '/').Contains("/Plugins/"))
                    {
                        continue;
                    }

                    Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (mat == null || mat.shader == null)
                    {
                        continue;
                    }

                    if (!ShouldConvert(mat.shader))
                    {
                        skipped.Add($"{path}  (shader={mat.shader.name})");
                        continue;
                    }

                    ConvertOne(mat, path, toonShader, backupRoot, log);
                    converted.Add($"{path}  ({mat.shader.name} -> ToonLit)");

                    EditorUtility.SetDirty(mat);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            WriteLog(projectRoot, log, converted, skipped);

            Debug.Log(
                $"[三渲二] 材质转换完成：成功 {converted.Count} 个，跳过 {skipped.Count} 个，失败 {failed} 个。\n" +
                $"原材质备份在 .toon-migration-backup/\n" +
                $"详细清单见 {LogPath}");
        }

        /// <summary>判断材质当前用的是不是本项目要替换的内置管线着色器。</summary>
        static bool ShouldConvert(Shader shader)
        {
            if (shader.name == "Standard")
            {
                return true;
            }

            // 旧版与新版 SpeedTree 内置着色器，以及带 SpeedTree 功能的 Standard 变体
            if (shader.name.StartsWith("Nature/SpeedTree"))
            {
                return true;
            }

            return false;
        }

        static void ConvertOne(
            Material mat, string path, Shader toonShader, string backupRoot, StringBuilder log)
        {
            // 先把要保留的数据摘出来，再换着色器 —— 换完之后旧属性就读不到了。
            var textures = new Dictionary<string, Texture>();
            var textureScales = new Dictionary<string, Vector2>();
            var textureOffsets = new Dictionary<string, Vector2>();
            var floats = new Dictionary<string, float>();
            var colors = new Dictionary<string, Color>();
            var keywords = new HashSet<string>(mat.shaderKeywords);

            foreach (string prop in TexturePropertiesToCopy)
            {
                if (!mat.HasProperty(prop))
                {
                    continue;
                }

                Texture tex = mat.GetTexture(prop);
                if (tex != null)
                {
                    textures[prop] = tex;
                    textureScales[prop] = mat.GetTextureScale(prop);
                    textureOffsets[prop] = mat.GetTextureOffset(prop);
                }
            }

            foreach (string prop in FloatPropertiesToCopy)
            {
                if (mat.HasProperty(prop))
                {
                    floats[prop] = mat.GetFloat(prop);
                }
            }

            foreach (string prop in ColorPropertiesToCopy)
            {
                if (mat.HasProperty(prop))
                {
                    colors[prop] = mat.GetColor(prop);
                }
            }

            float renderMode = mat.HasProperty("_Mode") ? mat.GetFloat("_Mode") : 0f;

            // 备份原材质文件，便于逐个回退
            string backupName = path.Replace('/', '_').Replace('\\', '_');
            string backupPath = Path.Combine(backupRoot, backupName);
            string sourceFullPath = Path.Combine(
                Directory.GetParent(Application.dataPath).FullName, path);
            if (File.Exists(sourceFullPath))
            {
                File.Copy(sourceFullPath, backupPath, true);
            }

            mat.shader = toonShader;

            // ---- 贴图 ----
            foreach (var kv in textures)
            {
                string target = RenamedProperties.TryGetValue(kv.Key, out string renamed)
                    ? renamed
                    : kv.Key;

                if (!mat.HasProperty(target))
                {
                    continue;
                }

                mat.SetTexture(target, kv.Value);
                // _BaseMap 在 URP 里靠 _BaseMap_ST 控制缩放平移
                mat.SetTextureScale(target, textureScales[kv.Key]);
                mat.SetTextureOffset(target, textureOffsets[kv.Key]);
            }

            // ---- 浮点 ----
            foreach (var kv in floats)
            {
                if (mat.HasProperty(kv.Key))
                {
                    mat.SetFloat(kv.Key, kv.Value);
                }
            }

            // ---- 颜色 ----
            foreach (var kv in colors)
            {
                string target = RenamedProperties.TryGetValue(kv.Key, out string renamed)
                    ? renamed
                    : kv.Key;

                if (mat.HasProperty(target))
                {
                    mat.SetColor(target, kv.Value);
                }
            }

            // ---- Alpha 裁切 ----
            bool alphaClipping =
                keywords.Contains("_ALPHATEST_ON") ||
                Mathf.Approximately(renderMode, 1f) ||
                (floats.ContainsKey("_Cutoff") && floats["_Cutoff"] < 0.999f);

            if (alphaClipping)
            {
                mat.EnableKeyword("_UseAlphaClipping");
                mat.SetFloat("_UseAlphaClipping", 1f);
                if (!mat.HasProperty("_Cutoff"))
                {
                    mat.SetFloat("_Cutoff", 0.5f);
                }
            }
            else
            {
                mat.DisableKeyword("_UseAlphaClipping");
                mat.SetFloat("_UseAlphaClipping", 0f);
            }

            // ---- 半透明 ----
            bool transparent =
                keywords.Contains("_ALPHABLEND_ON") ||
                keywords.Contains("_ALPHAPREMULTIPLY_ON") ||
                Mathf.Approximately(renderMode, 2f) ||
                Mathf.Approximately(renderMode, 3f) ||
                mat.renderQueue >= 3000;

            if (transparent)
            {
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.SetOverrideTag("RenderType", "Transparent");
            }
            else
            {
                mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);
                mat.renderQueue = -1;
                mat.SetOverrideTag("RenderType", "Opaque");
            }

            string grassTag = string.Empty;
            if (path.Replace('\\', '/').Contains("/grass/"))
            {
                mat.SetFloat("_UseGrassWind", 1f);
                grassTag = "  [草地风动画已开启]";
            }

            log.AppendLine(
                $"{path}\t裁切={(alphaClipping ? "是" : "否")}\t半透明={(transparent ? "是" : "否")}{grassTag}");
        }

        static void WriteLog(
            string projectRoot, StringBuilder log,
            List<string> converted, List<string> skipped)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 三渲二材质转换日志");
            sb.AppendLine($"# 生成时间：{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine($"## 已转换（{converted.Count} 个）");
            foreach (string line in converted)
            {
                sb.AppendLine(line);
            }

            sb.AppendLine();
            sb.AppendLine($"## 跳过（{skipped.Count} 个，已是非内置管线着色器）");
            foreach (string line in skipped)
            {
                sb.AppendLine(line);
            }

            sb.AppendLine();
            sb.AppendLine("## 逐材质属性处理明细");
            sb.Append(log);

            string logPath = Path.Combine(projectRoot, LogPath);
            Directory.CreateDirectory(Path.GetDirectoryName(logPath));
            File.WriteAllText(logPath, sb.ToString(), new UTF8Encoding(false));
        }
    }
}
