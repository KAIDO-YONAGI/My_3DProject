using UnityEngine;

internal static class CameraRotationMath
{
    public static float ExponentialFactor(float rate, float deltaTime)
    {
        return rate > 0f && deltaTime > 0f ? 1f - Mathf.Exp(-rate * deltaTime) : 0f;
    }

    public static float BoundedExponentialStep(float remaining, float rate, float maxSpeed, float deltaTime)
    {
        if (remaining <= 0f || rate <= 0f || maxSpeed <= 0f || deltaTime <= 0f)
        {
            return 0f;
        }

        // 指数步长按帧长计算，收尾自然减速，且不同帧率下的收敛时间一致。
        // 最大角速度只兜住大角差时的峰值，不参与收尾，收尾由指数项决定。
        // 精确积分 dr/dt = -min(rate * r, maxSpeed)，包含一帧内从限速切换到指数段的情况。
        float threshold = maxSpeed / rate;
        if (remaining <= threshold)
        {
            return Mathf.Min(remaining * ExponentialFactor(rate, deltaTime), maxSpeed * deltaTime);
        }

        float timeToThreshold = (remaining - threshold) / maxSpeed;
        if (deltaTime <= timeToThreshold)
        {
            return maxSpeed * deltaTime;
        }

        float step = remaining - threshold * Mathf.Exp(-rate * (deltaTime - timeToThreshold));
        return Mathf.Clamp(step, 0f, Mathf.Min(remaining, maxSpeed * deltaTime));
    }

    /// <summary>
    /// 存在移动输入时，让角色转向镜头的水平朝向。
    /// 只同步 yaw，角色不继承镜头俯仰；单帧步长先按指数收敛算出、再用最大角速度夹住：
    /// 大角差被限速，不会甩头；小角差按指数减速收尾，不会在最后一帧把剩余角差一次转完。
    /// </summary>
    public static Quaternion AlignTarget(Quaternion current, float yaw, float rate, float maxSpeed, float deltaTime)
    {
        // 上述保留原有算法说明；限速与指数不再事后取最小值，而是对连续限速模型精确积分。
        Quaternion desired = Quaternion.Euler(0f, yaw, 0f);
        float step = BoundedExponentialStep(Quaternion.Angle(current, desired), rate, maxSpeed, deltaTime);
        return Quaternion.RotateTowards(current, desired, step);
    }
}
