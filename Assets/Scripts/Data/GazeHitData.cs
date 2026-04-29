using System;
using UnityEngine;

[Serializable]
public class GazeHitData
{
    public Vector3 hitPoint;
    public Vector3 hitNormal;
    public Vector3 gazeOrigin;
    public Vector3 gazeDirection;
    public float distance;
    public string hitObjectName;

    public bool Validate()
    {
        if (!IsFinite(hitPoint) || !IsFinite(hitNormal))
        {
            Debug.LogWarning($"[GazeHitData] Validate FAILED: hitPoint or hitNormal not finite. hitPoint={hitPoint}, hitNormal={hitNormal}");
            return false;
        }

        if (distance <= 0f)
        {
            Debug.LogWarning($"[GazeHitData] Validate FAILED: distance={distance} (must be positive)");
            return false;
        }

        float magnitude = gazeDirection.magnitude;
        if (Mathf.Abs(magnitude - 1.0f) > 0.01f)
        {
            Debug.LogWarning($"[GazeHitData] Validate FAILED: gazeDirection not normalized. magnitude={magnitude:F4}, direction={gazeDirection}");
            return false;
        }

        return true;
    }

    private static bool IsFinite(Vector3 v)
    {
        return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z)
            && !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
    }
}
