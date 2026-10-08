using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 在 PersistentScene 中配置本地与远程角色资源，按网络玩家身份装配客户端表现。
/// 每个玩家的视觉实例、角色编号、来源 Prefab 和相机组件统一保存在表现状态中。
/// </summary>
public sealed class NetworkCharacterManager : MonoBehaviour
{
    /// <summary>网络玩家共用的角色表现管理器。</summary>
    public static NetworkCharacterManager Instance { get; private set; }

    [Header("CharactersForLocal 本地玩家配置")]
    // 下标对应角色编号，本地拥有者从这里取得模型、动画配置和本地相机。
    [Tooltip("本地玩家的角色 Prefab 数组。下标从 0 开始作为角色编号，与 CharactersForSync 相同下标配置同一角色；本地 Prefab 提供模型、动画和本地相机。")]
    [SerializeField] private GameObject[] localCharacterPrefabs = new GameObject[0];

    [Header("CharactersForSync 远程角色配置")]
    // 与本地数组的同一下标配置同一角色，供当前客户端显示远程玩家的模型和动画。
    [Tooltip("远程玩家的同步表现 Prefab 数组。与 CharactersForLocal 按下标配对，配置 CharactersForSync 目录中的模型和动画资源；相机归属由本地玩家身份统一控制。")]
    [SerializeField] private GameObject[] characterPrefabs = new GameObject[0];
    [Tooltip("服务端生成玩家时写入 SyncVar 的初始角色编号。编号从 0 开始，对应两套角色数组中同一下标，两个位置都需要配置有效 Prefab。")]
    [SerializeField, Min(0)] private int defaultCharacterId;

    // 单个玩家的装配结果，组件缓存与视觉实例共用生命周期。
    private sealed class CharacterPresentation
    {
        public GameObject Instance;
        public int CharacterId;
        // 最近一次相机配置使用的本地身份，用于判断后续回调是否需要刷新。
        public bool CameraIsLocal;
        public GameObject SourcePrefab;
        public Camera[] Cameras;
        public AudioListener[] Listeners;
        public ThirdPersonCamera[] CameraControllers;
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
            return;
        }

        networkPresentationMode = networkMode;
        ApplyPresentationMode(networkMode);
    }

    /// <summary>检查角色编号在两套数组中均对应有效 Prefab，供客户端请求和服务器校验使用。</summary>
    public bool IsValidCharacterId(int requestedCharacterId)
    {
        return localCharacterPrefabs != null
            && characterPrefabs != null
            && requestedCharacterId >= 0
            && requestedCharacterId < localCharacterPrefabs.Length
            && requestedCharacterId < characterPrefabs.Length
            && localCharacterPrefabs[requestedCharacterId] != null
            && characterPrefabs[requestedCharacterId] != null;
    }

    /// <summary>校验两套数组的对应项，返回远程角色表现的 Prefab。</summary>
    public bool TryGetCharacterPrefab(int requestedCharacterId, out GameObject characterPrefab)
    {
        if (IsValidCharacterId(requestedCharacterId))
        {
            characterPrefab = characterPrefabs[requestedCharacterId];
            return true;
        }

        characterPrefab = null;
        return false;
    }

    // 本地装配检查本地数组，角色选择请求的双数组校验由 IsValidCharacterId 承担。
    private bool TryGetLocalCharacterPrefab(int requestedCharacterId, out GameObject characterPrefab)
    {
        if (requestedCharacterId >= 0
            && localCharacterPrefabs != null
            && requestedCharacterId < localCharacterPrefabs.Length
            && localCharacterPrefabs[requestedCharacterId] != null)
        {
            characterPrefab = localCharacterPrefabs[requestedCharacterId];
            return true;
        }

        characterPrefab = null;
        return false;
    }

    /// <summary>
    /// 角色编号下行链路的本地装配终点，由 NetworkCharacterSync 的启动回调和 SyncVar hook 调用。
    /// 使用服务器确认的编号，按当前客户端的玩家身份选择 Prefab，将表现挂到已生成的网络根下。
    /// 本地拥有者选择 localCharacterPrefabs，其他玩家选择 characterPrefabs；模型和相机来自本地资源。
    /// 有效实例的角色编号和来源 Prefab 相同时复用，资源变化或实例销毁时重新装配。
    /// </summary>
    public void ApplyCharacter(NetworkCharacterSync player, int requestedCharacterId)
    {
        // 同步编号决定数组下标，本地玩家身份决定使用哪套资源。
        bool isLocalPlayer = player != null && player.IsLocalPlayerCharacter;
        GameObject prefab;
        bool hasPrefab = isLocalPlayer
            ? TryGetLocalCharacterPrefab(requestedCharacterId, out prefab)
            : TryGetCharacterPrefab(requestedCharacterId, out prefab);
        if (player == null || !isActiveAndEnabled || !hasPrefab)
        {
            Debug.LogWarning(
                $"无法加载联机角色：编号 {requestedCharacterId} 缺少对应的 {(isLocalPlayer ? "CharactersForLocal" : "CharactersForSync")} 配置。",
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

        RemoveCharacter(player);

        GameObject characterInstance = Instantiate(prefab, player.transform);
        // 实例名标出本地或远程身份，便于核对运行时使用的资源。
        characterInstance.name = $"{prefab.name}_{(isLocalPlayer ? "LocalCharacter" : "RemoteCharacter")}";
        // 视觉实例与网络根对齐，世界位置和物理运动由根上的玩家控制器负责。
        characterInstance.transform.localPosition = Vector3.zero;
        characterInstance.transform.localRotation = Quaternion.identity;
        characterInstance.transform.localScale = Vector3.one;

        // 停用视觉子对象的移动和碰撞组件，由网络根统一驱动角色运动。
        PlayerCharacterController[] visualControllers =
            characterInstance.GetComponentsInChildren<PlayerCharacterController>(true);
        foreach (PlayerCharacterController characterController in visualControllers)
        {
            characterController.enabled = false;
        }

        foreach (CharacterController characterController in
                 characterInstance.GetComponentsInChildren<CharacterController>(true))
        {
            characterController.enabled = false;
        }

        // 网络根绑定新模型的 Animator，并从已找到的首个视觉控制器复制动画素材配置。
        PlayerCharacterController visualController =
            visualControllers.Length > 0 ? visualControllers[0] : null;
        player.PlayerController?.RebindAnimator(
            characterInstance.GetComponentInChildren<Animator>(true),
            visualController);

        // 每类相机组件在装配时查找一次，后续身份配置直接使用当前实例的缓存。
        var presentation = new CharacterPresentation
        {
            Instance = characterInstance,
            CharacterId = requestedCharacterId,
            SourcePrefab = prefab,
            Cameras = characterInstance.GetComponentsInChildren<Camera>(true),
            Listeners = characterInstance.GetComponentsInChildren<AudioListener>(true),
            CameraControllers = characterInstance.GetComponentsInChildren<ThirdPersonCamera>(true)
        };

        ConfigureLocalCamera(player, presentation);
        presentations[player] = presentation;
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
            Destroy(presentation.Instance);
        }

        presentations.Remove(player);
    }

    // 按当前本地身份配置缓存中的相机组件，同时绑定移动输入使用的视角。
    private static void ConfigureLocalCamera(
        NetworkCharacterSync player,
        CharacterPresentation presentation)
    {
        bool enableLocalCamera = player.IsLocalPlayerCharacter;

        foreach (Camera camera in presentation.Cameras)
        {
            camera.enabled = enableLocalCamera;
            if (enableLocalCamera)
            {
                camera.tag = "MainCamera";
            }
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

        // 本地玩家以当前实例的首个 Camera 作为输入参考，远程玩家清空该引用。
        Camera localCamera = enableLocalCamera && presentation.Cameras.Length > 0
            ? presentation.Cameras[0]
            : null;
        player.PlayerController?.RebindInputSpace(localCamera != null ? localCamera.transform : null);
        presentation.CameraIsLocal = enableLocalCamera;
    }

    // 通过父级 NetworkIdentity 识别网络表现，相机模式切换作用于场景单机组件。
    private static void SetStandaloneCameraEnabled(bool enabled)
    {
        foreach (Camera camera in FindObjectsOfType<Camera>(true))
        {
            if (camera.GetComponentInParent<Mirror.NetworkIdentity>() == null)
            {
                camera.enabled = enabled;
            }
        }

        foreach (ThirdPersonCamera cameraController in
                 FindObjectsOfType<ThirdPersonCamera>(true))
        {
            if (cameraController.GetComponentInParent<Mirror.NetworkIdentity>() == null)
            {
                cameraController.enabled = enabled;
            }
        }

        foreach (AudioListener listener in FindObjectsOfType<AudioListener>(true))
        {
            if (listener.GetComponentInParent<Mirror.NetworkIdentity>() == null)
            {
                listener.enabled = enabled;
            }
        }
    }


    private static bool IsNetworkPresentationActive()
    {
        // 客户端连接成功或进程采用批处理模式时使用联机表现，连接尝试期间保留单机表现。
        return Mirror.NetworkClient.isConnected || Application.isBatchMode;
    }

    // 联机模式收起场景单机角色和相机，单机模式启用它们。
    private static void ApplyPresentationMode(bool networkMode)
    {
        bool standaloneEnabled = !networkMode;
        SetStandalonePlayerEnabled(standaloneEnabled);
        SetStandaloneCameraEnabled(standaloneEnabled);
    }

    // 包含停用对象一起查找，按表现模式切换整个单机角色的激活状态。
    private static void SetStandalonePlayerEnabled(bool enabled)
    {
        foreach (PlayerCharacterController characterController in
                 FindObjectsOfType<PlayerCharacterController>(true))
        {
            if (characterController.GetComponentInParent<Mirror.NetworkIdentity>() != null)
            {
                continue;
            }

            if (characterController.gameObject.activeSelf != enabled)
            {
                characterController.gameObject.SetActive(enabled);
            }
        }
    }
}
