using UnityEngine;
#if USING_2DANIMATION
using UnityEngine.U2D.Animation;
#endif

internal static class ShadowShapeProvider2DUtility
{
    public static float GetTrimEdgeFromBounds(Bounds bounds, float trimMultipler)
    {
        Vector3 size = bounds.size;

        // Pick the smaller side
        float trimEdge = trimMultipler * (size.x < size.y ? size.x : size.y);

        // Clean up the trim value to one significant digit
        float multiplier = Mathf.Pow(10, -Mathf.Floor(Mathf.Log10(trimEdge)));
        trimEdge = Mathf.Floor(trimEdge * multiplier) / multiplier;

        return trimEdge;
    }

    public static bool IsUsingGpuDeformation()
    {
#if USING_2DANIMATION
        return SpriteSkinUtility.IsUsingGpuDeformation();
#else
        return false;
#endif
    }
}
