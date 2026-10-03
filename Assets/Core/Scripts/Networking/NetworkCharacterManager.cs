using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// PersistentScene 中的联机角色表现管理器。
/// 编辑器通过这个场景对象分别配置本地角色和远程同步角色，网络玩家只同步角色编号。
/// </summary>
public sealed class NetworkCharacterManager : MonoBehaviour
{
    /// <summary>当前场景中供网络玩家访问的角色表现管理器。</summary>
    public static NetworkCharacterManager Instance { get; private set; }

    [Header("CharactersForLocal 本地玩家配置")]
    // 数组下标就是角色编号；本地拥有者使用这里的本地 Prefab。
    // 这些 Prefab 可以保留本地 Camera、ThirdPersonCamera 和单机表现配置。
    [Tooltip("本地玩家的角色 Prefab 数组。下标从 0 开始作为角色编号，与 CharactersForSync 相同下标配置同一角色；本地 Prefab 提供模型、动画和本地相机。")]
    [SerializeField] private GameObject[] localCharacterPrefabs = new GameObject[0];

    [Header("CharactersForSync 远程角色配置")]
    // 数组下标必须与 localCharacterPrefabs 对齐；远程拥有者只使用这里的同步 Prefab。
    // 这里不应配置 CharactersForLocal，远程角色不应携带本地相机和输入表现。
    [Tooltip("远程玩家的同步表现 Prefab 数组。与 CharactersForLocal 按下标配对，配置 CharactersForSync 目录中的模型和动画资源；相机归属由本地玩家身份统一控制。")]
    [SerializeField] private GameObject[] characterPrefabs = new GameObject[0];
    [Tooltip("服务端生成玩家时写入 SyncVar 的初始角色编号。编号从 0 开始，对应两套角色数组中同一下标，两个位置都需要配置有效 Prefab。")]
    [SerializeField, Min(0)] private int defaultCharacterId;

    // 每个网络玩家对应的当前视觉实例，用于复用模型以及在切换角色或断线时销毁模型。
    private readonly Dictionary<NetworkCharacterSync, GameObject> characterInstances = new();
    // 已装配实例的角色编号，与实例缓存一起识别重复的初始化和 SyncVar 回调。
    private readonly Dictionary<NetworkCharacterSync, int> appliedCharacterIds = new();
    // null 表示首次应用表现规则；之后记录联机状态，状态变化时统一刷新单机角色和相机。
    private bool? networkPresentationMode;

    /// <summary>
    /// 只返回 PersistentScene 中配置的初始角色编号，不创建玩家或模型。
    ///
    /// 初次生成玩家时，这个编号的去向：
    /// 1. 客户端连接后请求添加玩家；服务器的 NetworkManager.OnServerAddPlayer
    ///    实例化 Player_Network，并通过 NetworkServer.AddPlayerForConnection 关联玩家与连接。
    /// 2. Mirror 在服务器生成这个网络对象时调用其 NetworkCharacterSync.OnStartServer；
    ///    该方法读取本属性，把编号写入服务器上的 characterId。
    /// 3. characterId 是 SyncVar：远端客户端通过玩家的 Spawn 状态收到初始值，
    ///    后续值变化也会同步给观察该玩家的客户端；Host 在同一进程读取服务器状态。
    ///    OnStartClient 应用当前编号，客户端收到编号变化时 SyncVar hook 再应用。
    /// 4. NetworkCharacterSync.ApplyCharacter 把编号交给本管理器的 ApplyCharacter；
    ///    每台客户端为自己的玩家从 CharactersForLocal 取模型，为其他玩家从 CharactersForSync
    ///    取模型，并在本地实例化。网络同步的是编号，不是模型实例。
    ///
    /// 本属性不校验编号、不负责网络同步，也不实例化模型。
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

    // Additive 加载会带入新的场景对象，加载完成后再次应用当前角色和相机启用规则。
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
        // 正在重试连接时仍属于单机表现；只有真正连上服务端后才切换到网络角色和网络相机。
        bool networkMode = IsNetworkPresentationActive();
        if (networkPresentationMode == networkMode)
        {
            return;
        }

        networkPresentationMode = networkMode;
        ApplyPresentationMode(networkMode);
    }

    /// <summary>检查角色编号在本地和同步数组中均有对应的有效 Prefab。</summary>
    public bool IsValidCharacterId(int requestedCharacterId)
    {
        // 服务端和客户端都使用这个检查，保证同一个编号在两套配置中都可解析。
        return localCharacterPrefabs != null
            && characterPrefabs != null
            && requestedCharacterId >= 0
            && requestedCharacterId < localCharacterPrefabs.Length
            && requestedCharacterId < characterPrefabs.Length
            && localCharacterPrefabs[requestedCharacterId] != null
            && characterPrefabs[requestedCharacterId] != null;
    }

    /// <summary>按角色编号取得远程同步表现的 Prefab，并校验两套数组的对应配置。</summary>
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

    // 本地装配直接读取本地数组中的资源，服务端角色选择校验由 IsValidCharacterId 承担。
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
    /// 在当前客户端根据同步到的编号装配视觉模型，而不是生成网络玩家。
    /// player 是已生成的 Player_Network；它属于当前客户端时，从 CharactersForLocal
    /// 取对应编号的 Prefab，否则从 CharactersForSync 取，并实例化为 player 的子物体。
    /// 同一编号重复应用时复用已有实例；编号变化时替换视觉实例。
    /// </summary>
    public void ApplyCharacter(NetworkCharacterSync player, int requestedCharacterId)
    {
        // NetworkCharacterSync 传来网络编号；这里按当前客户端的拥有者身份选用本地或远程模型。
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

        // 初始同步和本地身份回调可能重复到达；相同编号复用视觉实例，并重新确认相机归属。
        if (characterInstances.TryGetValue(player, out GameObject currentInstance)
            && currentInstance != null
            && appliedCharacterIds.TryGetValue(player, out int currentId)
            && currentId == requestedCharacterId)
        {
            ConfigureLocalCamera(player, currentInstance);
            return;
        }

        RemoveCharacter(player);

        GameObject characterInstance = Instantiate(prefab, player.transform);
        // 名称明确标出模型属于本地还是远程，便于在编辑器和运行时层级中排查混用问题。
        characterInstance.name = $"{prefab.name}_{(isLocalPlayer ? "LocalCharacter" : "RemoteCharacter")}";
        // 这里对齐视觉子对象与网络根；网络根的位置和物理运动由玩家控制器负责。
        characterInstance.transform.localPosition = Vector3.zero;
        characterInstance.transform.localRotation = Quaternion.identity;
        characterInstance.transform.localScale = Vector3.one;

        // 角色 Prefab 保留单机组件，但联机移动由 Player_Network 根对象统一负责。
        foreach (PlayerCharacterController characterController in
                 characterInstance.GetComponentsInChildren<PlayerCharacterController>(true))
        {
            characterController.enabled = false;
        }

        foreach (CharacterController characterController in
                 characterInstance.GetComponentsInChildren<CharacterController>(true))
        {
            characterController.enabled = false;
        }

        // 模型替换后，网络根对象必须重新绑定新模型的 Animator，
        // 并复制当前类别 Prefab 上的动画素材配置。
        PlayerCharacterController visualController =
            characterInstance.GetComponentInChildren<PlayerCharacterController>(true);
        player.PlayerController?.RebindAnimator(
            characterInstance.GetComponentInChildren<Animator>(true),
            visualController);

        // Camera 只允许存在并启用于本地 CharactersForLocal。
        // CharactersForSync 不应包含 Camera；这里仍保留统一绑定入口，兼容旧资源并防止误启用。
        foreach (ThirdPersonCamera cameraController in
                 characterInstance.GetComponentsInChildren<ThirdPersonCamera>(true))
        {
            cameraController.SetTarget(player.transform);
        }

        characterInstances[player] = characterInstance;
        appliedCharacterIds[player] = requestedCharacterId;
        ConfigureLocalCamera(player, characterInstance);
    }

    /// <summary>解除网络根的表现引用，销毁视觉实例并清理该玩家的装配缓存。</summary>
    public void RemoveCharacter(NetworkCharacterSync player)
    {
        if (player == null)
        {
            return;
        }

        // 清理网络根对象对旧模型的引用，避免旧 Animator 被销毁后仍被 Update/LateUpdate 访问。
        player.PlayerController?.RebindInputSpace(null);
        player.PlayerController?.RebindAnimator(null);

        if (characterInstances.TryGetValue(player, out GameObject characterInstance)
            && characterInstance != null)
        {
            Destroy(characterInstance);
        }

        characterInstances.Remove(player);
        appliedCharacterIds.Remove(player);
    }

    // 相机、音频监听器与相机控制器共用本地身份开关，并将本地视角交给移动输入。
    private static void ConfigureLocalCamera(
        NetworkCharacterSync player,
        GameObject characterInstance)
    {
        bool enableLocalCamera = player.IsLocalPlayerCharacter;

        foreach (Camera camera in characterInstance.GetComponentsInChildren<Camera>(true))
        {
            camera.enabled = enableLocalCamera;
            if (enableLocalCamera)
            {
                camera.tag = "MainCamera";
            }
        }

        foreach (AudioListener listener in
                 characterInstance.GetComponentsInChildren<AudioListener>(true))
        {
            listener.enabled = enableLocalCamera;
        }

        foreach (ThirdPersonCamera cameraController in
                 characterInstance.GetComponentsInChildren<ThirdPersonCamera>(true))
        {
            cameraController.enabled = enableLocalCamera;
        }

        // 移动代码不能依赖全局 Camera.main：场景中可能同时存在单机相机、远程模型相机
        // 或编辑器残留相机。只把本地 CharactersForLocal 的相机交给网络根对象作为输入参考。
        Camera localCamera = enableLocalCamera
            ? characterInstance.GetComponentInChildren<Camera>(true)
            : null;
        player.PlayerController?.RebindInputSpace(localCamera != null ? localCamera.transform : null);
    }

    // 通过父级 NetworkIdentity 区分网络表现与场景单机相机，只切换后者。
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
        // NetworkClient.active 在连接尝试和重试阶段也会为 true，不能用它关闭单机表现。
        // 专用服务器没有本地玩家；批处理模式下同样不应运行场景中的单机角色。
        return Mirror.NetworkClient.isConnected || Application.isBatchMode;
    }

    // 连接成功或进入批处理时收起单机表现，离线开发时启用场景角色及其相机。
    private static void ApplyPresentationMode(bool networkMode)
    {
        bool standaloneEnabled = !networkMode;
        SetStandalonePlayerEnabled(standaloneEnabled);
        SetStandaloneCameraEnabled(standaloneEnabled);
    }

    // 扫描包含停用对象在内的单机控制器，按所属对象切换整个角色的激活状态。
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
