using Mirror;
using UnityEngine;

/// <summary>
/// 同步网络玩家的角色编号，客户端与专用服务器之间分为上行请求和下行状态两条链路。
/// 上行：本地拥有者调用 SetLocalCharacter，通过 CommandSetCharacter 将请求编号发送到服务器。
/// 服务器按自身角色配置校验请求，将有效编号写入 characterId。
/// 下行：Mirror 将 characterId 的初始值放入 Spawn，后续变化通过 SyncVar 状态同步发送。
/// 接收该玩家的客户端包含拥有者，客户端将确认编号交给 NetworkCharacterManager 装配表现。
/// 
/// 玩家位置与旋转由同一网络根上的 NetworkTransformReliable 独立同步。
/// 变换上行：本地 PlayerCharacterController 驱动网络根，NetworkTransformReliable 将变换快照发送到服务器。
/// 变换下行：服务器缓冲并插值更新网络根，再向其他观察客户端广播变换，接收端通过快照插值更新网络根。
/// 本地拥有者持续使用本地运动结果，角色模型作为网络根子对象跟随其变换。
/// </summary>
/// 
public sealed class NetworkCharacterSync : NetworkBehaviour
{
    // 下行状态由服务器写入：Spawn 携带初始编号，后续编号变化同步给拥有者和其他观察客户端。
    [SyncVar(hook = nameof(OnCharacterIdChanged))]
    private int characterId;

    // 缓存网络根的运动控制器，供表现管理器绑定模型动画和本地输入参考空间。
    private PlayerCharacterController playerController;

    /// <summary>服务器确认的当前角色编号，对应管理器两套数组的下标。</summary>
    public int CharacterId => characterId;
    /// <summary>当前网络玩家是否为此客户端的本地玩家。</summary>
    public bool IsLocalPlayerCharacter => isLocalPlayer;
    /// <summary>按需查找并复用同一网络根上的运动控制器。</summary>
    public PlayerCharacterController PlayerController =>
        playerController != null
            ? playerController
            : playerController = GetComponent<PlayerCharacterController>();

    public override void OnStartServer()
    {
        base.OnStartServer();

        // 服务器生成玩家时读取默认编号，取非负值写入 characterId，供初始 Spawn 下发。
        // 管理器为空时初始编号为 0，视觉资源在客户端装配时解析。
        characterId = NetworkCharacterManager.Instance != null
            ? Mathf.Max(0, NetworkCharacterManager.Instance.DefaultCharacterId)
            : 0;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // 下行初始装配：Mirror 已从 Spawn 读取 characterId，此处使用该编号装配角色。
        // 初始值与字段默认值相同时也通过本回调装配；与初始 hook 重复的请求由管理器缓存复用。
        ApplyCharacter();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        // 本地玩家启动或身份变为本地玩家时，按已接收的编号配置本地表现与相机。
        // 管理器按来源 Prefab 和已配置的身份判断复用、相机刷新或重新装配。
        ApplyCharacter();
    }

    // 客户端玩家停止时，回收其视觉实例、动画引用和输入参考。
    public override void OnStopClient()
    {
        NetworkCharacterManager.Instance?.RemoveCharacter(this);
        base.OnStopClient();
    }

    /// <summary>
    /// 上行入口：检查本地玩家身份和本地角色配置，将请求编号通过 Command 提交到服务器。
    /// 角色表现使用服务器下行确认的 characterId，由 SyncVar hook 和启动回调负责装配。
    /// </summary>
    /// <param name="requestedCharacterId">两套角色数组中有效且对应同一角色的下标。</param>
    public void SetLocalCharacter(int requestedCharacterId)
    {
        if (!isLocalPlayer
            || NetworkCharacterManager.Instance == null
            || !NetworkCharacterManager.Instance.IsValidCharacterId(requestedCharacterId))
        {
            return;
        }

        // Mirror 生成的 Command 发送代码将 requestedCharacterId 序列化为上行请求。
        CommandSetCharacter(requestedCharacterId);
    }

    // 上行接收：Mirror 校验发送连接的对象归属，在服务器执行此方法。
    // 服务器按自身两套角色数组校验编号，通过后更新 characterId，进入 SyncVar 下行同步。
    [Command]
    private void CommandSetCharacter(int requestedCharacterId)
    {
        if (NetworkCharacterManager.Instance == null
            || !NetworkCharacterManager.Instance.IsValidCharacterId(requestedCharacterId))
        {
            return;
        }

        characterId = requestedCharacterId;
    }

    // 下行变化接收：Mirror 反序列化时先更新 characterId，值变化后调用此 hook。
    // 客户端使用服务器确认的当前编号刷新表现，初始 Spawn 装配同时由 OnStartClient 保证。
    private void OnCharacterIdChanged(int _, int __)
    {
        ApplyCharacter();
    }

    private void ApplyCharacter()
    {
        // 下行装配入口：将当前网络玩家和已接收的编号交给管理器，由本地身份选择对应 Prefab。
        NetworkCharacterManager.Instance?.ApplyCharacter(this, characterId);
    }
}
