using Mirror;
using UnityEngine;

/// <summary>
/// Player_Network 上的轻量同步组件。
/// 只负责向服务端提交角色编号并接收同步结果，视觉模型由 PersistentScene 管理器加载。
/// </summary>
public sealed class NetworkCharacterSync : NetworkBehaviour
{
    [SyncVar(hook = nameof(OnCharacterIdChanged))]
    private int characterId;

    private PlayerCharacterController playerController;

    public int CharacterId => characterId;
    public bool IsLocalPlayerCharacter => isLocalPlayer;
    public PlayerCharacterController PlayerController =>
        playerController != null
            ? playerController
            : playerController = GetComponent<PlayerCharacterController>();

    public override void OnStartServer()
    {
        base.OnStartServer();

        // 默认角色配置只存在于 PersistentScene 的 NetworkCharacterManager。
        // 服务端读取编号并写入 SyncVar；这里不直接实例化模型，因为服务端不负责客户端视觉表现。
        characterId = NetworkCharacterManager.Instance != null
            ? Mathf.Max(0, NetworkCharacterManager.Instance.DefaultCharacterId)
            : 0;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // OnStartClient 负责处理初始同步值，保证客户端刚生成玩家时就显示对应的本地或远程视觉模型。
        ApplyCharacter();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        // 本地玩家可能先完成本地身份初始化，再完成场景管理器绑定。
        // 再调用一次 ApplyCharacter 可以覆盖这个初始化时序差异；管理器会复用已存在的相同模型。
        ApplyCharacter();
    }

    public override void OnStopClient()
    {
        NetworkCharacterManager.Instance?.RemoveCharacter(this);
        base.OnStopClient();
    }

    /// <summary>
    /// 本地玩家切换远程角色时只提交编号，不直接操作远程模型。
    /// </summary>
    public void SetLocalCharacter(int requestedCharacterId)
    {
        if (!isLocalPlayer
            || NetworkCharacterManager.Instance == null
            || !NetworkCharacterManager.Instance.IsValidCharacterId(requestedCharacterId))
        {
            return;
        }

        if (NetworkServer.active)
        {
            characterId = requestedCharacterId;
            ApplyCharacter();
            return;
        }

        CmdSetCharacter(requestedCharacterId);
    }

    [Command]
    private void CmdSetCharacter(int requestedCharacterId)
    {
        // 服务端只验证编号，视觉 Prefab 由各客户端的 PersistentScene 配置加载。
        if (NetworkCharacterManager.Instance == null
            || !NetworkCharacterManager.Instance.IsValidCharacterId(requestedCharacterId))
        {
            return;
        }

        characterId = requestedCharacterId;
    }

    private void OnCharacterIdChanged(int _, int __)
    {
        // Mirror 收到服务端的新编号后，从这里重新进入“编号 -> 本地预制体 -> 模型实例”的链路。
        ApplyCharacter();
    }

    private void ApplyCharacter()
    {
        // NetworkCharacterSync 不持有本地/远程 Prefab 数组，也不直接 Instantiate；
        // 它只把网络状态交给 PersistentScene 中唯一的 NetworkCharacterManager。
        NetworkCharacterManager.Instance?.ApplyCharacter(this, characterId);
    }
}
