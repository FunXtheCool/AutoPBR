using System.Numerics;

namespace AutoPBR.Core.Preview;

/// <summary>Derives near/far planes for orbit preview from subject + optional stage environment.</summary>
public static class PreviewCameraDepthRange
{
    public const float DefaultNear = 0.1f;
    public const float DefaultFar = 100f;

    /// <summary>
    /// Orbit camera depth range using subject bounds plus optional floor grid/ground extent.
    /// <paramref name="eye"/> must already be composed for the current frame.
    /// </summary>
    public static (float NearPlane, float FarPlane) ForOrbitPreview(
        Vector3 subjectMin,
        Vector3 subjectMax,
        float orbitDistance,
        Vector3 eye,
        float environmentHalfExtent = 0f,
        float environmentFloorY = -0.56f)
    {
        var sceneMin = subjectMin;
        var sceneMax = subjectMax;
        if (environmentHalfExtent > 0f)
        {
            var envMin = new Vector3(-environmentHalfExtent, environmentFloorY, -environmentHalfExtent);
            var envMax = new Vector3(environmentHalfExtent, environmentFloorY + 0.05f, environmentHalfExtent);
            sceneMin = Vector3.Min(sceneMin, envMin);
            sceneMax = Vector3.Max(sceneMax, envMax);
        }

        var minDist = MinDistanceEyeToAabb(eye, sceneMin, sceneMax);
        var maxDist = MaxDistanceEyeToAabb(eye, sceneMin, sceneMax);

        var orbit = Math.Max(orbitDistance, minDist);
        var near = Math.Clamp(minDist * 0.35f, 0.05f, Math.Max(minDist * 0.92f, 0.05f));
        if (minDist < 0.15f)
        {
            near = Math.Clamp(orbit * 0.04f, 0.05f, orbit * 0.45f);
        }

        var far = Math.Max(maxDist + 2.5f, near * 8f);
        far = Math.Min(far, DefaultFar);

        if (far / near > 5000f)
        {
            far = near * 5000f;
        }

        return (near, far);
    }

    /// <summary>Legacy subject-only range (does not include grid); prefer <see cref="ForOrbitPreview"/>.</summary>
    public static (float NearPlane, float FarPlane) ForSubjectBounds(
        Vector3 boundsMin,
        Vector3 boundsMax,
        float orbitDistance,
        float marginScale = 1.35f)
    {
        var extent = boundsMax - boundsMin;
        var radius = extent.Length() * 0.5f;
        if (!float.IsFinite(radius) || radius < 1e-4f)
        {
            radius = 0.75f;
        }

        var orbit = Math.Max(orbitDistance, radius + 0.25f);
        var margin = Math.Max(0.35f, radius * marginScale);
        var near = Math.Clamp(orbit * 0.04f, 0.05f, orbit * 0.45f);
        var far = orbit + radius * 2.5f + margin;
        if (far / near > 5000f)
        {
            far = near * 5000f;
        }

        return (near, far);
    }

    private static float MaxDistanceEyeToAabb(Vector3 eye, Vector3 min, Vector3 max)
    {
        var maxDist = 0f;
        for (var ix = 0; ix < 2; ix++)
        {
            var x = ix == 0 ? min.X : max.X;
            for (var iy = 0; iy < 2; iy++)
            {
                var y = iy == 0 ? min.Y : max.Y;
                for (var iz = 0; iz < 2; iz++)
                {
                    var z = iz == 0 ? min.Z : max.Z;
                    maxDist = Math.Max(maxDist, Vector3.Distance(eye, new Vector3(x, y, z)));
                }
            }
        }

        return maxDist;
    }

    private static float MinDistanceEyeToAabb(Vector3 eye, Vector3 min, Vector3 max)
    {
        var closest = new Vector3(
            Math.Clamp(eye.X, min.X, max.X),
            Math.Clamp(eye.Y, min.Y, max.Y),
            Math.Clamp(eye.Z, min.Z, max.Z));
        return Vector3.Distance(eye, closest);
    }
}
