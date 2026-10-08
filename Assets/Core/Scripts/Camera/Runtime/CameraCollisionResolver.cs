using UnityEngine;

internal sealed class CameraCollisionResolver
{
    // 使用固定数组接收无分配球形检测结果，容量足以覆盖镜头路径上的常见碰撞体。
    // 常见情况复用初始数组；结果填满时必须扩容重试，不能假定未排序的结果已包含最近障碍。
    private const int CollisionHitCapacity = 16;
    private const int MaxCachedHitCapacity = 128;
    private const float ContactSkin = 0.01f;
    private RaycastHit[] collisionHits = new RaycastHit[CollisionHitCapacity];

    /// <summary>
    /// 检测焦点到期望镜头位置之间的障碍物，并返回不会穿模的最近距离。
    /// </summary>
    public Vector3 Resolve(Vector3 focus, Vector3 candidateOffset, Transform target, float radius, LayerMask layers)
    {
        float length = candidateOffset.magnitude;
        if (radius <= 0f || length <= 0f)
        {
            return candidateOffset;
        }

        Vector3 direction = candidateOffset / length;
        int hitCount;
        while (true)
        {
            hitCount = Physics.SphereCastNonAlloc(
                focus, radius, direction, collisionHits, length, layers, QueryTriggerInteraction.Ignore);
            if (hitCount < collisionHits.Length)
            {
                return Clip(focus, candidateOffset, length, target, radius, collisionHits, hitCount);
            }
            if (collisionHits.Length >= MaxCachedHitCapacity)
            {
                RaycastHit[] allHits = Physics.SphereCastAll(
                    focus, radius, direction, length, layers, QueryTriggerInteraction.Ignore);
                return Clip(focus, candidateOffset, length, target, radius, allHits, allHits.Length);
            }
            collisionHits = new RaycastHit[collisionHits.Length * 2];
        }
    }

    private static Vector3 Clip(
        Vector3 focus, Vector3 candidateOffset, float length, Transform target, float radius, RaycastHit[] hits, int count)
    {
        float resolvedDistance = length;
        bool exitsOverlap = false;
        for (int index = 0; index < count; index++)
        {
            RaycastHit hit = hits[index];

            // 角色自身碰撞体不应被当成镜头障碍物。
            if (hit.collider == null || IsTargetCollider(hit.collider.transform, target))
            {
                continue;
            }
            if (hit.distance <= 0f && CanExitOverlap(hit.collider.bounds, focus, candidateOffset, radius))
            {
                exitsOverlap = true;
                continue;
            }

            // hit.distance 是球心行进距离，不能再减一次球半径，也不能用缩放下限推回障碍后面。
            resolvedDistance = Mathf.Min(resolvedDistance, Mathf.Max(0f, hit.distance - ContactSkin));
        }
        Vector3 resolved = candidateOffset * (resolvedDistance / length);
        if (exitsOverlap && resolvedDistance < length)
        {
            // 其他障碍可能把出口裁短；最终位置也必须满足退出条件，而不只是原始候选位置。
            for (int index = 0; index < count; index++)
            {
                RaycastHit hit = hits[index];
                if (hit.distance <= 0f && hit.collider != null
                    && !IsTargetCollider(hit.collider.transform, target)
                    && !CanExitOverlap(hit.collider.bounds, focus, resolved, radius))
                {
                    return Vector3.zero;
                }
            }
        }
        return resolved;
    }

    private static bool CanExitOverlap(Bounds bounds, Vector3 focus, Vector3 candidateOffset, float radius)
    {
        // 包围盒最近点给出分离平面；沿其外法线单调远离且终点球体完全清空时，不可能再次撞到同一碰撞体。
        // 焦点处于包围盒内或不能证明安全退出时仍按零距离阻挡，不能笼统忽略初始重叠。
        Vector3 separation = focus - bounds.ClosestPoint(focus);
        float separationLength = separation.magnitude;
        if (separationLength <= 0f)
        {
            return false;
        }
        float outwardTravel = Vector3.Dot(candidateOffset, separation / separationLength);
        return outwardTravel > 0f && separationLength + outwardTravel >= radius + ContactSkin;
    }

    /// <summary>
    /// 判断命中的碰撞体是否属于当前跟随角色。
    /// </summary>
    private static bool IsTargetCollider(Transform hitTransform, Transform target)
    {
        // 射线从角色内部发出，必须忽略角色自己的碰撞体。
        return target != null && (hitTransform == target || hitTransform.IsChildOf(target));
    }
}
