using System.Collections;
using System.Collections.Generic;
using Core.Scripts.SO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.Scripts.Scene
{
    public class InitialLoad : MonoBehaviour
    {
        [Tooltip("启动时以叠加模式加载并持续保留的场景。")]
        public List<GameSceneSO> persistentScenes = new List<GameSceneSO>();

        private void Awake()
        {
            ValidateReferences();
            PersistentSceneRegistry.Register(persistentScenes);
            StartCoroutine(LoadPersistentScenes());
        }

        // 常驻场景清单是必填配置：缺失时按项报错，加载流程仍按现有条目继续。
        private void ValidateReferences()
        {
            if (persistentScenes == null || persistentScenes.Count == 0)
            {
                Debug.LogError($"[{nameof(InitialLoad)}] persistentScenes 未配置：常驻场景不会加载。", this);
                return;
            }

            for (int index = 0; index < persistentScenes.Count; index++)
            {
                if (persistentScenes[index] == null)
                {
                    Debug.LogError($"[{nameof(InitialLoad)}] persistentScenes[{index}] 为空：该条目会被跳过。", this);
                }
            }
        }

        private IEnumerator LoadPersistentScenes()
        {
            foreach (GameSceneSO sceneReference in persistentScenes)
            {
                if (sceneReference == null)
                    continue;

                string sceneName = sceneReference.sceneName;
                if (string.IsNullOrWhiteSpace(sceneName))
                {
                    Debug.LogError($"InitialLoad cannot load '{sceneReference.name}' because it has no scene assigned.");
                    continue;
                }

                UnityEngine.SceneManagement.Scene loadedScene = SceneManager.GetSceneByName(sceneName);
                if (loadedScene.IsValid() && loadedScene.isLoaded)
                    continue;

                if (!Application.CanStreamedLevelBeLoaded(sceneName))
                {
                    Debug.LogError($"InitialLoad cannot load scene '{sceneName}'. Assign a scene in its GameSceneSO first.");
                    continue;
                }

                AsyncOperation loadOperation = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
                while (loadOperation != null && !loadOperation.isDone)
                    yield return null;
            }
        }
    }
}
