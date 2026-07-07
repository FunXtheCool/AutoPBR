using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Superseded by <see cref="ModelPartTranslateAndRotateProbeTests"/> and JVM
/// <c>renderPartAffines</c> / <c>renderCuboidCenters</c> — see
/// <c>docs/runtime-ir-preview-plan.md</c> § PartPose vs ModelPart render.
/// </summary>
public sealed class HornComposeOrderProbeTests
{
    [Fact]
    public void Cold_cow_horn_cuboid_world_Z_clusters_with_head_under_TxEr_not_ErxT()
    {
        var head = Matrix4x4.CreateTranslation(0f, 4f, -8f);
        var hornPoseJson = """
            {"translation":[-4.5,-2.5,-3.5],"rotationEulerRad":[1.570799947,0,0],"eulerOrder":"XYZ"}
            """;
        using var doc = JsonDocument.Parse(hornPoseJson);
        var cuboidCenter = new Vector3(-0.5f, -1.5f, 0.5f);

        EntityPreviewDebugSettings.UseLegacyTranslationTimesRotationPartPose = false;
        Assert.True(EntityModelRuntime.TryComposePartPosePublic(doc.RootElement, out var erTLocal));
        var erTHornWorld = Matrix4x4.Multiply(head, erTLocal);
        var erTCenter = Vector3.Transform(cuboidCenter, erTHornWorld);
        var headPivot = Vector3.Transform(Vector3.Zero, head);

        EntityPreviewDebugSettings.UseLegacyTranslationTimesRotationPartPose = true;
        Assert.True(EntityModelRuntime.TryComposePartPosePublic(doc.RootElement, out var txErLocal));
        var txErHornWorld = Matrix4x4.Multiply(head, txErLocal);
        var txErCenter = Vector3.Transform(cuboidCenter, txErHornWorld);

        EntityPreviewDebugSettings.UseLegacyTranslationTimesRotationPartPose = false;

        var erTHeadDist = Vector3.Distance(erTCenter, headPivot);
        var txErHeadDist = Vector3.Distance(txErCenter, headPivot);

        _ = erTHeadDist;
        _ = txErHeadDist;
        Assert.True(
            erTHeadDist < txErHeadDist,
            $"PartRender Er×T horn cuboid should be closer to head; Er×T={erTCenter} dHead={erTHeadDist:F3} T×Er={txErCenter} dHead={txErHeadDist:F3} head={headPivot}");
    }
}
