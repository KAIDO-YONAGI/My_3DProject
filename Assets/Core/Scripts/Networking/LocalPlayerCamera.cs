using UnityEngine;
using Mirror;

// 本地玩家相机跟随：只有本地玩家的实例会启用相机 Rig。
// 挂在 Player Prefab 内的相机 Rig 上，远端实例自动禁用，避免多相机/AudioListener 冲突。
/// <summary>
/// 按 Mirror 的本地玩家身份管理当前层级的相机与音频监听器。
/// </summary>
public class LocalPlayerCamera : NetworkBehaviour
{
    // Start 读取此 NetworkBehaviour 的本地玩家身份，为远程实例关闭当前子层级的相机组件。
    private void Start()
    {
        if (!isLocalPlayer)
        {
            // 远端玩家：禁用相机与监听器
            foreach (Camera cam in GetComponentsInChildren<Camera>())
            {
                cam.enabled = false;
            }
            foreach (AudioListener listener in GetComponentsInChildren<AudioListener>())
            {
                listener.enabled = false;
            }
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        if (!isLocalPlayer) return;

        // 简易跟随：保持在玩家后上方（阶段三替换为平滑跟随镜头）
        // player 引用此组件自身的 Transform，下面的世界坐标偏移以它当前的位置为基准。
        Transform player = transform;
        transform.position = player.position + new Vector3(0f, 2f, -6f);
    }
}
