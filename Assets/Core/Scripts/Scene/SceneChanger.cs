using System.Collections;
using System.Collections.Generic;
using Core.Scripts.Common;
using Core.Scripts.SO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.Scripts.Scene
{
    /// <summary>
    /// 场景切换管理器：负责场景组的加载、卸载和过渡动画。
    /// 切换的唯一入口是 <see cref="RequestSceneLoad"/>：先广播 loadEventSO，
    /// 让订阅方在切换开始前同步收尾，再执行切换流程；先后顺序就是入口内两条语句的顺序。
    /// 采用多场景加载：每次请求针对一个完整 Additive 场景组，整组卸载旧内容场景后按顺序加载；
    /// PersistentSceneRegistry 注册的常驻场景不参与卸载。
    /// </summary>
    public class SceneChanger : YSingleton<SceneChanger>
    {
        /// <summary>玩家初始位置</summary>
        [SerializeField] private Vector3 initialPosition = Vector3.zero;
        [Tooltip("启动时自动加载的初始场景组。按列表顺序以 Additive 模式加载，通常配置为菜单场景组。")] [SerializeField]
        private List<GameSceneSO> firstSceneToLoad = new();

        [Header("事件")]
        [SerializeField] private SceneLoadEventSO loadEventSO;
        /// <summary>重试请求事件：死亡后重载当前场景组，由 RetryButton 广播</summary>
        [SerializeField] private VoidEventSO retryEventSO;
        [SerializeField] private SceneLoadedEventSO sceneLoadedEvent;
        [SerializeField] private PlayerSpawnEventSO playerSpawnEvent;
        
        [Header("过渡设置")]
        [SerializeField] private float fadeDuration = 1f;
        /// <summary>过渡动画播放器数组</summary>
        [SerializeField] private CanvasGroup fadeCanva;
        [SerializeField] private Animator[] transitionImagesDuringFade;

        /// <summary>当前已加载的内容场景组（不含常驻场景）</summary>
        private readonly List<LoadedScene> currentScenes = new();

        /// <summary>关联场景配置资产与其当前加载的 Unity 场景实例。</summary>
        private class LoadedScene
        {
            public readonly GameSceneSO Asset;
            public readonly UnityEngine.SceneManagement.Scene Instance;

            public LoadedScene(GameSceneSO asset, UnityEngine.SceneManagement.Scene instance)
            {
                Asset = asset;
                Instance = instance;
            }
        }

        /// <summary>最近一次整组加载完成后的首个内容场景，供 GetCurrentScene/GetCurrentGameScene 回读</summary>
        private GameSceneSO currentScene;

        /// <summary>已加载的场景对象</summary>
        private UnityEngine.SceneManagement.Scene loadedScene;

        private bool isInitialScene = true;

        /// <summary>加载进行中标记：请求→淡入→卸载→加载→完成的整个窗口内为 true，防止双请求竞态。</summary>
        private bool isLoading;

        /// <summary>
        /// 获取当前活动场景
        /// </summary>
        /// <returns>当前场景对象</returns>
        public UnityEngine.SceneManagement.Scene GetCurrentScene()
        {
            return loadedScene.IsValid() && loadedScene.isLoaded
                ? loadedScene
                : SceneManager.GetActiveScene();
        }

        // 供存档系统在切场前读取当前场景 SO。
        public GameSceneSO GetCurrentGameScene()
        {
            return currentScenes.Count > 0 ? currentScenes[0].Asset : null;
        }

        /// <summary>
        /// 当前已加载的场景组副本（整组重载用；
        /// 返回副本是因为本类加载流程中会 Clear 原列表，不能把原引用传回去）。
        /// </summary>
        public List<GameSceneSO> GetCurrentScenes()
        {
            var scenes = new List<GameSceneSO>(currentScenes.Count);
            foreach (LoadedScene scene in currentScenes)
                scenes.Add(scene.Asset);
            return scenes;
        }

        /// <summary>
        /// 唤醒时校验必填引用并设置玩家初始位置
        /// </summary>
        protected override void OnSingletonInitialized()
        {
            ValidateReferences();
        }

        // 必填引用在单例注册后只校验一次：缺失时按字段名断言，后续调用点直接解引用。
        private void ValidateReferences()
        {
            Debug.Assert(fadeCanva != null,
                $"{nameof(fadeCanva)} 未指定：切换时的过渡遮罩无法播放。", this);
            Debug.Assert(loadEventSO != null,
                $"{nameof(loadEventSO)} 未指定：场景切换请求无法广播。", this);
            Debug.Assert(retryEventSO != null,
                $"{nameof(retryEventSO)} 未指定：重试的整组重载无法订阅。", this);
            Debug.Assert(sceneLoadedEvent != null,
                $"{nameof(sceneLoadedEvent)} 未指定：画布与加载音效收不到场景加载完成通知。", this);
            Debug.Assert(playerSpawnEvent != null,
                $"{nameof(playerSpawnEvent)} 未指定：场景加载完成后玩家收不到出生位置。", this);

            // 启动场景组必填：容器为空时启动后没有内容场景。
            Debug.Assert(firstSceneToLoad != null && firstSceneToLoad.Count > 0,
                $"{nameof(firstSceneToLoad)} 未配置：启动后不会加载任何内容场景。", this);

            for (int index = 0; index < firstSceneToLoad.Count; index++)
            {
                Debug.Assert(firstSceneToLoad[index] != null,
                    $"{nameof(firstSceneToLoad)}[{index}] 为空：该场景组条目会被跳过。", this);
            }

            // 过渡图片允许留空；配置后每一项都必须有效。
            if (transitionImagesDuringFade != null)
            {
                for (int index = 0; index < transitionImagesDuringFade.Length; index++)
                {
                    Debug.Assert(transitionImagesDuringFade[index] != null,
                        $"{nameof(transitionImagesDuringFade)}[{index}] 为空：该过渡图片动画不会播放。", this);
                }
            }

        }

        /// <summary>
        /// 启用时订阅重试事件
        /// </summary>
        private void OnEnable()
        {
            // 重试是外部输入，经事件订阅；加载执行由 RequestSceneLoad 直接调用
            retryEventSO.VoidEvent += OnRetryRequest;
        }

        private void OnDisable()
        {
            retryEventSO.VoidEvent -= OnRetryRequest;
        }

        /// <summary>
        /// 场景切换的唯一入口：先广播，后执行。
        /// Raise 同步调用所有订阅方的处理器并等它们全部返回，订阅方在切换开始前完成收尾；
        /// 随后才执行本类的切换流程，两步的先后就是本方法内语句的先后。
        /// 触发场景切换必须调用本方法。直接 Raise loadEventSO 只会通知订阅方，不会切场景。
        /// </summary>
        public void RequestSceneLoad(List<GameSceneSO> scenes, Vector3 position, bool isToFade)
        {
            loadEventSO.RaiseLoadRequestEvent(scenes, position, isToFade);
            OnLoadRequestEvent(scenes, position, isToFade);
        }

        /// <summary>
        /// 首个场景组请求放在 Start：同批所有 Awake/OnEnable 已跑完，
        /// TimeManager 实例与 UIManager 的事件订阅必然就绪，广播不漏听众。
        /// </summary>
        private void Start()
        {
            RequestSceneLoad(firstSceneToLoad, Vector3.zero, false);
        }

        /// <summary>
        /// 播放过渡动画
        /// </summary>
        /// <param name="name">动画状态名称（FadeIn/FadeOut）</param>
        private void PlayLoadingAnimation(string name)
        {
            fadeCanva.alpha = 1;
            if (transitionImagesDuringFade == null)
            {
                return;
            }

            foreach (Animator transitionImage in transitionImagesDuringFade)
            {
                transitionImage.Play(name);
            }
        }

        /// <summary>
        /// 加载请求的执行段，由 RequestSceneLoad 在广播完成后调用。
        /// 检查请求后锁定切换状态、暂停操作与玩法收尾，并启动切场总协程。
        /// </summary>
        /// <param name="scenes">目标场景组</param>
        /// <param name="newPosition">玩家新位置</param>
        /// <param name="isToFade">是否显示过渡动画</param>
        private void OnLoadRequestEvent(List<GameSceneSO> scenes, Vector3 newPosition, bool isToFade)
        {
            if (scenes == null || scenes.Count == 0)
            {
#if UNITY_EDITOR
                Debug.LogWarning("[SceneChanger] 收到空场景组加载请求，已忽略。");
#endif
                return;
            }

            // 加载窗口内拒绝新请求，避免同一时间启动多条卸载/加载协程。
            if (isLoading)
            {
#if UNITY_EDITOR
                Debug.LogWarning($"[SceneChanger] 加载进行中，忽略新的加载请求: {scenes[0].name}");
#endif
                return;
            }

            isLoading = true;

            ForbidInput();
            TimeManager.RequiredInstance.PauseGame();

            OnBeforeSceneUnload(); // 玩法侧收尾钩子（如回血）

            Vector3 targetPosition = newPosition == Vector3.zero ? GetInitialPosition(scenes) : newPosition;
            //如果传入位置为零向量，则使用场景组预设的初始位置
            if (isToFade && !isInitialScene)
            {
                PlayLoadingAnimation("FadeIn");
            }

            StartCoroutine(ChangeScenesRoutine(scenes, targetPosition, isToFade)); //启动切场总协程
        }

        /// <summary>
        /// 切场总流程：先卸载当前内容场景组，再加载目标场景组。
        /// 每次 yield return 都等待整个子协程结束，包含子协程内部的跨帧等待。
        /// </summary>
        /// <param name="targetScenes">目标场景组</param>
        /// <param name="targetPosition">目标场景组加载后的玩家位置</param>
        /// <param name="targetFade">是否在加载完成时播放淡出动画</param>
        private IEnumerator ChangeScenesRoutine(
            List<GameSceneSO> targetScenes,
            Vector3 targetPosition,
            bool targetFade)
        {
            yield return UnloadCurrentScenes(targetFade);
            yield return LoadScenesRoutine(
                targetScenes, targetPosition, targetFade);
        }

        /// <summary>场景组的默认出生点：取首个有效场景 SO 的初始位置。</summary>
        private Vector3 GetInitialPosition(List<GameSceneSO> scenes)
        {
            foreach (GameSceneSO scene in scenes)
            {
                if (scene != null)
                    return scene.initialPosition;
            }

            return Vector3.zero;
        }

        /// <summary>
        /// 玩法侧收尾钩子：框架不依赖具体玩法，切场前的回血、状态复位等
        /// 由宿主项目继承 SceneChanger 重写本方法实现。
        /// </summary>
        protected virtual void OnBeforeSceneUnload()
        {
        }

        /// <summary>
        /// 重试请求回调：重载当前所在场景组。
        /// 位置传零向量，复用 OnLoadRequestEvent 的初始位置兜底；
        /// 回血、复活玩家、收起 GameOver 面板均由本次加载流程统一处理，此处不再重复。
        /// </summary>
        private void OnRetryRequest()
        {
            RequestSceneLoad(GetCurrentScenes(), Vector3.zero, true);
        }

        /// <summary>
        /// 卸载当前内容场景组，保留注册的常驻场景。
        /// 逐个等待异步卸载完成，随后按需补足过渡时长；过渡时长从开始卸载起算。
        /// </summary>
        /// <param name="targetFade">本次切换是否播放过渡动画</param>
        private IEnumerator UnloadCurrentScenes(bool targetFade)
        {
            float time = Time.realtimeSinceStartup;

            for (int i = currentScenes.Count - 1; i >= 0; i--)
            {
                LoadedScene scene = currentScenes[i];
                UnityEngine.SceneManagement.Scene loaded = scene.Instance;

                if (PersistentSceneRegistry.IsPersistent(scene.Asset) ||
                    PersistentSceneRegistry.IsPersistent(loaded.name))
                {
#if UNITY_EDITOR
                    Debug.LogWarning(
                        $"[SceneChanger] Skipped unload of registered persistent scene '{loaded.name}'.");
#endif
                    currentScenes.RemoveAt(i);
                    continue;
                }

                if (loaded.IsValid() && loaded.isLoaded)
                    yield return SceneManager.UnloadSceneAsync(loaded);

                if (loaded.IsValid() && loaded.isLoaded)
                {
#if UNITY_EDITOR
                    Debug.LogError($"[SceneChanger] Scene '{loaded.name}' is still loaded. Scene switch aborted.");
#endif
                    if (targetFade && !isInitialScene)
                        PlayLoadingAnimation("FadeOut");
                    AllowInput();
                    TimeManager.RequiredInstance.ForceResumeGame();
                    isLoading = false;
                    yield break;
                }

                currentScenes.RemoveAt(i);
            }

            float subTime = Time.realtimeSinceStartup - time;

            // 无过渡的请求（如首次加载）不需要把卸载段补足到过渡时长。
            if (targetFade)
                yield return new WaitForSecondsRealtime(fadeDuration - subTime > 0 ? fadeDuration - subTime : 0);
        }

        /// <summary>
        /// 由切场总流程在卸载协程结束后调用，按列表顺序以 Additive 模式加载场景组，
        /// 全部完成后调用 OnLoadCompleted。
        /// 禁止裸调用：必须经 RequestSceneLoad 入口先广播订阅方再执行，
        /// 否则订阅者收不到切换通知。
        /// </summary>
        private IEnumerator LoadScenesRoutine(
            List<GameSceneSO> targetScenes,
            Vector3 targetPosition,
            bool targetFade)
        {
            GameSceneSO firstLoaded = null;
            foreach (GameSceneSO scene in targetScenes)
            {
                if (scene == null || string.IsNullOrEmpty(scene.sceneName))
                    continue;

                if (!Application.CanStreamedLevelBeLoaded(scene.sceneName))
                {
#if UNITY_EDITOR
                    Debug.LogError(
                        $"[SceneChanger] Scene '{scene.sceneName}' ({scene.name}) is not in Build Settings; " +
                        "add it via File > Build Settings before loading. Skipped.");
#endif
                    continue;
                }

                UnityEngine.SceneManagement.Scene loaded = default;
                UnityEngine.Events.UnityAction<UnityEngine.SceneManagement.Scene, LoadSceneMode> onSceneLoaded =
                    (instance, mode) =>
                    {
                        if (mode == LoadSceneMode.Additive && instance.name == scene.sceneName)
                            loaded = instance;
                    };

                // 保存本次加载通知中的实际实例，避免同名的未加载条目干扰查找。
                SceneManager.sceneLoaded += onSceneLoaded;
                try
                {
                    AsyncOperation loadOperation =
                        SceneManager.LoadSceneAsync(scene.sceneName, LoadSceneMode.Additive);
                    while (loadOperation != null && !loadOperation.isDone)
                        yield return null;
                }
                finally
                {
                    SceneManager.sceneLoaded -= onSceneLoaded;
                }

                if (!loaded.IsValid() || !loaded.isLoaded)
                {
#if UNITY_EDITOR
                    Debug.LogError($"[SceneChanger] Scene '{scene.sceneName}' did not finish loading.");
#endif
                    continue;
                }

                if (firstLoaded == null)
                {
                    firstLoaded = scene;
                    loadedScene = loaded;
                }

                currentScenes.Add(new LoadedScene(scene, loaded));
            }

            OnLoadCompleted(firstLoaded, targetFade, targetPosition);
        }


        /// <summary>
        /// 场景组加载完成回调
        /// 更新当前场景引用，播放淡出动画
        /// </summary>
        /// <param name="loadedTarget">本次加载的场景组首个场景</param>
        /// <param name="targetFade">本次加载是否需要播放淡出动画</param>
        /// <param name="targetPosition">加载后再设置玩家位置</param>
        private void OnLoadCompleted(GameSceneSO loadedTarget, bool targetFade, Vector3 targetPosition)
        {
            // 不变量：currentScene 赋值必须在 sceneLoadedEvent 广播之前——
            // 订阅方（画布管理器/存档系统等）经 GetCurrentGameScene() 回读
            currentScene = loadedTarget;
            if (targetFade && !isInitialScene)
            {
                PlayLoadingAnimation("FadeOut");
            }

            isInitialScene = false;
            sceneLoadedEvent.RaiseSceneLoadedEvent(currentScene);
            AllowInput();
            TimeManager.RequiredInstance.ForceResumeGame();
            isLoading = false; // 全部完成后才解锁，允许下一次加载请求
            // 出生位置改为事件广播：场景加载完成后由玩家预制件上的接收器自行应用。
            playerSpawnEvent.OnPlayerSpawn(targetPosition != Vector3.zero?targetPosition:initialPosition);
        }

        private void ForbidInput()
        {
            // 框架不依赖具体玩法；宿主项目可重写此方法禁用玩家输入。
        }

        private void AllowInput()
        {
            // 框架不依赖具体玩法；宿主项目可重写此方法恢复玩家输入。
        }
    }
}