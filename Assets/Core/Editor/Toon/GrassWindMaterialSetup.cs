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
    ///     在迁移前使用已被 Unity 2022 废弃的 Nature/SpeedTree 内置着色器（带风动画与弯曲效果）。
    ///     迁到 URP 后该着色器不可用，风动画需要由 ToonLit 的 _UseGrassWind 接管。
    ///
    ///     但这 9 个材质目前没有被工程里任何场景、Prefab 或 TerrainLayer 引用，
    ///     两个玩法场景的 Terrain 也没有绘制任何图层（TerrainData 里没有外部资源引用）。
    ///     也就是说：草地当前并不会渲染出来。
    ///     本工具先把材质准备好，等你把草画到地形上时可以直接挂用。
    ///
    /// 依赖：Tools/ToonMigration/grass-wind-map.json（由 Build-GrassWindMap.ps1 生成）。
    /// </summary>
    public static class GrassWindMaterialSetup
    {
        const string ShaderName = "My_3DProject/Toon/ToonLit (NiloCat URP)";
        const string MapRelativePath = "Tools/ToonMigration/grass-wind-map.json";

        [MenuItem("My_3DProject/三渲二/生成草地风动画材质", priority = 102)]
        public static void CreateGrassWindMaterials()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string mapPath = Path.Combine(projectRoot, MapRelativePath);

            if (!File.Exists(mapPath))
            {
                Debug.LogError(
                    $"[三渲二] 找不到草地映射文件：{MapRelativePath}\n" +
                    "请先在工程根目录执行：\n" +
                    "  pwsh -File Tools/ToonMigration/Build-MaterialMap.ps1\n" +
                    "  pwsh -File Tools/ToonMigration/Build-GrassWindMap.ps1");
                return;
            }

            GrassWindMap map = JsonUtility.FromJson<GrassWindMap>(File.ReadAllText(mapPath));
            if (map == null || map.grasses == null || map.grasses.Length == 0)
            {
                Debug.LogError("[三渲二] 草地映射文件里没有条目。");
                return;
            }

            Shader toonShader = Shader.Find(ShaderName);
            if (toonShader == null)
            {
                Debug.LogError($"[三渲二] 找不到着色器 \"{ShaderName}\"，请先确认 URP 与 ToonLit.shader 均已导入。");
                return;
            }

            int created = 0;
            int missing = 0;

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (GrassEntry entry in map.grasses)
                {
                    // 源材质已经被 ToonMaterialConverter 换成了 ToonLit，直接复制即可继承全部属性。
                    Material source = AssetDatabase.LoadAssetAtPath<Material>(entry.oldPath);
                    if (source == null)
                    {
                        Debug.LogWarning($"[三渲二] 找不到草地材质：{entry.oldPath}");
                        missing++;
                        continue;
                    }

                    string targetDir = Path.GetDirectoryName(entry.newPath).Replace('\\', '/');
                    if (!AssetDatabase.IsValidFolder(targetDir))
                    {
                        CreateFolderRecursive(targetDir);
                    }

                    // 用 copy 走 AssetDatabase，贴图与 _BaseMap_ST 等属性会一并带过来。
                    if (!AssetDatabase.CopyAsset(entry.oldPath, entry.newPath))
                    {
                        Debug.LogWarning($"[三渲二] 复制失败：{entry.oldPath} -> {entry.newPath}");
                        missing++;
                        continue;
                    }

                    Material toon = AssetDatabase.LoadAssetAtPath<Material>(entry.newPath);
                    if (toon == null)
                    {
                        missing++;
                        continue;
                    }

                    toon.shader = toonShader;
                    toon.SetFloat("_UseGrassWind", 1f);
                    toon.SetFloat("_GrassWindStrength", map.grassWindStrength);
                    toon.SetFloat("_GrassWindSpeed", map.grassWindSpeed);
                    toon.SetFloat("_GrassWindFrequency", map.grassWindFrequency);
                    // _GrassWindDirection 是 Vector 属性，必须用 SetVector。
                    // 只把风往 +X 方向吹，Z 分量为 0，第四位是 w 占位。
                    toon.SetVector("_GrassWindDirection", new Vector4(1f, 0f, 0f, 1f));
                    EditorUtility.SetDirty(toon);

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
            sb.AppendLine($"[三渲二] 草地风动画材质生成完成：成功 {created} 个，失败 {missing} 个。");
            sb.AppendLine();
            sb.AppendLine("提醒：这些草地材质目前没有被任何场景或地形引用，");
            sb.AppendLine("需要把草画到地形（Terrain 的 Paint Texture / Paint Details）上才会出现在画面里。");
            sb.AppendLine("画好之后，把地形图层指向 Assets/Materials/ForestMaterials/grass/Materials/Toon/ 下的材质即可。");

            Debug.Log(sb.ToString());
        }

        static void CreateFolderRecursive(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            string leaf = Path.GetFileName(folder);

            if (!AssetDatabase.IsValidFolder(parent))
            {
                CreateFolderRecursive(parent);
            }

            AssetDatabase.CreateFolder(parent, leaf);
        }

        [System.Serializable]
        class GrassWindMap
        {
            public string toonShaderName;
            public float grassWindStrength;
            public float grassWindSpeed;
            public float grassWindFrequency;
            public int grassCount;
            public GrassEntry[] grasses;
        }

        [System.Serializable]
        class GrassEntry
        {
            public string oldPath;
            public string newPath;
            public string name;
            public string oldShader;
        }
    }
}
