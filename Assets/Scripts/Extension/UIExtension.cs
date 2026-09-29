using UnityEngine;

public static class RectTransformExtensions
{
    public static void SetSafeParent(this RectTransform source, Transform p, bool worldPositionStays = false)
    {
        if (p == null)
        {
            Debug.LogError("The target instance is null");
        }

        source.SetParent(p, worldPositionStays);
    }

    public static void SetSafeParent(this Transform source, Transform p, bool worldPositionStays = false)
    {
        if (p == null)
        {
            Debug.LogError("The target instance is null");
        }

        source.SetParent(p, worldPositionStays);
    }
}