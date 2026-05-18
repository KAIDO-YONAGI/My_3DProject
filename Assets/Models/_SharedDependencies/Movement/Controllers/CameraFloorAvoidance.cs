using UnityEngine;

public static class CameraFloorAvoidance
{
    public static Vector3 ResolvePosition(Vector3 orbitPosition, Vector3 targetPosition, float minHeightY, float minDistance = 0f)
    {
        float floorY = targetPosition.y + minHeightY;
        if (orbitPosition.y >= floorY)
        {
            return orbitPosition;
        }

        Vector3 toOrbit = orbitPosition - targetPosition;
        toOrbit.y = 0f;

        if (toOrbit.sqrMagnitude < 0.001f)
        {
            orbitPosition.y = floorY;
            return orbitPosition;
        }

        float yDeficit = floorY - orbitPosition.y;
        Vector3 flatDir = toOrbit.normalized;
        orbitPosition -= flatDir * yDeficit;
        orbitPosition.y = floorY;

        Vector3 toFinal = orbitPosition - targetPosition;
        toFinal.y = 0f;
        if (toFinal.sqrMagnitude < minDistance * minDistance)
        {
            orbitPosition = targetPosition + flatDir * minDistance;
            orbitPosition.y = floorY;
        }
        return orbitPosition;
    }
}
