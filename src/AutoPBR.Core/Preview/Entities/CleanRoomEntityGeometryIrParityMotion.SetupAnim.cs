using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    private static bool TryGetSetupAnimPartPose(
        string partId,
        VanillaSetupAnimRuntime.PoseResult pose,
        out VanillaSetupAnimRuntime.PartPose partPose)
    {
        if (pose.Parts.TryGetValue(partId, out partPose!))
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

            "tail1" => "tail1",

            "tail2" => "tail2",

            _ => ""

        };

        return modelPartField.Length > 0;

    }



    /// <summary>
    /// World-space setupAnim delta: <c>partWorld * partLocalAnim * inverse(partWorld)</c> so
    /// <c>L' = deltaWorld * L</c> matches <c>partWorld * R_anim * cuboid</c> (ModelPart rotate at joint, not cuboid corner).
    /// </summary>
    private static bool TryBuildSetupAnimPartWorldDelta(
        string partId,
        VanillaSetupAnimRuntime.PartPose partPose,
        IReadOnlyDictionary<string, Matrix4x4> partOriginWorld,
        out Matrix4x4 deltaWorld)
    {
        deltaWorld = Matrix4x4.Identity;
        if (!partOriginWorld.TryGetValue(partId, out var partWorld) ||
            !Matrix4x4.Invert(partWorld, out var partInv))
        {
            return false;
        }

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

            if (partPose is { ZRot: 0f })

            {

                return false;

            }



            localRot = EntityParityTemplate.Rz(partPose.ZRot);

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
