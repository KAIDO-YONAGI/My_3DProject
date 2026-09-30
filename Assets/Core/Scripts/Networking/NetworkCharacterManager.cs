using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// PersistentScene 中的联机角色表现管理器。
/// 编辑器通过这个场景对象分别配置本地角色和远程同步角色，网络玩家只同步角色编号。
/// </summary>
public sealed class NetworkCharacterManager : MonoBehaviour
{
    public static NetworkCharacterManager Instance { get; private set; }

    [Header("CharactersForLocal 本地玩家配置")]
    // 数组下标就是角色编号；本地拥有者使用这里的本地 Prefab。
    // 这些 Prefab 可以保留本地 Camera、ThirdPersonCamera 和单机表现配置。
    [SerializeField] private GameObject[] localCharacterPrefabs = new GameObject[0];

    [Header("CharactersForSync 远程角色配置")]
    // 数组下标必须与 localCharacterPrefabs 对齐；远程拥有者只使用这里的同步 Prefab。
    // 这里不应配置 CharactersForLocal，远程角色不应携带本地相机和输入表现。
    [SerializeField] private GameObject[] characterPrefabs = new GameObject[0];
    [SerializeField, Min(0)] private int defaultCharacterId;

    private readonly Dictionary<NetworkCharacterSync, GameObject> characterInstances = new();
    private readonly Dictionary<NetworkCharacterSync, int> appliedCharacterIds = new();
    private bool? networkPresentationMode;

    /// <summary>
    /// 返回 PersistentScene 中配置的默认角色编号。
    /// 
    /// 调用链：
    /// 1. NetworkCharacterSync.OnStartServer 在服务端生成 Player_Network 时读取本属性。
    /// 2. 读取结果写入 NetworkCharacterSync.characterId，这个字段由 Mirror 的 SyncVar 发送给所有客户端。
    /// 3. 客户端收到编号后进入 NetworkCharacterSync.ApplyCharacter。
    /// 4. ApplyCharacter 再调用本管理器的 ApplyCharacter：
    ///    本地拥有者从 CharactersForLocal 取模型，远程拥有者从 CharactersForSync 取模型。
    /// 
    /// 本属性只提供配置值，不负责验证编号、不负责同步网络，也不负责实例化模型。
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
    /// 根据玩家同步到的编号加载视觉模型。
    /// 本地拥有者使用 CharactersForLocal，远程拥有者使用 CharactersForSync。
    /// </summary>
    public void ApplyCharacter(NetworkCharacterSync player, int requestedCharacterId)
    {
        // 这里是“收到编号后显示模型”的最终入口：
        // NetworkCharacterSync 只保存网络编号，本管理器才负责把编号转换为正确类别的本地实例。
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

    private static void ApplyPresentationMode(bool networkMode)
    {
        bool standaloneEnabled = !networkMode;
        SetStandalonePlayerEnabled(standaloneEnabled);
        SetStandaloneCameraEnabled(standaloneEnabled);
    }

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
