using System.Globalization;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.GeometryCompiler;

internal static partial class JavapFloatGeometryMeshLift
{
    private static Dictionary<int, int> WithLoopIteration(IReadOnlyDictionary<int, int> boxIntLocals, int loopSlot,
        int iteration, IReadOnlyList<string>? loopSegment = null)
    {
        var map = new Dictionary<int, int>(boxIntLocals) { [loopSlot] = iteration };
        ApplyMagmaCubeSegmentTexLocals(loopSlot, iteration, map);
        if (loopSegment is not null)
        {
            ApplyGhastTentacleLoopIntLocals(loopSegment, loopSlot, iteration, map);
        }

        return map;
    }

    /// <summary>
    /// <c>GhastModel.createBodyLayer</c> stores per-tentacle height in local slot 6 via <c>RandomSource.nextInt(7) + 8</c>
    /// (seed <c>1660L</c> thread-local). Heights are stable for that seed; see reference_java bake.
    /// </summary>
    private static readonly int[] GhastTentacleHeightsByIteration = [8, 13, 9, 11, 11, 10, 12, 9, 12];
    private static readonly int[] HappyGhastTentacleHeightsByIteration = [5, 7, 4, 5, 5, 7, 8, 8, 5];

    private static void ApplyGhastTentacleLoopIntLocals(IReadOnlyList<string> seg, int loopSlot, int iteration,
        Dictionary<int, int> map)
    {
        var heights = ResolveGhastTentacleHeights(seg);
        if (loopSlot != 3 || iteration < 0 || iteration >= heights.Length)
        {
            return;
        }

        if (!seg.Any(static l => l.Contains("nextInt:(I)I", StringComparison.Ordinal)) ||
            !seg.Any(static l => l.Contains("bipush        7", StringComparison.Ordinal) ||
                                 l.Contains("bipush 7", StringComparison.Ordinal)) ||
            !seg.Any(static l => l.Contains("istore        6", StringComparison.Ordinal) ||
                                 l.Contains("istore 6", StringComparison.Ordinal)))
        {
            return;
        }

        map[6] = heights[iteration];
    }

    private static ReadOnlySpan<int> ResolveGhastTentacleHeights(IReadOnlyList<string> seg)
    {
        if (seg.Any(static l => l.Contains("HappyGhastModel", StringComparison.Ordinal) ||
                                l.Contains("animal/ghast/HappyGhastModel", StringComparison.Ordinal)))
        {
            return HappyGhastTentacleHeightsByIteration;
        }

        return GhastTentacleHeightsByIteration;
    }

    /// <summary>
    /// Forward-evaluate <c>fstore</c> pose locals in a mesh segment (blaze rods, guardian spikes, baby squid tentacles).
    /// </summary>
    private static void ApplySegmentComputedFstorePoseLocals(List<string> seg,
        IReadOnlyDictionary<int, int> intLocals, Dictionary<int, double> poseFloatLocals)
    {
        for (var i = 0; i < seg.Count; i++)
        {
            if (!JavapBytecodeStreamAnalyzer.TryParseFstoreLocalSlot(seg[i], out var dst))
            {
                continue;
            }

            var j = i - 1;
            var scratch = new List<string>();
            if (!TryConsumeOnePoseFloatOperandBackward(seg, ref j, 0, 0, poseFloatLocals, intLocals, scratch, out var v))
            {
                continue;
            }

            poseFloatLocals[dst] = v;
        }
    }

    /// <summary>
    /// Squid tentacle loop: legacy fast-path when segment fstore simulation is insufficient.
    /// </summary>
    private static void ApplyLoopDerivedPoseFloatLocals(List<string> seg, int _, int iteration,
        Dictionary<int, double> poseFloatLocals)
    {
        if (seg.Any(static l => l.Contains("BlazeModel.getPartName", StringComparison.Ordinal) ||
                                 l.Contains("monster/blaze/BlazeModel.getPartName", StringComparison.Ordinal)))
        {
            ApplyBlazeRodLoopPoseLocals(iteration, poseFloatLocals);
            return;
        }

        if (seg.Any(static l => l.Contains("GuardianModel.getSpike", StringComparison.Ordinal) ||
                                 l.Contains("GuardianModel.createSpikeName", StringComparison.Ordinal)))
        {
            ApplyGuardianSpikeLoopPoseLocals(iteration, poseFloatLocals);
            return;
        }

        if (seg.Any(static l => l.Contains("boxName:(I)", StringComparison.Ordinal)))
        {
            ApplySpinAttackBoxLoopFloatLocals(seg, iteration, poseFloatLocals);
            return;
        }

        if (!seg.Any(static l => l.Contains("createTentacleName", StringComparison.Ordinal) ||
                                 l.Contains("offsetAndRotation", StringComparison.Ordinal)))
        {
            return;
        }

        if (seg.Any(static l => l.Contains("Math.sin:(D)D", StringComparison.Ordinal)) &&
            seg.Any(static l => l.Contains("18.5f", StringComparison.Ordinal)))
        {
            var babyAngle = iteration * Math.PI * 2.0 / 8.0;
            poseFloatLocals[8] = Math.Cos(babyAngle) * 3.0;
            poseFloatLocals[9] = 18.5;
            poseFloatLocals[10] = Math.Sin(babyAngle) * 3.0;
            poseFloatLocals[11] = iteration * Math.PI * (-2.0) / 8.0 + Math.PI / 2.0;
            return;
        }

        var angle = iteration * Math.PI * 2.0 / 8.0;
        poseFloatLocals[9] = Math.Cos(angle) * 5.0;
        poseFloatLocals[10] = 15.0;
        poseFloatLocals[11] = Math.Sin(angle) * 5.0;
        poseFloatLocals[12] = iteration * Math.PI * (-2.0) / 8.0 + Math.PI / 2.0;
    }

    /// <summary>
    /// <c>SpinAttackEffectModel.createLayer</c>: per-iteration Y offset (slot 3) and <c>PartPose.withScale</c> factor (slot 4).
    /// </summary>
    private static void ApplySpinAttackBoxLoopFloatLocals(List<string> seg, int iteration,
        Dictionary<int, double> poseFloatLocals)
    {
        const double yBase = -3.2;
        const double yStep = 9.6;
        const double scaleBase = 0.75;
        if (!seg.Any(static l => l.Contains("// float -3.2f", StringComparison.Ordinal) ||
                                 l.Contains("float -3.2", StringComparison.Ordinal)))
        {
            return;
        }

        var factor = iteration + 1;
        poseFloatLocals[3] = yBase + yStep * factor;
        poseFloatLocals[4] = scaleBase * factor;
    }

    /// <summary>Blaze rods: <c>Mth.cos/sin((double)(int)fAngle)</c> with three quartets at different radii/heights.</summary>
    private static void ApplyBlazeRodLoopPoseLocals(int iteration, Dictionary<int, double> poseFloatLocals)
    {
        double baseAngle;
        double radius;
        double yBase;
        if (iteration < 4)
        {
            baseAngle = iteration * (Math.PI / 2.0);
            radius = 9;
            yBase = -2;
        }
        else if (iteration < 8)
        {
            baseAngle = 0.785398185 + (iteration - 4) * (Math.PI / 2.0);
            radius = 7;
            yBase = 2;
        }
        else
        {
            baseAngle = 0.471238941 + (iteration - 8) * (Math.PI / 2.0);
            radius = 5;
            yBase = 11;
        }

        poseFloatLocals[2] = baseAngle;
        poseFloatLocals[5] = Math.Cos(baseAngle) * radius;
        poseFloatLocals[6] = iteration < 8
            ? yBase + Math.Cos(iteration * 0.5)
            : yBase + Math.Cos(iteration * 1.5 * 0.5);
        poseFloatLocals[7] = Math.Sin(baseAngle) * radius;
    }

    private static void ApplyGuardianSpikeLoopPoseLocals(int iteration, Dictionary<int, double> poseFloatLocals)
    {
        if (_staticFloatArrays is null)
        {
            return;
        }

        const double pi = Math.PI;
        if (_staticFloatArrays.TryGetValue("SPIKE_X_ROT", out var xr) && iteration < xr.Length)
        {
            poseFloatLocals[8] = pi * xr[iteration];
        }

        if (_staticFloatArrays.TryGetValue("SPIKE_Y_ROT", out var yr) && iteration < yr.Length)
        {
            poseFloatLocals[9] = pi * yr[iteration];
        }

        if (_staticFloatArrays.TryGetValue("SPIKE_Z_ROT", out var zr) && iteration < zr.Length)
        {
            poseFloatLocals[10] = pi * zr[iteration];
        }
    }

    /// <summary>
    /// MagmaCube <c>createBodyLayer</c> computes per-segment <c>texOffs</c> locals in the loop prologue (slots 3 and 4).
    /// </summary>
    private static void ApplyMagmaCubeSegmentTexLocals(int loopSlot, int iteration, Dictionary<int, int> map)
    {
        if (loopSlot != 2)
        {
            return;
        }

        if (iteration <= 0)
        {
            map[3] = 0;
            map[4] = 0;
            return;
        }

        if (iteration < 4)
        {
            map[3] = 0;
            map[4] = 9 * iteration;
            return;
        }

        map[3] = 32;
        map[4] = 9 * iteration - 36;
    }
}
