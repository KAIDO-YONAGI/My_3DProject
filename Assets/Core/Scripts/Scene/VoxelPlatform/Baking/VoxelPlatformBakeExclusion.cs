using UnityEngine;

namespace Core.Scripts.Scene.VoxelPlatform.Baking
{
    /// <summary>
    /// 保留当前节点及其子节点的独立方块，供场景编辑和单独的交互逻辑使用。
    /// 标记覆盖源几何收集与烘焙清理，节点启停不改变排除范围。
    /// </summary>
    [DisallowMultipleComponent, AddComponentMenu("Environment/Voxel Platform Bake Exclusion")]
    public class VoxelPlatformBakeExclusion : MonoBehaviour
    {
    }
}
