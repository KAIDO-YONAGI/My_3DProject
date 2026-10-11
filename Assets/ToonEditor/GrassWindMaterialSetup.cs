using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Core.Editor.Toon
{
    /// <summary>
    /// 为草地材质生成开启了顶点风动画的三渲二材质副本。
    ///
    /// 用法：菜单 My_3DProject/三渲二/生成草地风动画材质
    ///
    /// 背景（已核实的工程事实）：
    ///     Assets/Materials/ForestMaterials/grass/Materials/ 下的 9 个草地材质，
    ///     迁移前使用 `Legacy Shaders/Transparent/Cutout/Diffuse` 这个内置管线着色器。
    ///     URP 下它不可用，材质在迁移时已被 ToonMaterialConverter 统一换成 ToonLit。
    ///     本工具在此基础上再复制一份，把顶点风动画打开。
    ///
    ///     这 9 个材质目前没有被工程里任何场景、Prefab 或 TerrainLayer 引用，
    ///     两个玩法场景的 Terrain 也没有绘制任何图层（TerrainData 里没有外部资源引用）。
    ///     也就是说：草地当前并不会渲染出来。
    ///     本工具先把材质准备好，等你把草画到地形上时可以直接挂用。
    ///
    /// 不依赖任何外部文件：直接扫描草地材质目录，逐个生成对应副本。
    /// </summary>
    public static class GrassWindMaterialSetup
    {
        const string ShaderName = "My_3DProject/Toon/ToonLit (NiloCat URP)";

        /// <summary>草地材质所在目录，相对工程根目录。</summary>
        const string GrassFolder = "Assets/Materials/ForestMaterials/grass/Materials";

        /// <summary>生成副本的子目录名。</summary>
        const string ToonSubFolder = "Toon";

        // 风动画默认参数。强度单位是世界坐标位移量，速度是正弦角频率，频率影响相邻草叶的相位差。
        const float WindStrength = 0.08f;
        const float WindSpeed = 1.5f;
        const float WindFrequency = 1.0f;

        [MenuItem("My_3DProject/三渲二/生成草地风动画材质", priority = 102)]
        public static void CreateGrassWindMaterials()
        {
            Shader toonShader = Shader.Find(ShaderName);
            if (toonShader == null)
            {
                Debug.LogError($"[三渲二] 找不到着色器 \"{ShaderName}\"，请先确认 URP 与 ToonLit.shader 均已导入。");
                return;
            }

            if (!AssetDatabase.IsValidFolder(GrassFolder))
            {
                Debug.LogError($"[三渲二] 找不到草地材质目录：{GrassFolder}");
                return;
            }

            List<string> sourcePaths = FindGrassMaterials();
            if (sourcePaths.Count == 0)
            {
                Debug.LogWarning($"[三渲二] {GrassFolder} 下没有找到草地材质。");
                return;
            }

            string toonFolder = GrassFolder + "/" + ToonSubFolder;
            if (!AssetDatabase.IsValidFolder(toonFolder))
            {
                AssetDatabase.CreateFolder(GrassFolder, ToonSubFolder);
            }

            int created = 0;
            int skipped = 0;
            int failed = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (string sourcePath in sourcePaths)
                {
                    string stem = Path.GetFileNameWithoutExtension(sourcePath);
                    string targetPath = toonFolder + "/" + stem + "_Toon.mat";

                    Material source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
                    if (source == null)
                    {
                        Debug.LogWarning($"[三渲二] 载入失败，跳过：{sourcePath}");
                        failed++;
                        continue;
                    }

                    // 已经是 ToonLit 的材质说明上一次已经跑过，直接刷新风参数即可，不重复建副本。
                    bool exists = AssetDatabase.LoadAssetAtPath<Material>(targetPath) != null;
                    if (!exists)
                    {
                        // 走 AssetDatabase 复制，贴图引用与 _BaseMap_ST 等属性会一并带过来。
                        if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
                        {
                            Debug.LogWarning($"[三渲二] 复制失败：{sourcePath} -> {targetPath}");
                            failed++;
                            continue;
                        }
                    }

                    Material toon = AssetDatabase.LoadAssetAtPath<Material>(targetPath);
                    if (toon == null)
                    {
                        failed++;
                        continue;
                    }

                    toon.shader = toonShader;
                    toon.SetFloat("_UseGrassWind", 1f);
                    toon.SetFloat("_GrassWindStrength", WindStrength);
                    toon.SetFloat("_GrassWindSpeed", WindSpeed);
                    toon.SetFloat("_GrassWindFrequency", WindFrequency);
                    // _GrassWindDirection 是 Vector 属性，必须用 SetVector。
                    // 风朝 +X 吹，Z 分量为 0，第四位是 w 占位。
                    toon.SetVector("_GrassWindDirection", new Vector4(1f, 0f, 0f, 1f));
                    EditorUtility.SetDirty(toon);

                    if (exists)
                    {
                        skipped++;
                    }

                    created++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[三渲二] 草地风动画材质处理完成：共 {created} 个（新建 {created - skipped}，已存在刷新 {skipped}），失败 {failed} 个。");
            sb.AppendLine($"  输出目录：{toonFolder}/");
            sb.AppendLine();
            sb.AppendLine("提醒：这些草地材质目前没有被任何场景或地形引用，");
            sb.AppendLine("需要把草画到地形（Terrain 的 Paint Texture / Paint Details）上才会出现在画面里。");
            sb.AppendLine($"画好之后，把地形图层指向 {toonFolder}/ 下的材质即可。");

            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// 扫描草地材质目录，返回除 Toon 子目录之外的所有 .mat 路径。
        /// 限定在当前目录且显式跳过 Toon 子目录，避免把上一次生成的副本再当源材质。
        /// </summary>
        static List<string> FindGrassMaterials()
        {
            var result = new List<string>();
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string absoluteFolder = Path.Combine(projectRoot, GrassFolder);

            if (!Directory.Exists(absoluteFolder))
            {
                return result;
            }

            string[] files = Directory.GetFiles(absoluteFolder, "*.mat", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < files.Length; i++)
            {
                string relative = GrassFolder + "/" + Path.GetFileName(files[i]);
                if (relative.Replace('\\', '/').Contains("/" + ToonSubFolder + "/"))
                {
                    continue;
                }

                result.Add(relative);
            }

            result.Sort();
            return result;
        }
    }
}
