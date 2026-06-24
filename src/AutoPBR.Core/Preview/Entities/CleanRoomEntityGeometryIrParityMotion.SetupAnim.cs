using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    private const float SetupAnimBaselineEpsilon = 1e-5f;

    private static bool TryGetSetupAnimPartPose(
        string partId,
        VanillaSetupAnimRuntime.PoseResult pose,
        out VanillaSetupAnimRuntime.PartPose partPose)
    {
        if (pose.Parts.TryGetValue(partId, out partPose!))
        {
            return true;
        }

        if (TryResolveLegacyMagmaCubeSetupAnimPartField(partId, out var legacyField) &&
            pose.Parts.TryGetValue(legacyField, out partPose!))
        {
            return true;
        }

        if (TryResolveSetupAnimPartField(partId, out var modelPartField) &&
            pose.Parts.TryGetValue(modelPartField, out partPose!))
        {
            return true;
        }

        partPose = null!;
        return false;
    }

    private static bool TryGetBaselinePartPose(
        string partId,
        IReadOnlyDictionary<string, VanillaSetupAnimRuntime.PartPose> baselineParts,
        out VanillaSetupAnimRuntime.PartPose baseline)
    {
        if (baselineParts.TryGetValue(partId, out baseline!))
        {
            return true;
        }

        if (TryResolveLegacyMagmaCubeSetupAnimPartField(partId, out var legacyField) &&
            baselineParts.TryGetValue(legacyField, out baseline!))
        {
            return true;
        }

        if (TryResolveSetupAnimPartField(partId, out var modelPartField) &&
            baselineParts.TryGetValue(modelPartField, out baseline!))
        {
            return true;
        }

        baseline = null!;
        return false;
    }

    private static bool TryResolveSetupAnimPartField(string geometryPartId, out string modelPartField)

    {

        modelPartField = geometryPartId switch

        {

            "right_hind_leg" => "rightHindLeg",

            "left_hind_leg" => "leftHindLeg",

            "right_front_leg" => "rightFrontLeg",

            "left_front_leg" => "leftFrontLeg",

            "right_leg" => "rightLeg",

            "left_leg" => "leftLeg",

            "right_arm" => "rightArm",

            "left_arm" => "leftArm",

            "right_wing" => "rightWing",

            "left_wing" => "leftWing",

            "hat" => "hat",

            "nose" => "nose",

            "head" or "beak" or "red_thing" or "mole" => "head",

            "body" => "body",

            "tail" => "tail",

            "tail_fin" => "tailFin",

            "tail1" => "tail1",

            "tail2" => "tail2",

            _ => ""

        };

        return modelPartField.Length > 0;

    }

    private static bool TryResolveLegacyMagmaCubeSetupAnimPartField(string geometryPartId, out string modelPartField)
    {
        modelPartField = "";
        if (!geometryPartId.StartsWith("cube", StringComparison.Ordinal) ||
            geometryPartId.Length <= 4 ||
            !int.TryParse(geometryPartId.AsSpan(4), out _))
        {
            return false;
        }

        modelPartField = "segment" + geometryPartId[4..];
        return true;
    }

    private static VanillaSetupAnimRuntime.PartPose MergeSetupAnimEffectivePose(
        VanillaSetupAnimRuntime.PartPose anim,
        VanillaSetupAnimRuntime.PartPose baseline)
    {
        var assigned = anim.Assigned;
        return new VanillaSetupAnimRuntime.PartPose
        {
            X = (assigned & VanillaSetupAnimRuntime.PartPoseChannel.X) != 0 ? anim.X : baseline.X,
            Y = (assigned & VanillaSetupAnimRuntime.PartPoseChannel.Y) != 0 ? anim.Y : baseline.Y,
            Z = (assigned & VanillaSetupAnimRuntime.PartPoseChannel.Z) != 0 ? anim.Z : baseline.Z,
            XRot = (assigned & VanillaSetupAnimRuntime.PartPoseChannel.XRot) != 0 ? anim.XRot : baseline.XRot,
            YRot = (assigned & VanillaSetupAnimRuntime.PartPoseChannel.YRot) != 0 ? anim.YRot : baseline.YRot,
            ZRot = (assigned & VanillaSetupAnimRuntime.PartPoseChannel.ZRot) != 0 ? anim.ZRot : baseline.ZRot,
        };
    }

    /// <summary>
    /// World-space setupAnim delta: <c>partWorld * partLocalAnim * inverse(partWorld)</c> so
    /// <c>L' = deltaWorld * L</c> matches <c>partWorld * R_anim * cuboid</c> (ModelPart rotate at joint, not cuboid corner).
    /// Geometry IR already bakes rest pose; only assigned channels are applied relative to that baseline.
    /// </summary>
    private static bool TryBuildSetupAnimPartWorldDelta(
        string partId,
        VanillaSetupAnimRuntime.PartPose partPose,
        IReadOnlyDictionary<string, Matrix4x4> partOriginWorld,
        IReadOnlyDictionary<string, VanillaSetupAnimRuntime.PartPose> baselineParts,
        out Matrix4x4 deltaWorld)
    {
        deltaWorld = Matrix4x4.Identity;
        if (!partOriginWorld.TryGetValue(partId, out var partWorld) ||
            !Matrix4x4.Invert(partWorld, out var partInv))
        {
            return false;
        }

        if (partPose.Assigned == VanillaSetupAnimRuntime.PartPoseChannel.None)
        {
            return TryBuildSetupAnimPartWorldDeltaAbsolute(partId, partPose, partWorld, partInv, out deltaWorld);
        }

        _ = TryGetBaselinePartPose(partId, baselineParts, out var baseline);
        var effective = MergeSetupAnimEffectivePose(partPose, baseline);

        var hasPosChannel = (partPose.Assigned & (VanillaSetupAnimRuntime.PartPoseChannel.X |
                                                   VanillaSetupAnimRuntime.PartPoseChannel.Y |
                                                   VanillaSetupAnimRuntime.PartPoseChannel.Z)) != 0;
        var dx = effective.X - baseline.X;
        var dy = effective.Y - baseline.Y;
        var dz = effective.Z - baseline.Z;
        var hasPos = hasPosChannel &&
                     (MathF.Abs(dx) > SetupAnimBaselineEpsilon ||
                      MathF.Abs(dy) > SetupAnimBaselineEpsilon ||
                      MathF.Abs(dz) > SetupAnimBaselineEpsilon);

        var hasRotChannel = (partPose.Assigned & (VanillaSetupAnimRuntime.PartPoseChannel.XRot |
                                                   VanillaSetupAnimRuntime.PartPoseChannel.YRot |
                                                   VanillaSetupAnimRuntime.PartPoseChannel.ZRot)) != 0;
        Matrix4x4 targetRot = Matrix4x4.Identity;
        Matrix4x4 baselineRot = Matrix4x4.Identity;
        var hasTargetRot = hasRotChannel &&
                           TryBuildSetupAnimRotationMatrix(partId, effective, out targetRot);
        var hasBaselineRot = TryBuildSetupAnimRotationMatrix(partId, baseline, out baselineRot);

        if (!hasRotChannel && !hasPos)
        {
            return false;
        }

        var partLocal = Matrix4x4.Identity;
        if (hasPos)
        {
            partLocal = EntityParityTemplate.T(dx, dy, dz);
        }

        if (hasTargetRot)
        {
            var rotDelta = hasBaselineRot && Matrix4x4.Invert(baselineRot, out var baselineRotInv)
                ? EntityParityTemplate.Mul(targetRot, baselineRotInv)
                : targetRot;
            partLocal = hasPos ? EntityParityTemplate.Mul(partLocal, rotDelta) : rotDelta;
        }

        deltaWorld = EntityParityTemplate.Mul(EntityParityTemplate.Mul(partWorld, partLocal), partInv);
        return true;
    }

    private static bool TryBuildSetupAnimPartWorldDeltaAbsolute(
        string partId,
        VanillaSetupAnimRuntime.PartPose partPose,
        Matrix4x4 partWorld,
        Matrix4x4 partInv,
        out Matrix4x4 deltaWorld)
    {
        deltaWorld = Matrix4x4.Identity;

        var hasPos = partPose.X != 0f || partPose.Y != 0f || partPose.Z != 0f;
        var hasRot = TryBuildSetupAnimRotationMatrix(partId, partPose, out var localRot);
        if (!hasRot && !hasPos)
        {
            return false;
        }

        var partLocal = Matrix4x4.Identity;
        if (hasPos)
        {
            partLocal = EntityParityTemplate.T(partPose.X, partPose.Y, partPose.Z);
        }

        if (hasRot)
        {
            partLocal = hasPos ? EntityParityTemplate.Mul(partLocal, localRot) : localRot;
        }

        deltaWorld = EntityParityTemplate.Mul(EntityParityTemplate.Mul(partWorld, partLocal), partInv);
        return true;
    }

    private static bool TryBuildSetupAnimRotationMatrix(

        string partId,

        VanillaSetupAnimRuntime.PartPose partPose,

        out Matrix4x4 localRot)

    {

        localRot = Matrix4x4.Identity;

        if (IsChickenHeadFamilyPartId(partId))

        {

            if (partPose is { XRot: 0f, YRot: 0f, ZRot: 0f })

            {

                return false;

            }



            localRot = EntityParityTemplate.Er(partPose.XRot, partPose.YRot, 0f);

            return true;

        }



        if (partId.Contains("wing", StringComparison.Ordinal))

        {

            if (partPose is { XRot: 0f, YRot: 0f, ZRot: 0f })

            {

                return false;

            }



            localRot = EntityParityTemplate.Er(partPose.XRot, partPose.YRot, partPose.ZRot);

            return true;

        }



        if (partId.Contains("leg", StringComparison.Ordinal) || string.Equals(partId, "tail", StringComparison.Ordinal))

        {

            var hasX = partPose.XRot != 0f;

            var hasZ = partPose.ZRot != 0f;

            if (!hasX && !hasZ)

            {

                return false;

            }



            localRot = Matrix4x4.Identity;

            if (hasX)

            {

                localRot = EntityParityTemplate.Rx(partPose.XRot);

            }



            if (hasZ)

            {

                localRot = hasX

                    ? EntityParityTemplate.Mul(localRot, EntityParityTemplate.Rz(partPose.ZRot))

                    : EntityParityTemplate.Rz(partPose.ZRot);

            }



            return true;

        }



        if (string.Equals(partId, "head", StringComparison.Ordinal) || string.Equals(partId, "body", StringComparison.Ordinal))

        {

            if (partPose is { XRot: 0f, YRot: 0f, ZRot: 0f })

            {

                return false;

            }



            localRot = EntityParityTemplate.Er(partPose.XRot, partPose.YRot, partPose.ZRot);

            return true;

        }



        if (partPose.XRot != 0f)

        {

            localRot = EntityParityTemplate.Rx(partPose.XRot);

            return true;

        }



        if (partPose.YRot != 0f)

        {

            localRot = EntityParityTemplate.Ry(partPose.YRot);

            return true;

        }



        if (partPose.ZRot != 0f)

        {

            localRot = EntityParityTemplate.Rz(partPose.ZRot);

            return true;

        }



        return false;

    }
}
