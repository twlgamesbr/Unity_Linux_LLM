using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Unity.Rendering
{
    // This mirrors the LODRenderingUtils.cs from SRP core
    // It has been copied over to avoid exposing SRP core internals to the Entities package.
    // This uses unity.mathematics instead of Mathf for vectorization purposes.
    internal static class LODRenderingUtils
    {
        public static float CalculateFOVHalfAngle(float fieldOfView)
        {
            return math.tan(math.TORADIANS * fieldOfView * 0.5f);
        }

        public static float CalculateScreenRelativeMetricNoBias(LODParameters lodParams)
        {
            if (lodParams.isOrthographic)
            {
                return 2.0F * lodParams.orthoSize;
            }

            // Half angle at 90 degrees is 1.0 (So we skip halfAngle / 1.0 calculation)
            float halfAngle = CalculateFOVHalfAngle(lodParams.fieldOfView);
            return 2.0f * halfAngle;
        }

        public static float CalculateMeshLodConstant(
            LODParameters lodParams,
            float screenRelativeMetric,
            float meshLodThreshold
        )
        {
            return meshLodThreshold * screenRelativeMetric / lodParams.cameraPixelHeight;
        }

        public static float CalculatePerspectiveDistance(
            float3 objPosition,
            float3 camPosition,
            float sqrScreenRelativeMetric
        )
        {
            return math.sqrt(CalculateSqrPerspectiveDistance(objPosition, camPosition, sqrScreenRelativeMetric));
        }

        public static float CalculateSqrPerspectiveDistance(
            float3 objPosition,
            float3 camPosition,
            float sqrScreenRelativeMetric
        )
        {
            return math.lengthsq(objPosition - camPosition) * sqrScreenRelativeMetric;
        }

        public static float3 GetWorldReferencePoint(this LODGroup lodGroup)
        {
            return lodGroup.transform.TransformPoint(lodGroup.localReferencePoint);
        }

        public static float GetWorldSpaceScale(this LODGroup lodGroup)
        {
            float3 scale = lodGroup.transform.lossyScale;
            float largestAxis = math.abs(scale.x);
            largestAxis = math.max(largestAxis, math.abs(scale.y));
            largestAxis = math.max(largestAxis, math.abs(scale.z));
            return largestAxis;
        }

        public static float GetWorldSpaceSize(this LODGroup lodGroup)
        {
            return lodGroup.GetWorldSpaceScale() * lodGroup.size;
        }

        public static float CalculateLODDistance(float relativeScreenHeight, float size)
        {
            return size / relativeScreenHeight;
        }
    }
}
