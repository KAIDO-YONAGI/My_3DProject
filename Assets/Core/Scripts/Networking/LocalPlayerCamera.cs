using UnityEngine;
using Mirror;

// 本地玩家相机跟随：只有本地玩家的实例会启用相机 Rig。
// 挂在 Player Prefab 内的相机 Rig 上，远端实例自动禁用，避免多相机/AudioListener 冲突。
public class LocalPlayerCamera : NetworkBehaviour
{
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
        Transform player = transform;
        transform.position = player.position + new Vector3(0f, 2f, -6f);
    }
}
