using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using MyEnums;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "GameSceneSO", menuName = "GameSceneSO/SceneSO", order = 0)]
public class GameSceneSO : ScriptableObject {
    // 注意：这不是存档键。ID 由 OnValidate 生成且不落盘，跨会话/打包会变，不要用它索引存档。
    public string ID;

#if UNITY_EDITOR
    [Tooltip("拖入场景文件（.unity），保存时自动同步到 sceneName")]
    public SceneAsset sceneAsset;
#endif

    [Tooltip("运行时实际加载的场景名。编辑器下由 sceneAsset 自动同步，一般无需手动修改")]
    public string sceneName;

    public SceneType sceneType;
    public Vector3 initialPosition;

    /// <summary>
    /// 存档用的场景稳定标识：编辑器下取场景文件（.unity）的 GUID，随资产落盘，跨会话与打包一致，
    /// 与旧 Addressables 方案 sceneReference.AssetGUID 的取值完全一致，旧存档仍可解析。
    /// 存档 Data.sceneIDAndPlayerPos.sceneID 存的是它，读档时由 SceneDataForSave.gameScenes 反查回本资产。
    /// </summary>
    public string SaveKey
    {
        get
        {
#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(sceneName))
            {
                string[] guids = AssetDatabase.FindAssets($"{sceneName} t:SceneAsset");
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(path) &&
                        System.IO.Path.GetFileNameWithoutExtension(path) == sceneName)
                        return guid;
                }
            }
#endif
            return name;
        }
    }

    void OnValidate()
    {
        if (string.IsNullOrEmpty(ID))
            ID = Guid.NewGuid().ToString();

#if UNITY_EDITOR
        if (sceneAsset != null)
        {
            string assetName = sceneAsset.name;
            if (assetName != sceneName)
                sceneName = assetName;
        }
#endif
    }
}
