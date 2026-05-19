// ReSharper disable CheckNamespace



using System.Numerics;

using System.Text.Json;



namespace AutoPBR.Core.Preview;



internal sealed partial class CleanRoomEntityModelRuntime

{

    /// <summary>

    /// Part-origin world matrices for geometry IR (same DFS visit order as geometry IR mesh emission).

    /// </summary>

    private static class GeometryIrPartWorldPoseIndex

    {

        public static IReadOnlyDictionary<string, Matrix4x4> Build(JsonElement geometryRoot)

        {

            var map = new Dictionary<string, Matrix4x4>(StringComparer.Ordinal);

            if (!geometryRoot.TryGetProperty("roots", out var roots) || roots.ValueKind != JsonValueKind.Array)

            {

                return map;

            }



            foreach (var rootPart in roots.EnumerateArray())

            {

                VisitPart(rootPart, Matrix4x4.Identity, map);

            }



            return map;

        }



        private static void VisitPart(JsonElement part, Matrix4x4 parentWorld, Dictionary<string, Matrix4x4> sink)

        {

            var world = parentWorld;

            if (part.TryGetProperty("pose", out var poseEl) &&
                TryComposePartPosePublic(poseEl, out var local))
            {
                world = EntityParityTemplate.Mul(parentWorld, local);
            }



            var partId = part.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";

            if (partId.Length > 0)

            {

                sink[partId] = world;

            }



            if (!part.TryGetProperty("children", out var children) || children.ValueKind != JsonValueKind.Array)

            {

                return;

            }



            foreach (var child in children.EnumerateArray())

            {

                VisitPart(child, world, sink);

            }

        }

    }



    /// <summary>

    /// Applies lifted setupAnim part poses to IR-emitted cuboids (part.id keys, pivot from part-origin world index).

    /// </summary>

    private static bool ApplySetupAnimToGeometryIrMesh(

        MergedJavaBlockModel merged,

        JsonElement geometryRoot,

        VanillaSetupAnimRuntime.PoseResult pose,

        IReadOnlyDictionary<string, Matrix4x4> partOriginWorld,

        GeometryIrMeshEmitOptions emitOptions)

    {

        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(geometryRoot, emitOptions);

        if (partIds.Count != merged.Elements.Count)

        {

            return false;

        }



        for (var i = 0; i < merged.Elements.Count; i++)
        {
            var partId = partIds[i];
            if (!TryGetSetupAnimPartPose(partId, pose, out var partPose))
            {
                continue;
            }

            if (!TryBuildSetupAnimPartWorldDelta(partId, partPose, partOriginWorld, out var deltaWorld))
            {
                continue;
            }

            var e = merged.Elements[i];
            merged.Elements[i] = new ModelElement
            {
                From = e.From,
                To = e.To,
                Faces = e.Faces,
                LocalToParent = EntityParityTemplate.Mul(deltaWorld, e.LocalToParent),
            };
        }

        return true;
    }

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



    /// <summary>Parity-catalog: apply idle preview animation drivers to IR-emitted chicken elements.</summary>

    private static void ApplyChickenGeometryIrPreviewAnimPass(

        MergedJavaBlockModel merged,

        JsonElement geometryRoot,

        bool _,

        float headPitchRad,

        float headYawRad,

        float wingZRadians,

        float rightLegPitchRad,

        float leftLegPitchRad,

        GeometryIrMeshEmitOptions emitOptions)

    {

        var pose = new VanillaSetupAnimRuntime.PoseResult
        {
            Parts =
            {
                ["head"] = new VanillaSetupAnimRuntime.PartPose { XRot = headPitchRad, YRot = headYawRad },
                ["rightWing"] = new VanillaSetupAnimRuntime.PartPose { ZRot = wingZRadians },
                ["leftWing"] = new VanillaSetupAnimRuntime.PartPose { ZRot = -wingZRadians },
                ["rightLeg"] = new VanillaSetupAnimRuntime.PartPose { XRot = rightLegPitchRad },
                ["leftLeg"] = new VanillaSetupAnimRuntime.PartPose { XRot = leftLegPitchRad },
            },
        };



        _ = ApplySetupAnimToGeometryIrMesh(
            merged,
            geometryRoot,
            pose,
            GeometryIrPartWorldPoseIndex.Build(geometryRoot),
            emitOptions);

    }



    private static bool TryApplySetupAnimGeometryIrPreviewPass(
        EntityTextureParityRule parityRule,
        string geometryIrOfficialJvm,
        MergedJavaBlockModel merged,
        JsonElement geometryRoot,
        bool isBaby,
        float animationTimeSeconds,
        float idlePhase01,
        float wave,
        GeometryIrMeshEmitOptions emitOptions)
    {
        var modelJvm = SetupAnimParityResolver.ResolveModelJvmForPreview(
            parityRule.BuilderMethod,
            parityRule.DeobfuscatedModelClass,
            isBaby,
            geometryIrOfficialJvm);
        if (!SetupAnimDocumentLoader.TryLoad(modelJvm, out _))
        {
            return false;
        }

        var state = PreviewRenderStateSynthesis.ForLivingWalk(animationTimeSeconds, idlePhase01, wave);
        if (!VanillaSetupAnimRuntime.TryEvaluate(
                modelJvm,
                state,
                animationTimeSeconds,
                out var pose,
                parityRule.BuilderMethod,
                isBaby))
        {
            return false;
        }

        return ApplySetupAnimToGeometryIrMesh(
            merged,
            geometryRoot,
            pose,
            GeometryIrPartWorldPoseIndex.Build(geometryRoot),
            emitOptions);
    }



    private static bool IsChickenHeadFamilyPartId(string partId) =>

        string.Equals(partId, "head", StringComparison.Ordinal) ||

        string.Equals(partId, "beak", StringComparison.Ordinal) ||

        string.Equals(partId, "red_thing", StringComparison.Ordinal);

    /// <summary>Survey helper: whether setupAnim bytecode exists and evaluates for a catalog rule.</summary>
    internal static void ProbeParityCatalogSetupAnimCapability(
        EntityTextureParityRule parityRule,
        string? resolvedGeometryJvm,
        bool isBaby,
        float animationTimeSeconds,
        float idlePhase01,
        out bool hasSetupAnimDocument,
        out bool setupAnimWouldEvaluate)
    {
        hasSetupAnimDocument = false;
        setupAnimWouldEvaluate = false;
        var modelJvm = !string.IsNullOrWhiteSpace(parityRule.DeobfuscatedModelClass)
            ? parityRule.DeobfuscatedModelClass
            : resolvedGeometryJvm ?? "";
        if (string.IsNullOrWhiteSpace(modelJvm) || !SetupAnimDocumentLoader.TryLoadOk(modelJvm, out _))
        {
            return;
        }

        hasSetupAnimDocument = true;
        var wave = Wave(animationTimeSeconds, 0.8f);
        var state = PreviewRenderStateSynthesis.ForLivingWalk(animationTimeSeconds, idlePhase01, wave);
        setupAnimWouldEvaluate = VanillaSetupAnimRuntime.TryEvaluate(
            modelJvm,
            state,
            animationTimeSeconds,
            out _,
            parityRule.BuilderMethod,
            isBaby);
    }

}


