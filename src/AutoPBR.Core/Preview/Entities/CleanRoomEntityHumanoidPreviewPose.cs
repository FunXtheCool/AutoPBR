using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Models;

// ReSharper disable CheckNamespace

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    private static class HumanoidPreviewPoseSupport
    {
        private const float ItemArmX = -0.31415927f;
        private const float BlockArmX = -0.9424779f;
        private const float BlockArmY = 0.1f;
        private const float SpyglassArmX = -1.4835298f;
        private const float SpyglassArmZ = -0.5235988f;

        public static GeometryIrMeshEmitOptions CreateHumanoidParityEmitOptions(
            GeometryIrMeshEmitOptions baseOptions,
            string? builderMethod,
            float idlePhase01,
            float animationTimeSeconds,
            float wave)
        {
            var armPose = EntityPreviewPoseCatalog.ResolveEffectiveHumanoidArmPose(
                builderMethod,
                EntityPreviewBuildContext.CurrentPoseId);
            ComputeHumanoidPreviewArmRotations(
                armPose,
                idlePhase01,
                animationTimeSeconds,
                wave,
                out var raX,
                out var raY,
                out var raZ,
                out var laX,
                out var laY,
                out var laZ);
            ApplyGeometryIrHumanoidArmRotations(armPose, ref raX, ref raY, ref raZ, ref laX, ref laY, ref laZ);

            return baseOptions with
            {
                TryGetPartPoseOverride = (partId, world) => ApplyHumanoidArmPoseOverride(
                    partId,
                    world,
                    raX,
                    raY,
                    raZ,
                    laX,
                    laY,
                    laZ),
            };
        }

        private static Matrix4x4 ApplyHumanoidArmPoseOverride(
            string partId,
            Matrix4x4 world,
            float raX,
            float raY,
            float raZ,
            float laX,
            float laY,
            float laZ)
        {
            if (string.Equals(partId, "right_arm", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(partId, "rightArm", StringComparison.OrdinalIgnoreCase))
            {
                return EntityParityTemplate.Mul(world, BuildHumanoidArmRotationMatrix(raX, raY, raZ));
            }

            if (string.Equals(partId, "left_arm", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(partId, "leftArm", StringComparison.OrdinalIgnoreCase))
            {
                return EntityParityTemplate.Mul(world, BuildHumanoidArmRotationMatrix(laX, laY, laZ));
            }

            return world;
        }

        public static void StripSetupAnimArmChannels(VanillaSetupAnimRuntime.PoseResult pose)
        {
            pose.Parts.Remove("rightArm");
            pose.Parts.Remove("leftArm");
            pose.Parts.Remove("right_arm");
            pose.Parts.Remove("left_arm");
        }

    /// <summary>
    /// Player and repaired <c>HumanoidModel.createMesh</c> zombie IR use reference bind poses;
    /// <c>HumanoidModel.setupAnim</c> leg/body conjugation produces NaN world deltas against the part-origin index.
    /// </summary>
    public static void StripSetupAnimBodyLegChannels(VanillaSetupAnimRuntime.PoseResult pose)
        {
            pose.Parts.Remove("rightLeg");
            pose.Parts.Remove("leftLeg");
            pose.Parts.Remove("right_leg");
            pose.Parts.Remove("left_leg");
            pose.Parts.Remove("body");
            pose.Parts.Remove("head");
        }

        public static bool TryApplyArmPoseToGeometryIrMesh(
            MergedJavaBlockModel merged,
            JsonElement geometryRoot,
            GeometryIrMeshEmitOptions emitOptions,
            EntityHumanoidPreviewArmPose armPose,
            float idlePhase01,
            float animationTimeSeconds,
            float wave)
        {
            ComputeHumanoidPreviewArmRotations(
                armPose,
                idlePhase01,
                animationTimeSeconds,
                wave,
                out var raX,
                out var raY,
                out var raZ,
                out var laX,
                out var laY,
                out var laZ);
            ApplyGeometryIrHumanoidArmRotations(armPose, ref raX, ref raY, ref raZ, ref laX, ref laY, ref laZ);

            var partOriginWorld = GeometryIrPartWorldPoseIndex.Build(geometryRoot, emitOptions);
            var baselineParts = BuildSetupAnimBaselineParts(geometryRoot);
            var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(geometryRoot, emitOptions);
            if (partIds.Count != merged.Elements.Count)
            {
                return false;
            }

            var partDeltas = new Dictionary<string, Matrix4x4>(StringComparer.Ordinal);
            if (TryBuildHumanoidArmRotationDelta("right_arm", raX, raY, raZ, partOriginWorld, baselineParts, out var rightDelta) ||
                TryBuildHumanoidArmRotationDelta("rightArm", raX, raY, raZ, partOriginWorld, baselineParts, out rightDelta))
            {
                partDeltas["right_arm"] = rightDelta;
            }

            if (TryBuildHumanoidArmRotationDelta("left_arm", laX, laY, laZ, partOriginWorld, baselineParts, out var leftDelta) ||
                TryBuildHumanoidArmRotationDelta("leftArm", laX, laY, laZ, partOriginWorld, baselineParts, out leftDelta))
            {
                partDeltas["left_arm"] = leftDelta;
            }

            if (partDeltas.Count == 0)
            {
                return false;
            }

            var parentMap = GeometryIrPartWorldPoseIndex.BuildParentMap(geometryRoot);
            for (var i = 0; i < merged.Elements.Count; i++)
            {
                var partId = partIds[i];
                if (!TryComposeSetupAnimAncestorDeltas(partId, parentMap, partDeltas, out var composedDelta))
                {
                    continue;
                }

                var e = merged.Elements[i];
                merged.Elements[i] = new ModelElement
                {
                    From = e.From,
                    To = e.To,
                    Faces = e.Faces,
                    LocalToParent = EntityParityTemplate.Mul(composedDelta, e.LocalToParent),
                };
            }

            return true;
        }

        private static bool TryBuildHumanoidArmRotationDelta(
            string partId,
            float xRot,
            float yRot,
            float zRot,
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

            _ = TryGetBaselinePartPose(partId, baselineParts, out var baseline);
            var targetRot = BuildHumanoidArmRotationMatrix(xRot, yRot, zRot);
            var baselineRot = BuildHumanoidArmRotationMatrix(baseline.XRot, baseline.YRot, baseline.ZRot);
            var rotDelta = Matrix4x4.Invert(baselineRot, out var baselineRotInv)
                ? EntityParityTemplate.Mul(targetRot, baselineRotInv)
                : targetRot;
            deltaWorld = EntityParityTemplate.Mul(EntityParityTemplate.Mul(partWorld, rotDelta), partInv);
            return true;
        }

        /// <summary>
        /// Geometry IR applies arm <c>xRot</c> at the part joint (ModelPart convention). Rig-builder
        /// cuboids use the opposite sign on <see cref="EntityCuboid.XRot"/> for zombie-family arms.
        /// </summary>
        private static void ApplyGeometryIrHumanoidArmRotations(
            EntityHumanoidPreviewArmPose armPose,
            ref float raX,
            ref float raY,
            ref float raZ,
            ref float laX,
            ref float laY,
            ref float laZ)
        {
            if (armPose != EntityHumanoidPreviewArmPose.ZombieArms)
            {
                return;
            }

            raX = -raX;
            laX = -laX;
        }

        private static Matrix4x4 BuildHumanoidArmRotationMatrix(float xRot, float yRot, float zRot) =>
            yRot == 0f && zRot == 0f
                ? EntityParityTemplate.Rx(xRot)
                : EntityParityTemplate.Er(xRot, yRot, zRot);

        public static void ComputeHumanoidPreviewArmRotations(
            EntityHumanoidPreviewArmPose armPose,
            float idlePhase01,
            float animationTimeSeconds,
            float wave,
            out float raX,
            out float raY,
            out float raZ,
            out float laX,
            out float laY,
            out float laZ)
        {
            const float k6662 = 0.6662f;
            const float degToRad = 0.017453292f;
            var (walkPos, walkSpeed) = ComputePreviewEntityWalkCycle(animationTimeSeconds, idlePhase01, wave);
            var headYawRad = (wave * 8f + idlePhase01 * 6f) * degToRad;
            var headPitchRad = (idlePhase01 * 10f + wave * 4f) * degToRad;
            var ageInTicks = animationTimeSeconds * 20f;

            raX = MathF.Cos(walkPos * k6662 + MathF.PI) * 2f * walkSpeed * 0.5f;
            laX = MathF.Cos(walkPos * k6662) * 2f * walkSpeed * 0.5f;
            raY = raZ = laY = laZ = 0f;

            switch (armPose)
            {
                case EntityHumanoidPreviewArmPose.Empty:
                    break;
                case EntityHumanoidPreviewArmPose.ZombieArms:
                    raX = laX = 1.15f + idlePhase01 * 0.55f + wave * 0.18f;
                    raY = laY = raZ = laZ = 0f;
                    break;
                case EntityHumanoidPreviewArmPose.Item:
                    raX = ItemArmX;
                    raY = raZ = 0f;
                    laX = MathF.Cos(walkPos * k6662) * 2f * walkSpeed * 0.5f;
                    laY = laZ = 0f;
                    break;
                case EntityHumanoidPreviewArmPose.Block:
                    raX = laX = BlockArmX;
                    raY = -BlockArmY;
                    laY = BlockArmY;
                    raZ = laZ = 0f;
                    break;
                case EntityHumanoidPreviewArmPose.BowAndArrow:
                    raY = -0.1f + headYawRad;
                    raX = -1.5707964f + headPitchRad;
                    laX = -0.9424779f + headPitchRad;
                    laY = headYawRad - 0.4f;
                    laZ = 1.5707964f;
                    raZ = 0f;
                    break;
                case EntityHumanoidPreviewArmPose.CrossbowHold:
                    IllagerAnimateCrossbowHold(
                        ref raX,
                        ref raY,
                        ref raZ,
                        ref laX,
                        ref laY,
                        ref laZ,
                        headYawRad,
                        headPitchRad,
                        rightHanded: true);
                    break;
                case EntityHumanoidPreviewArmPose.CrossbowCharge:
                    IllagerAnimateCrossbowCharge(
                        ref raX,
                        ref raY,
                        ref raZ,
                        ref laX,
                        ref laY,
                        ref laZ,
                        maxCrossbowChargeDuration: 25f,
                        ticksUsingItem: (animationTimeSeconds * 20f) % 26f,
                        rightHanded: true);
                    break;
                case EntityHumanoidPreviewArmPose.Spyglass:
                    raZ = SpyglassArmZ;
                    raX = SpyglassArmX;
                    laX = MathF.Cos(walkPos * k6662) * 2f * walkSpeed * 0.5f;
                    laY = laZ = 0f;
                    break;
                default:
                    break;
            }

            if (armPose is EntityHumanoidPreviewArmPose.Empty or EntityHumanoidPreviewArmPose.Item)
            {
                IllagerBobArms(ref raX, ref raZ, ref laX, ref laZ, ageInTicks);
            }
        }

        public static float ResolveHumanoidArmLiftRad(
            EntityHumanoidPreviewArmPose armPose,
            float idlePhase01,
            float animationTimeSeconds,
            float wave)
        {
            ComputeHumanoidPreviewArmRotations(
                armPose,
                idlePhase01,
                animationTimeSeconds,
                wave,
                out var raX,
                out _,
                out _,
                out _,
                out _,
                out _);
            return raX;
        }
    }
}
