using System;
using System.Collections.Generic;
using System.Numerics;
// ReSharper disable CheckNamespace



namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{

    /// <summary>
    /// <c>HoglinModel</c>/<c>ZoglinModel</c> head baseline from <c>PartPose.offsetAndRotation(..., 0.87266463f + headPitch, ...)</c>.
    /// Baby transformer scales the resulting angle.
    /// </summary>
    internal static float ComputeHoglinFamilyHeadPitchRad(float setupAnimHeadPitchRad, float transformerScale) =>
        (0.87266463f + setupAnimHeadPitchRad) * transformerScale;

    /// <summary>
    /// Matches <c>StriderModel.animateBristle</c> (26.1.2 <c>client.jar</c> javap): accumulates <c>zRot</c> deltas on three bristle parts.
    /// </summary>
    private static void AccumulateStriderBristleZRotFromAnimateBristle(
        float ageInTicks,
        float bristlePhaseFactor,
        ref float topZ,
        ref float midZ,
        ref float bottomZ)
    {
        topZ += bristlePhaseFactor * 0.6f + 0.1f * MathF.Sin(ageInTicks * 0.4f);
        midZ += bristlePhaseFactor * 1.2f + 0.1f * MathF.Sin(ageInTicks * 0.2f);
        bottomZ += bristlePhaseFactor * 1.3f + 0.05f * MathF.Sin(-ageInTicks * 0.4f);
    }

}
