using System.Collections.Generic;
using Core.Scripts.Camera.Runtime;
using Core.Scripts.Character.Control;
using Core.Scripts.Character.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.Scripts.Networking
{
    /// <summary>
    /// 在 PersistentScene 中配置共用角色资源，按网络玩家身份装配客户端表现。
    /// 每个玩家的视觉实例、角色编号、来源 Prefab 和相机组件统一保存在表现状态中。
    /// </summary>
    public class NetworkCharacterManager : MonoBehaviour
    {
        /// <summary>网络玩家共用的角色表现管理器。</summary>
        public static NetworkCharacterManager Instance { get; private set; }

        [Header("角色资源")]
        // 下标对应角色编号，本地拥有者从这里取得模型、动画配置和本地相机。
        [Tooltip("每个编号配置一个完整角色 Prefab。单机、本地联机和远程联机共用它，运行时按身份启停组件。")]
        [SerializeField] private GameObject[] characters = new GameObject[0];
        [Tooltip("服务端生成玩家时写入 SyncVar 的初始角色编号，对应角色资源条目下标。")]
        [SerializeField, Min(0)] private int defaultCharacterId;

        [Header("场景单机角色")]
        [Tooltip("常驻场景中的单机角色实例。联机时停用，单机时启用。")]
        [SerializeField] private GameObject standalonePlayer;

        [Header("场景单机相机")]
        [Tooltip("需要随单机模式启停的场景 Camera，直接引用组件。")]
        [SerializeField] private UnityEngine.Camera[] standaloneCameras = new UnityEngine.Camera[0];
        [Tooltip("需要随单机模式启停的场景 ThirdPersonCamera。")]
        [SerializeField] private ThirdPersonCamera[] standaloneCameraControllers = new ThirdPersonCamera[0];
        [Tooltip("需要随单机模式启停的场景 AudioListener。")]
        [SerializeField] private AudioListener[] standaloneListeners = new AudioListener[0];

        // 当前离线模式使用的角色编号，-1 表示场景实例对应的编号尚未记录。
        private int standaloneCharacterId = -1;

        // 单个玩家的装配结果，组件缓存与视觉实例共用生命周期。
        private class CharacterPresentation
        {
            public GameObject Instance;
            public int CharacterId;
            // 最近一次相机配置使用的本地身份，用于判断后续回调是否需要刷新。
            public bool CameraIsLocal;
            public GameObject SourcePrefab;
            public UnityEngine.Camera[] Cameras;
            public AudioListener[] Listeners;
            public ThirdPersonCamera[] CameraControllers;
            public CharacterVisual Visual;
        }
        // 以网络玩家为键，统一查询、替换和清理其表现状态。
        private readonly Dictionary<NetworkCharacterSync, CharacterPresentation> presentations = new();
        // 缓存最近应用的表现模式；null 使首次 Update 执行场景角色和相机配置。
        private bool? networkPresentationMode;

        /// <summary>
        /// 供 NetworkCharacterSync.OnStartServer 读取的初始角色编号。
        /// 服务器将编号写入 characterId，由 Mirror 同步给客户端。
        /// 客户端根据编号和本地玩家身份，从对应数组装配角色表现。
        /// </summary>
        public int DefaultCharacterId => defaultCharacterId;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError(
                    $"场景中存在多个 {nameof(NetworkCharacterManager)}，请只在 PersistentScene 保留一个。",
                    this);
                enabled = false;
                return;
            }

            Instance = this;
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        // 场景加载带入新的单机角色和相机，按当前表现模式统一设置其启用状态。
        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            bool networkMode = IsNetworkPresentationActive();
            networkPresentationMode = networkMode;
            ApplyPresentationMode(networkMode);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            // 表现模式变化时刷新场景对象，模式稳定期间沿用已配置的状态。
            bool networkMode = IsNetworkPresentationActive();
            if (networkPresentationMode == networkMode)
            {
                return;//状态没变就不往下走
            }

            networkPresentationMode = networkMode;
            ApplyPresentationMode(networkMode);
        }

        /// <summary>检查角色编号是否配置了完整角色 Prefab，供客户端请求和服务器校验使用。</summary>
        public bool IsValidCharacterId(int requestedCharacterId)
        {
            return characters != null
                   && requestedCharacterId >= 0
                   && requestedCharacterId < characters.Length
                   && characters[requestedCharacterId] != null;
        }

        /// <summary>校验角色条目，返回远程角色表现的 Prefab。</summary>
        public bool TryGetCharacterPrefab(int requestedCharacterId, out GameObject characterPrefab)
        {
            if (IsValidCharacterId(requestedCharacterId))
            {
                characterPrefab = characters[requestedCharacterId];
                return true;
            }

            characterPrefab = null;
            return false;
        }

        /// <summary>
        /// 角色编号下行链路的本地装配终点，由 NetworkCharacterSync 的启动回调和 SyncVar hook 调用。
        /// 使用服务器确认的编号，从共用角色 Prefab 装配到已生成的网络根下。
        /// 本地拥有者启用控制器和相机，其他玩家仅保留视觉与动画。
        /// 有效实例的角色编号和来源 Prefab 相同时复用，资源变化或实例销毁时重新装配。
        /// </summary>
        public void ApplyCharacter(NetworkCharacterSync player, int requestedCharacterId)
        {
            // 同步编号决定数组下标，本地玩家身份决定使用哪套资源。
            bool isLocalPlayer = player != null && player.IsLocalPlayerCharacter;
            GameObject prefab;
            bool hasPrefab = TryGetCharacterPrefab(requestedCharacterId, out prefab);
            if (player == null || !isActiveAndEnabled || !hasPrefab)
            {
                Debug.LogWarning(
                    $"无法加载联机角色：编号 {requestedCharacterId} 缺少完整角色 Prefab 配置。",
                    this);
                return;
            }

            // 生命周期回调与 SyncVar hook 共用缓存；身份变化且资源相同时仅刷新相机配置。
            if (presentations.TryGetValue(player, out CharacterPresentation currentInstance)
                && currentInstance != null
                && currentInstance.Instance != null
                && currentInstance.CharacterId == requestedCharacterId
                && currentInstance.SourcePrefab == prefab)
            {
                if (currentInstance.CameraIsLocal != isLocalPlayer)
                {
                    ConfigureLocalCamera(player, currentInstance);
                }

                return;
            }

            CharacterVisual configuredVisual = prefab.GetComponent<CharacterVisual>();
            string issue = "Prefab 根节点缺少 CharacterVisual";
            if (configuredVisual == null || !configuredVisual.TryValidate(isLocalPlayer, out issue))
            {
                Debug.LogError($"角色 {requestedCharacterId} 的资源 {prefab.name} 配置无效：{issue}。", prefab);
                return;
            }

            GameObject characterInstance = InstantiateInactiveCharacter(prefab, player.transform);
            // 实例名标出本地或远程身份，便于核对运行时使用的资源。
            characterInstance.name = $"{prefab.name}_{(isLocalPlayer ? "LocalCharacter" : "RemoteCharacter")}";
            // 视觉实例与网络根对齐，世界位置和物理运动由根上的玩家控制器负责。
            characterInstance.transform.localPosition = Vector3.zero;
            characterInstance.transform.localRotation = Quaternion.identity;
            characterInstance.transform.localScale = Vector3.one;

            CharacterVisual visual = characterInstance.GetComponent<CharacterVisual>();
            if (visual == null || !visual.TryValidate(isLocalPlayer, out issue))
            {
                characterInstance.SetActive(false);
                Destroy(characterInstance);
                Debug.LogError($"角色 {requestedCharacterId} 的资源 {prefab.name} 实例配置无效：{issue}。", this);
                return;
            }

            RemoveCharacter(player);
            ConfigureNetworkCharacterComponents(visual);
            // 网络根绑定视觉实例保存的 Animator，运动驱动保持根对象中的实例。

            // 每类相机组件由视觉资产保存，后续身份配置直接使用当前实例的引用。
            var presentation = new CharacterPresentation
            {
                Instance = characterInstance,
                CharacterId = requestedCharacterId,
                SourcePrefab = prefab,
                Cameras = visual.Cameras,
                Listeners = visual.Listeners,
                CameraControllers = visual.CameraControllers,
                Visual = visual
            };

            ConfigureLocalCamera(player, presentation);
            presentations[player] = presentation;
            characterInstance.SetActive(true);
            player.PlayerController?.RebindAnimator(visual.Animator);
        }

        /// <summary>解除网络根的动画和输入引用，销毁视觉实例并移除玩家的表现状态。</summary>
        public void RemoveCharacter(NetworkCharacterSync player)
        {
            if (player == null)
            {
                return;
            }

            // 先解除引用，再销毁这些引用所属的视觉实例。
            player.PlayerController?.RebindInputSpace(null);
            player.PlayerController?.RebindAnimator(null);

            if (presentations.TryGetValue(player, out CharacterPresentation presentation)
                && presentation != null
                && presentation.Instance != null)
            {
                presentation.Instance.SetActive(false);
                Destroy(presentation.Instance);
            }

            presentations.Remove(player);
        }

        /// <summary>
        /// 角色切换的统一入口：联机时把编号提交给本地网络玩家，由服务器校验后经 SyncVar 下发；
        /// 离线时用目标编号的本地 Prefab 替换场景单机角色实例。
        /// 连接已建立但本地玩家尚未生成时输出警告，请求被忽略。
        /// </summary>
        public void RequestCharacter(int requestedCharacterId)
        {
            // 联机时角色表现由网络玩家承担，编号校验和同步交给服务器链路。
            if (TryGetLocalPlayerCharacter(out NetworkCharacterSync localCharacter))
            {
                localCharacter.SetLocalCharacter(requestedCharacterId);
                return;
            }

            // 离线时角色表现由场景单机角色承担，切换即替换该实例。
            if (IsStandalonePresentationActive)
            {
                ReplaceStandaloneCharacter(requestedCharacterId);
                return;
            }

            Debug.LogWarning(
                $"本地玩家尚未生成，角色 {requestedCharacterId} 的切换请求已忽略。",
                this);
        }

        // 当前是否由场景单机角色承担角色表现，连接重试阶段仍为 true。
        private static bool IsStandalonePresentationActive => !IsNetworkPresentationActive();

        // 取本机玩家的角色同步组件；未连接、玩家尚未生成或运行在专用服务器时返回 false。
        private static bool TryGetLocalPlayerCharacter(out NetworkCharacterSync localCharacter)
        {
            Mirror.NetworkIdentity localPlayer = Mirror.NetworkClient.localPlayer;
            if (localPlayer != null
                && localPlayer.TryGetComponent(out NetworkCharacterSync characterSync))
            {
                localCharacter = characterSync;
                return true;
            }

            localCharacter = null;
            return false;
        }

        // 离线切换：用目标编号的本地 Prefab 替换场景单机角色实例。
        // 新实例沿用旧实例的世界位置和朝向，并读取视觉组件保存的单机相机引用。
        private void ReplaceStandaloneCharacter(int requestedCharacterId)
        {
            if (!IsValidCharacterId(requestedCharacterId))
            {
                Debug.LogWarning(
                    $"无法切换单机角色：编号 {requestedCharacterId} 缺少对应的完整角色 Prefab 配置。",
                    this);
                return;
            }
            GameObject prefab = characters[requestedCharacterId];
            CharacterVisual configuredVisual = prefab.GetComponent<CharacterVisual>();
            string issue = "Prefab 根节点缺少 CharacterVisual";
            if (configuredVisual == null || !configuredVisual.TryValidate(true, out issue))
            {
                Debug.LogError($"单机角色 {requestedCharacterId} 的资源 {prefab.name} 配置无效：{issue}。", prefab);
                return;
            }

            // 编号与当前一致时沿用现有实例，避免重复装配重置相机轨道。
            if (standalonePlayer != null && standaloneCharacterId == requestedCharacterId)
            {
                return;
            }

            // 旧实例先停用，新旧实例不会同时持有活动的 AudioListener。
            GameObject previous = standalonePlayer;
            Vector3 position = previous != null ? previous.transform.position : Vector3.zero;
            Quaternion rotation = previous != null ? previous.transform.rotation : Quaternion.identity;
            if (previous != null)
            {
                previous.SetActive(false);
            }

            GameObject characterInstance = Instantiate(prefab, position, rotation);
            // 实例名沿用场景单机角色的命名，层级中可直接辨认当前角色。
            characterInstance.name = "LocalCharacter";
            standalonePlayer = characterInstance;
            standaloneCharacterId = requestedCharacterId;
            // 离线选定的编号成为本机角色，本地网络玩家启动时上报同一个值。
            defaultCharacterId = requestedCharacterId;

            // 相机组件随实例替换更新，后续启停沿用同一份引用。
            CharacterVisual visual = characterInstance.GetComponent<CharacterVisual>();
            standaloneCameras = visual.Cameras;
            standaloneCameraControllers = visual.CameraControllers;
            standaloneListeners = visual.Listeners;

            // 旧实例已无引用，销毁时一并释放它的输入与光标占用。
            if (previous != null)
            {
                Destroy(previous);
            }

            // 单机表现下新实例保持启用，相机、控制器和监听器与实例同步生效。
            SetStandalonePlayerEnabled(true);
            SetStandaloneCameraEnabled(true);
        }

        // 按当前本地身份配置缓存中的相机组件，同时绑定移动输入使用的视角。
        private static GameObject InstantiateInactiveCharacter(GameObject prefab, Transform parent)
        {
            // 在停用层级内实例化，身份配置完成前不执行相机和移动的启用生命周期。
            var staging = new GameObject("CharacterAssembly");
            staging.SetActive(false);
            try
            {
                GameObject instance = Instantiate(prefab, staging.transform);
                instance.SetActive(false);
                instance.transform.SetParent(parent, false);
                return instance;
            }
            finally
            {
                Destroy(staging);
            }
        }

        private static void ConfigureNetworkCharacterComponents(CharacterVisual visual)
        {
            visual.StandaloneController.enabled = false;
            visual.MovementCollider.enabled = false;
        }

        private static void ConfigureLocalCamera(
            NetworkCharacterSync player,
            CharacterPresentation presentation)
        {
            bool enableLocalCamera = player.IsLocalPlayerCharacter;

            foreach (UnityEngine.Camera camera in presentation.Cameras)
            {
                camera.enabled = enableLocalCamera;
            }

            foreach (AudioListener listener in presentation.Listeners)
            {
                listener.enabled = enableLocalCamera;
            }

            // 跟随目标统一指向网络根，启用状态由本地玩家身份控制。
            foreach (ThirdPersonCamera cameraController in presentation.CameraControllers)
            {
                cameraController.SetTarget(player.transform);
                cameraController.enabled = enableLocalCamera;
            }

            // 本地玩家以当前实例显式配置的 Camera 作为输入参考，远程玩家清空该引用。
            UnityEngine.Camera localCamera = enableLocalCamera
                ? presentation.Visual.InputCamera
                : null;
            player.PlayerController?.RebindInputSpace(localCamera != null ? localCamera.transform : null);
            presentation.CameraIsLocal = enableLocalCamera;
        }

        private static bool IsNetworkPresentationActive()
        {
            // 客户端连接成功或进程采用批处理模式时使用联机表现，连接尝试期间保留单机表现。
            return Mirror.NetworkClient.isConnected || Application.isBatchMode;
        }

        // 联机模式收起场景单机角色和相机，单机模式启用它们。
        private void ApplyPresentationMode(bool networkMode)
        {
            bool standaloneEnabled = !networkMode;
            SetStandalonePlayerEnabled(standaloneEnabled);
            SetStandaloneCameraEnabled(standaloneEnabled);
        }

        // 包含停用对象一起查找，按表现模式切换整个单机角色的激活状态。
        // 角色现由场景序列化引用提供，停用后仍可直接访问，不再执行全局查找。
        private void SetStandalonePlayerEnabled(bool enabled)
        {
            if (standalonePlayer != null && standalonePlayer.activeSelf != enabled)
            {
                standalonePlayer.SetActive(enabled);
            }
        }

        // 通过父级 NetworkIdentity 识别网络表现，相机模式切换作用于场景单机组件。
        // 单机组件现由场景显式引用区分，不再扫描场景或查询父级身份。
        private void SetStandaloneCameraEnabled(bool enabled)
        {
            foreach (UnityEngine.Camera camera in standaloneCameras)
            {
                if (camera != null)
                {
                    camera.enabled = enabled;
                }
            }

            foreach (ThirdPersonCamera cameraController in standaloneCameraControllers)
            {
                if (cameraController != null)
                {
                    cameraController.enabled = enabled;
                }
            }

            foreach (AudioListener listener in standaloneListeners)
            {
                if (listener != null)
                {
                    listener.enabled = enabled;
                }
            }
        }
    }
}
