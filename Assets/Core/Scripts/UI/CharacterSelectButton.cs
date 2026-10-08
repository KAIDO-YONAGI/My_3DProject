using Mirror;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 角色选择按钮：把 UI 点击转换为本地玩家的角色切换请求。
/// 本地玩家由 Mirror 在运行时生成，因此这里不做序列化引用，只在点击时按本地身份解析实例。
/// 编号校验和联机同步由 NetworkCharacterSync.SetLocalCharacter 与服务端 Command 完成。
/// </summary>
[DisallowMultipleComponent]
public sealed class CharacterSelectButton : MonoBehaviour
{
    [Tooltip("被监听的按钮。留空时自动取用同一 GameObject 上的 Button。")]
    [SerializeField] private Button targetButton;

    [Tooltip("该按钮代表的角色编号，对应 CharactersForLocal 与 CharactersForSync 的同一下标。两个数组都必须在这个下标上配置有效 Prefab。")]
    [SerializeField, Min(0)] private int characterId;

    // 组件与按钮同挂一个物体时自动补全引用，减少手工拖拽。
    private void Reset()
    {
        targetButton = GetComponent<Button>();
    }

    private void OnEnable()
    {
        Button button = ResolveButton();
        if (button != null)
        {
            button.onClick.AddListener(OnClick);
        }
    }

    private void OnDisable()
    {
        Button button = ResolveButton();
        if (button != null)
        {
            button.onClick.RemoveListener(OnClick);
        }
    }

    // 点击时解析本地玩家，未生成或已销毁时给出提示，避免请求被静默丢弃。
    private void OnClick()
    {
        NetworkCharacterSync localCharacter = ResolveLocalCharacter();
        if (localCharacter == null)
        {
            Debug.LogWarning(
                $"[{nameof(CharacterSelectButton)}] 本地玩家尚未生成，角色 {characterId} 的切换请求已忽略。",
                this);
            return;
        }

        // 编号是否落在两套角色数组内由 NetworkCharacterManager 校验，无效编号不会产生有效同步。
        localCharacter.SetLocalCharacter(characterId);
    }

    /// <summary>优先使用 Inspector 指定的按钮，缺省回退到同一 GameObject 上的 Button。</summary>
    private Button ResolveButton()
    {
        return targetButton != null ? targetButton : targetButton = GetComponent<Button>();
    }

    /// <summary>从 Mirror 的本地玩家身份取得网络玩家根上的角色同步组件；专用服务器上始终为 null。</summary>
    private static NetworkCharacterSync ResolveLocalCharacter()
    {
        NetworkIdentity localPlayer = NetworkClient.localPlayer;
        return localPlayer != null && localPlayer.TryGetComponent(out NetworkCharacterSync characterSync)
            ? characterSync
            : null;
    }
}
