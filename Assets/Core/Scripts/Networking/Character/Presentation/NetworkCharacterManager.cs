using System.Collections.Generic;
using Core.Scripts.Camera.Runtime;
using Core.Scripts.Character.Presentation;
using Core.Scripts.Networking.Character.Sync;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Core.Scripts.Networking.Character.Presentation
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

        // 以网络玩家为键，统一查询、替换和清理其表现状态。
        private readonly Dictionary<CharacterIdSync, CharacterPresentationState> presentations = new();
        private CharacterPresentationService presentationService;
        // 缓存最近应用的表现模式；null 使首次 Update 执行场景角色和相机配置。
        private bool? networkPresentationMode;

        /// <summary>
        /// 供 CharacterIdSync.OnStartServer 读取的初始角色编号。
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
            presentationService = new CharacterPresentationService(this, presentations);
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

        /// <summary>由 CharacterIdSync 调用，交给网络表现服务装配角色实例。</summary>
        public void ApplyCharacter(CharacterIdSync player, int requestedCharacterId)
        {
            presentationService.ApplyCharacter(player, requestedCharacterId);
        }

        /// <summary>由 CharacterIdSync 调用，交给网络表现服务清理角色实例。</summary>
        public void RemoveCharacter(CharacterIdSync player)
        {
            presentationService.RemoveCharacter(player);
        }

        /// <summary>
        /// 角色切换的统一入口：联机时把编号提交给本地网络玩家，由服务器校验后经 SyncVar 下发；
        /// 离线时用目标编号的本地 Prefab 替换场景单机角色实例。
        /// 连接已建立但本地玩家尚未生成时输出警告，请求被忽略。
        /// </summary>
        public void RequestCharacter(int requestedCharacterId)
        {
            // 联机时角色表现由网络玩家承担，编号校验和同步交给服务器链路。
            if (TryGetLocalPlayerCharacter(out CharacterIdSync localCharacter))
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
        private static bool TryGetLocalPlayerCharacter(out CharacterIdSync localCharacter)
        {
            Mirror.NetworkIdentity localPlayer = Mirror.NetworkClient.localPlayer;
            if (localPlayer != null
                && localPlayer.TryGetComponent(out CharacterIdSync characterSync))
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
