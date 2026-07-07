using System.Numerics;
using System.Text.Json;

// ReSharper disable CheckNamespace

namespace AutoPBR.Core.Preview;

internal sealed partial class EntityModelRuntime
{
    private static class IllagerPreviewPoseSupport
    {
        private static bool ShouldEmitIllagerPartCuboid(string partId, EntityIllagerPreviewArmPose armPose)
        {
            if (string.Equals(partId, "hat", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var showFoldedArms = armPose == EntityIllagerPreviewArmPose.Crossed;
            if (showFoldedArms)
            {
                return !string.Equals(partId, "left_arm", StringComparison.OrdinalIgnoreCase) &&
                       !string.Equals(partId, "right_arm", StringComparison.OrdinalIgnoreCase);
            }

            return !string.Equals(partId, "arms", StringComparison.OrdinalIgnoreCase) &&
                   !string.Equals(partId, "left_shoulder", StringComparison.OrdinalIgnoreCase);
        }

        public static GeometryIrMeshEmitOptions CreateIllagerParityEmitOptions(
            GeometryIrMeshEmitOptions baseOptions,
            string? normalizedAssetPath,
            string? builderMethod,
            float idlePhase01,
            float animationTimeSeconds,
            float wave)
        {
            var norm = normalizedAssetPath?.Replace('\\', '/').TrimStart('/') ?? "";
            var armPose = EntityPreviewPoseCatalog.ResolveEffectiveIllagerArmPose(
                norm,
                builderMethod,
                EntityPreviewBuildContext.CurrentPoseId);
            ComputeIllagerPreviewArmRotations(
                armPose,
                idlePhase01,
                animationTimeSeconds,
                wave,
                isRiding: false,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out var raX,
                out var raY,
                out var raZ,
                out var laX,
                out var laY,
                out var laZ);

            return baseOptions with
            {
                ShouldEmitPartCuboids = partId => ShouldEmitIllagerPartCuboid(partId, armPose),
                TryGetPartPoseOverride = armPose == EntityIllagerPreviewArmPose.Crossed
                    ? null
                    : (partId, world) => ApplyIllagerSeparateArmPoseOverride(
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

        private static Matrix4x4 ApplyIllagerSeparateArmPoseOverride(
            string partId,
            Matrix4x4 world,
            float raX,
            float raY,
            float raZ,
            float laX,
            float laY,
            float laZ)
        {
            if (string.Equals(partId, "right_arm", StringComparison.OrdinalIgnoreCase))
            {
                return HumanoidPreviewPoseSupport.ApplyPartRotationAtJoint(
                    world,
                    EntityParityTemplate.Er(raX, raY, raZ));
            }

            if (string.Equals(partId, "left_arm", StringComparison.OrdinalIgnoreCase))
            {
                return HumanoidPreviewPoseSupport.ApplyPartRotationAtJoint(
                    world,
                    EntityParityTemplate.Er(laX, laY, laZ));
            }

            return world;
        }

        public static void StripSetupAnimArmChannels(VanillaSetupAnimRuntime.PoseResult pose)
        {
            pose.Parts.Remove("rightArm");
            pose.Parts.Remove("leftArm");
            pose.Parts.Remove("arms");
            pose.Parts.Remove("right_arm");
            pose.Parts.Remove("left_arm");
        }

        public static bool TryApplyArmPoseToGeometryIrMesh(
            MergedJavaBlockModel merged,
            JsonElement geometryRoot,
            GeometryIrMeshEmitOptions emitOptions,
            EntityIllagerPreviewArmPose armPose,
            float idlePhase01,
            float animationTimeSeconds,
            float wave)
        {
            if (armPose == EntityIllagerPreviewArmPose.Crossed)
            {
                return true;
            }

            ComputeIllagerPreviewArmRotations(
                armPose,
                idlePhase01,
                animationTimeSeconds,
                wave,
                isRiding: false,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
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
            if (TryBuildIllagerArmRotationDelta("right_arm", raX, raY, raZ, partOriginWorld, baselineParts, out var rightDelta))
            {
                partDeltas["right_arm"] = rightDelta;
            }

            if (TryBuildIllagerArmRotationDelta("left_arm", laX, laY, laZ, partOriginWorld, baselineParts, out var leftDelta))
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

        private static bool TryBuildIllagerArmRotationDelta(
            string partId,
            float xRot,
            float yRot,
            float zRot,
            Dictionary<string, Matrix4x4> partOriginWorld,
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
            var baselineRot = EntityParityTemplate.Er(baseline.XRot, baseline.YRot, baseline.ZRot);
            var targetRot = EntityParityTemplate.Er(xRot, yRot, zRot);
            var rotDelta = Matrix4x4.Invert(baselineRot, out var baselineRotInv)
                ? EntityParityTemplate.Mul(targetRot, baselineRotInv)
                : targetRot;
            deltaWorld = EntityParityTemplate.Mul(EntityParityTemplate.Mul(partWorld, rotDelta), partInv);
            return true;
        }

        public static void ComputeIllagerPreviewArmRotations(
            EntityIllagerPreviewArmPose armPose,
            float idlePhase01,
            float animationTimeSeconds,
            float wave,
            bool isRiding,
            out float rlX,
            out float rlY,
            out float rlZ,
            out float llX,
            out float llY,
            out float llZ,
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
            var showFoldedArms = armPose == EntityIllagerPreviewArmPose.Crossed;

            if (isRiding)
            {
                raX = laX = -0.62831855f;
                raY = laY = raZ = laZ = 0f;
                rlX = llX = -1.4137167f;
                rlY = 0.31415927f;
                llY = -0.31415927f;
                rlZ = 0.07853982f;
                llZ = -0.07853982f;
            }
            else
            {
                rlX = MathF.Cos(walkPos * k6662) * 1.4f * walkSpeed * 0.5f;
                llX = MathF.Cos(walkPos * k6662 + MathF.PI) * 1.4f * walkSpeed * 0.5f;
                rlY = rlZ = llY = llZ = 0f;
                if (showFoldedArms)
                {
                    raX = raY = raZ = laX = laY = laZ = 0f;
                }
                else
                {
                    raX = MathF.Cos(walkPos * k6662 + MathF.PI) * 2f * walkSpeed * 0.5f;
                    laX = MathF.Cos(walkPos * k6662) * 2f * walkSpeed * 0.5f;
                    raY = raZ = laY = laZ = 0f;
                }
            }

            switch (armPose)
            {
                case EntityIllagerPreviewArmPose.ArmsAtSide:
                case EntityIllagerPreviewArmPose.Crossed:
                    break;
                case EntityIllagerPreviewArmPose.AttackingEmptyHands:
                    {
                        var attackT = Math.Clamp(0.35f + idlePhase01 * 0.45f + wave * 0.2f, 0f, 1f);
                        IllagerAnimateZombieArms(
                            ref laX,
                            ref laY,
                            ref laZ,
                            ref raX,
                            ref raY,
                            ref raZ,
                            useFifteenDivisor: true,
                            swingIsStab: false,
                            attackTime: attackT,
                            ageInTicks: ageInTicks);
                        break;
                    }
                case EntityIllagerPreviewArmPose.AttackingWeapon:
                    {
                        raX = raY = raZ = laX = laY = laZ = 0f;
                        var attackAnim = Math.Clamp(0.2f + idlePhase01 * 0.55f + wave * 0.25f, 0f, 1f);
                        IllagerSwingWeaponDown(
                            ref raX,
                            ref raY,
                            ref raZ,
                            ref laX,
                            ref laY,
                            ref laZ,
                            mainHandIsRight: true,
                            attackAnim,
                            ageInTicks);
                        break;
                    }
                case EntityIllagerPreviewArmPose.Spellcasting:
                    {
                        var sc = MathF.Cos(ageInTicks * k6662);
                        raX = sc * 0.25f;
                        laX = sc * 0.25f;
                        raZ = 2.3561945f;
                        laZ = -2.3561945f;
                        raY = laY = 0f;
                        break;
                    }
                case EntityIllagerPreviewArmPose.BowAndArrow:
                    {
                        raY = -0.1f + headYawRad;
                        raX = -1.5707964f + headPitchRad;
                        laX = -0.9424779f + headPitchRad;
                        laY = headYawRad - 0.4f;
                        laZ = 1.5707964f;
                        raZ = 0f;
                        break;
                    }
                case EntityIllagerPreviewArmPose.CrossbowHold:
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
                case EntityIllagerPreviewArmPose.CrossbowCharge:
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
                case EntityIllagerPreviewArmPose.Celebrating:
                    {
                        var cc = MathF.Cos(ageInTicks * k6662);
                        raX = cc * 0.05f;
                        laX = cc * 0.05f;
                        raZ = 2.670354f;
                        laZ = -2.3561945f;
                        raY = laY = 0f;
                        break;
                    }
            }
        }
    }
}
