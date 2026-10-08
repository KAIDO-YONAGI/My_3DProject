using UnityEngine;

internal sealed class CameraOrbitState
{
    public float Yaw { get; private set; }
    public float Pitch { get; private set; }
    public Quaternion WorldRotation { get; private set; }
    public Vector3 Offset { get; private set; }

    // 只平滑“焦点到镜头”的相对偏移，不平滑角色世界坐标。
    private Vector3 offsetVelocity;

    public void Initialize(Vector3 position, Quaternion rotation, Vector3 focus)
    {
        Vector3 angles = rotation.eulerAngles;
        Yaw = angles.y;
        Pitch = NormalizePitch(angles.x);
        WorldRotation = rotation;

        // 用场景中的初始镜头位置建立偏移，避免启用脚本时突然跳到预设距离。
        Offset = position - focus;
        offsetVelocity = Vector3.zero;
    }

    public void ApplyLook(Vector2 look, float sensitivity, float minPitch, float maxPitch)
    {
        // 鼠标只修改轨道角度，不直接累加世界位置。
        Yaw = Mathf.Repeat(Yaw + look.x * sensitivity, 360f);
        Pitch = Mathf.Clamp(Pitch - look.y * sensitivity, minPitch, maxPitch);
    }

    /// <summary>
    /// 从目标焦点、轨道旋转和碰撞距离计算镜头最终世界变换。
    /// </summary>
    public Vector3 Advance(float distance, float minimumOffsetY, float smoothSpeed, float deltaTime)
    {
        Quaternion desiredRotation = Quaternion.Euler(Pitch, Yaw, 0f);

        // 先解决障碍距离，再应用最低高度，得到当前帧期望位置。
        // 上一行保留旧版顺序说明；现在先约束高度并平滑，最终候选位置再交给避让器。
        Vector3 desiredOffset = -(desiredRotation * Vector3.forward) * distance;
        desiredOffset.y = Mathf.Max(desiredOffset.y, minimumOffsetY);

        // 只平滑相对偏移，避免角色速度改变镜头与角色之间的距离。
        float rate = Mathf.Max(0.01f, smoothSpeed);
        Vector3 candidate = Vector3.SmoothDamp(
            Offset, desiredOffset, ref offsetVelocity, 1f / rate, Mathf.Infinity, deltaTime);
        candidate.y = Mathf.Max(candidate.y, minimumOffsetY);
        WorldRotation = Quaternion.Slerp(
            WorldRotation, desiredRotation, CameraRotationMath.ExponentialFactor(rate, deltaTime));
        return candidate;
    }

    public void CommitOffset(Vector3 candidate, Vector3 resolved)
    {
        Offset = resolved;
        if ((candidate - resolved).sqrMagnitude > 0f)
        {
            offsetVelocity = Vector3.zero;
        }
    }

    // Unity 欧拉角使用 0-360 表示，转换后才能正确应用负俯角限制。
    private static float NormalizePitch(float value)
    {
        return value > 180f ? value - 360f : value;
    }
}
