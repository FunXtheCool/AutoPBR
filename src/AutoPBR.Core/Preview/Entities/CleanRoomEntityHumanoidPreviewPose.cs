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
                return ApplyPartRotationAtJoint(world, BuildHumanoidArmRotationMatrix(raX, raY, raZ));
            }

            if (string.Equals(partId, "left_arm", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(partId, "leftArm", StringComparison.OrdinalIgnoreCase))
            {
                return ApplyPartRotationAtJoint(world, BuildHumanoidArmRotationMatrix(laX, laY, laZ));
            }

            return world;
        }

        /// <summary>
        /// Pose overrides receive texel-row part worlds; rotate in block space then re-scale translation
        /// so shoulder pivots stay fixed (texel×block rotation mixes units and orbits limbs).
        /// </summary>
        internal static Matrix4x4 ApplyPartRotationAtJoint(Matrix4x4 worldTexel, Matrix4x4 rotationBlock)
        {
            var block = TexelRowAffineToBlock(worldTexel);
            block = EntityParityTemplate.Mul(block, rotationBlock);
            return BlockRowAffineToTexel(block);
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
            float wave,
            bool isBaby = false)
        {
            ComputeHumanoidPreviewArmRotations(
                armPose,
                idlePhase01,
                animationTimeSeconds,
                wave,
                isBaby,
                out var raX,
                out var raY,
                out var raZ,
                out var laX,
                out var laY,
                out var laZ);

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
                    DepthLayerKind = e.DepthLayerKind,
                    LayerOrdinal = e.LayerOrdinal,
                    CastsShadow = e.CastsShadow,
                    ShellInflateTexels = e.ShellInflateTexels,
                    EnableParallax = e.EnableParallax,
                    MirrorCuboidUv = e.MirrorCuboidUv,
                };
            }

            return true;
        }

        private static bool TryBuildHumanoidArmRotationDelta(
            string partId,
            float xRot,
            float yRot,
            float zRot,
            Dictionary<string, Matrix4x4> partOriginWorld,
            Dictionary<string, VanillaSetupAnimRuntime.PartPose> baselineParts,
            out Matrix4x4 deltaWorld)
        {
            deltaWorld = Matrix4x4.Identity;
            if (!partOriginWorld.ContainsKey(partId))
            {
                return false;
            }

            _ = TryGetBaselinePartPose(partId, baselineParts, out var baseline);
            var posedBlock = EntityParityTemplate.ModelPartRenderLocalBlock(
                baseline.X,
                baseline.Y,
                baseline.Z,
                xRot,
                yRot,
                zRot);
            var bindBlock = EntityParityTemplate.ModelPartRenderLocalBlock(
                baseline.X,
                baseline.Y,
                baseline.Z,
                baseline.XRot,
                baseline.YRot,
                baseline.ZRot);
            var posedWorld = BlockRowAffineToTexel(posedBlock);
            var bindWorldFromBaseline = BlockRowAffineToTexel(bindBlock);
            if (!Matrix4x4.Invert(bindWorldFromBaseline, out var bindInv))
            {
                return false;
            }

            deltaWorld = EntityParityTemplate.Mul(posedWorld, bindInv);
            return true;
        }

        /// <summary>
        /// Emit-time cuboid overrides bake rotation into <see cref="EntityCuboid.XRot"/> (opposite sign from
        /// ModelPart <c>xRot</c> for zombie-family arms on adult HumanoidModel cuboids).
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

        /// <summary>
        /// Base <c>ModelPart.xRot</c> from <c>AnimationUtils.animateZombieArms</c> at <c>attackTime=0</c>
        /// (<c>-PI / (baby ? 1.5 : 2.25)</c>, 26.1.2 <c>client.jar</c>).
        /// </summary>
        internal static float ResolveZombieArmPoseBaseRad(bool isBaby) =>
            -MathF.PI / (isBaby ? 1.5f : 2.25f);

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
            out float laZ) =>
            ComputeHumanoidPreviewArmRotations(
                armPose,
                idlePhase01,
                animationTimeSeconds,
                wave,
                isBaby: false,
                out raX,
                out raY,
                out raZ,
                out laX,
                out laY,
                out laZ);

        public static void ComputeHumanoidPreviewArmRotations(
            EntityHumanoidPreviewArmPose armPose,
            float idlePhase01,
            float animationTimeSeconds,
            float wave,
            bool isBaby,
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
                    raX = laX = ResolveZombieArmPoseBaseRad(isBaby);
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
            float wave,
            bool isBaby = false)
        {
            ComputeHumanoidPreviewArmRotations(
                armPose,
                idlePhase01,
                animationTimeSeconds,
                wave,
                isBaby,
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
