using Mirror;
using UnityEngine;

/// <summary>
/// Player_Network 上的轻量同步组件。
/// 服务器保存并同步角色编号；客户端收到编号后，交给 PersistentScene 中的管理器装配视觉模型。
/// </summary>
public sealed class NetworkCharacterSync : NetworkBehaviour
{
    // 服务器写入的角色编号，对应管理器两套角色数组的下标。
    // SyncVar 随 Spawn 发送初始值；后续修改同步给观察此玩家的客户端，收到变化时触发 hook。
    [SyncVar(hook = nameof(OnCharacterIdChanged))]
    private int characterId;

    // Player_Network 根上的运动控制器缓存，供表现管理器绑定 Animator 和相机输入空间。
    private PlayerCharacterController playerController;

    /// <summary>当前已同步的角色编号。</summary>
    public int CharacterId => characterId;
    /// <summary>当前网络玩家是否属于此客户端，用于选择本地模型和相机归属。</summary>
    public bool IsLocalPlayerCharacter => isLocalPlayer;
    /// <summary>按需取得同一网络根上的运动控制器，并缓存组件引用。</summary>
    public PlayerCharacterController PlayerController =>
        playerController != null
            ? playerController
            : playerController = GetComponent<PlayerCharacterController>();

    public override void OnStartServer()
    {
        base.OnStartServer();

        // NetworkManager.OnServerAddPlayer 已实例化 Player_Network 并交给 Mirror 生成网络对象；
        // Mirror 此时在服务器调用 OnStartServer。本方法从 PersistentScene 管理器读取默认编号，
        // 限制为非负值后写入服务器的 SyncVar；管理器不存在时使用 0，不检查数组是否有该模型。
        // 这里不创建视觉模型，客户端收到编号后才由各自的管理器装配。
        characterId = NetworkCharacterManager.Instance != null
            ? Mathf.Max(0, NetworkCharacterManager.Instance.DefaultCharacterId)
            : 0;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();

        // 远端客户端此时已反序列化 Spawn 中的初始编号；Host 则直接使用同进程的服务器状态。
        // 即使远端客户端的 hook 曾在反序列化时触发，也用当前编号再装配一次。
        ApplyCharacter();
    }

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();

        // Mirror 已把此网络玩家标记为当前客户端拥有；再按本地身份装配一次，
        // 确保使用 CharactersForLocal。若编号和模型都未变，管理器会复用实例。
        ApplyCharacter();
    }

    // Mirror 停止此客户端玩家时，回收本地装配的视觉模型和管理器缓存。
    public override void OnStopClient()
    {
        NetworkCharacterManager.Instance?.RemoveCharacter(this);
        base.OnStopClient();
    }

    /// <summary>
    /// 当前客户端为自己拥有的玩家申请切换角色；只请求修改同步编号，不发送模型。
    /// </summary>
    /// <param name="requestedCharacterId">本地玩家为自身选择的角色编号，对应管理器两套数组的下标。</param>
    public void SetLocalCharacter(int requestedCharacterId)
    {
        if (!isLocalPlayer
            || NetworkCharacterManager.Instance == null
            || !NetworkCharacterManager.Instance.IsValidCharacterId(requestedCharacterId))
        {
            return;
        }

        // Host 的本地玩家与服务器在同一进程：直接修改服务器字段并刷新本地视觉。
        if (NetworkServer.active)
        {
            characterId = requestedCharacterId;
            ApplyCharacter();
            return;
        }

        // 普通客户端不能直接写服务器的 SyncVar；用 Command 请求服务器修改编号，
        // 再由 SyncVar 把服务器确认的结果同步给观察此玩家的客户端。
        CmdSetCharacter(requestedCharacterId);
    }

    // Command 由拥有此玩家的客户端调用，在服务器执行；校验后才修改服务器的 SyncVar。
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

    // Mirror 在客户端反序列化编号变化时调用 hook；初始 Spawn 的反序列化也可能触发。
    // 参数是旧值和新值；这里统一从已经更新的 characterId 读取当前编号。
    private void OnCharacterIdChanged(int _, int __)
    {
        // 仅刷新当前客户端的视觉模型，不修改服务器的编号。
        ApplyCharacter();
    }

    private void ApplyCharacter()
    {
        // 把这个网络玩家和已同步的编号交给 PersistentScene 管理器。
        // 管理器按当前客户端是否拥有该玩家选数组，并在本地实例化或复用模型。
        NetworkCharacterManager.Instance?.ApplyCharacter(this, characterId);
    }
}
