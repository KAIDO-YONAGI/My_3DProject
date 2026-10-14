using System.Collections;
using System.Collections.Generic;
using Core.Scripts.GamePlayFrameWork.Scripts.SO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.Scripts.GamePlayFrameWork.Scripts.Scene
{
    public class InitialLoad : MonoBehaviour
    {
        [Tooltip("启动时以叠加模式加载并持续保留的场景。")]
        public List<GameSceneSO> persistentScenes = new List<GameSceneSO>();

        private void Awake()
        {
            PersistentSceneRegistry.Register(persistentScenes);
            StartCoroutine(LoadPersistentScenes());
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
