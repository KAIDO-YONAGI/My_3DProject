using Mirror;
using UnityEngine;

/// <summary>
/// 负责把网络玩家根对象与本地角色表现解耦。
/// 网络只同步 modelId；每个客户端根据自己的本地模型目录实例化角色和摄像头。
/// </summary>
public sealed class NetworkPlayerModel : NetworkBehaviour
{
    [Header("本地模型目录")]
    // 数组下标就是旧 UDP 方案中的 modelID，所有客户端必须保持相同顺序。
    [SerializeField] private GameObject[] models = new GameObject[0];
    [SerializeField, Min(0)] private int defaultModelId;

    // 只在当前网络玩家根对象下保存运行时表现，不参与网络同步。
    private GameObject currentModelInstance;
    private int currentAppliedModelId = -1;

    [SyncVar(hook = nameof(OnModelIdChanged))]
    private int modelId;

    public int ModelId => modelId;

    public override void OnStartServer()
    {
        base.OnStartServer();

        // 服务端也校验初始编号，避免错误配置被同步给所有客户端。
        modelId = IsValidModelId(defaultModelId) ? defaultModelId : FindFirstValidModelId();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        ApplyModel(modelId);
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        // Host 模式下 OnStartClient 与 OnStartLocalPlayer 的先后不能作为依赖。
        ApplyModel(modelId);
    }

    public override void OnStopClient()
    {
        DestroyCurrentModel();
        base.OnStopClient();
    }

    /// <summary>
    /// 给本地玩家调用的模型切换入口。
    /// 单机角色不经过本组件，因此不会被 Mirror 流程限制。
    /// </summary>
    public void SetLocalModel(int requestedModelId)
    {
        if (!isLocalPlayer || !IsValidModelId(requestedModelId))
        {
            return;
        }

        if (NetworkServer.active)
        {
            modelId = requestedModelId;
            ApplyModel(modelId);
            return;
        }

        CmdSetModel(requestedModelId);
    }

    [Command]
    private void CmdSetModel(int requestedModelId)
    {
        // 客户端只能请求目录中已经存在的编号，最终状态由服务端写入 SyncVar。
        if (!IsValidModelId(requestedModelId))
        {
            return;
        }

        modelId = requestedModelId;
    }

    private void OnModelIdChanged(int _, int newModelId)
    {
        ApplyModel(newModelId);
    }

    private void ApplyModel(int requestedModelId)
    {
        if (!isClient || !IsValidModelId(requestedModelId))
        {
            return;
        }

        if (currentModelInstance != null && currentAppliedModelId == requestedModelId)
        {
            ConfigureCameraOwnership(currentModelInstance);
            return;
        }

        DestroyCurrentModel();

        currentModelInstance = Instantiate(models[requestedModelId], transform);
        currentModelInstance.name = $"{models[requestedModelId].name}_Model";
        currentModelInstance.transform.localPosition = Vector3.zero;
        currentModelInstance.transform.localRotation = Quaternion.identity;
        currentModelInstance.transform.localScale = Vector3.one;
        currentAppliedModelId = requestedModelId;

        // 模型 Prefab 保留单机所需的完整组件，但联机移动由网络根对象统一负责。
        // 关闭模型内部的移动组件，避免它们与根对象的 CharacterController 争抢输入和位移。
        foreach (PlayerCharacterController characterController in
                 currentModelInstance.GetComponentsInChildren<PlayerCharacterController>(true))
        {
            characterController.enabled = false;
        }

        foreach (CharacterController characterController in
                 currentModelInstance.GetComponentsInChildren<CharacterController>(true))
        {
            characterController.enabled = false;
        }

        // 模型替换会销毁旧 Animator，网络根对象必须改绑到新模型的 Animator。
        GetComponent<PlayerCharacterController>()?.RebindAnimator(
            currentModelInstance.GetComponentInChildren<Animator>(true));

        // 摄像头仍然是本地模型的子物体，但跟随网络根对象计算轨道和朝向。
        foreach (ThirdPersonCamera cameraController in
                 currentModelInstance.GetComponentsInChildren<ThirdPersonCamera>(true))
        {
            cameraController.SetTarget(transform);
        }

        ConfigureCameraOwnership(currentModelInstance);
    }

    private void ConfigureCameraOwnership(GameObject modelInstance)
    {
        bool enableLocalCamera = isLocalPlayer;

        foreach (Camera camera in modelInstance.GetComponentsInChildren<Camera>(true))
        {
            camera.enabled = enableLocalCamera;
            if (enableLocalCamera)
            {
                camera.tag = "MainCamera";
            }
        }

        foreach (AudioListener listener in
                 modelInstance.GetComponentsInChildren<AudioListener>(true))
        {
            listener.enabled = enableLocalCamera;
        }

        foreach (ThirdPersonCamera cameraController in
                 modelInstance.GetComponentsInChildren<ThirdPersonCamera>(true))
        {
            cameraController.enabled = enableLocalCamera;
        }
    }

    private void DestroyCurrentModel()
    {
        if (currentModelInstance == null)
        {
            return;
        }

        // 先清空网络根对象的动画驱动，再销毁表现模型，避免销毁帧继续访问旧 Animator。
        GetComponent<PlayerCharacterController>()?.RebindAnimator(null);
        Destroy(currentModelInstance);
        currentModelInstance = null;
        currentAppliedModelId = -1;
    }

    private bool IsValidModelId(int requestedModelId)
    {
        return models != null
            && requestedModelId >= 0
            && requestedModelId < models.Length
            && models[requestedModelId] != null;
    }

    private int FindFirstValidModelId()
    {
        if (models == null)
        {
            return -1;
        }

        for (int index = 0; index < models.Length; index++)
        {
            if (models[index] != null)
            {
                return index;
            }
        }

        Debug.LogError($"{name} 没有配置任何可用的本地角色模型。", this);
        return -1;
    }
}
