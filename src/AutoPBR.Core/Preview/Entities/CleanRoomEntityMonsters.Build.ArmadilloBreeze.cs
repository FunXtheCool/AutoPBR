using System.Numerics;
// ReSharper disable CheckNamespace



namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{

    private static MergedJavaBlockModel BuildBabyArmadillo(string texRef, float headPitch, float tailWalkPitchRad)
    {
        _ = tailWalkPitchRad;
        var p = BabyProfile.Adult;
        var b = new RigBuilder(64, 64);
        var root = Matrix4x4.Identity;

        var bodyPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 20f, 0.5f));
        new EntityCuboid(-2f, -3.5f, 5f - 0.01f, 2f, 3.5f, 5.3f + 0.01f, 0, 0, UvSizeW: 4, UvSizeH: 7, UvSizeD: 1).Emit(b, bodyPose, p.BodyScale);
        new EntityCuboid(-2.5f, -2f, -3f, 2.5f, 2f, 3f, 0, 11, UvSizeW: 5, UvSizeH: 4, UvSizeD: 6).Emit(b, bodyPose, p.BodyScale);

        var rightEarCubePose = EntityParityTemplate.Mul(
            bodyPose,
            EntityParityTemplate.Mul(EntityParityTemplate.T(0f, 1.5f, 1f), EntityParityTemplate.Er(-1.0472f, 0f, 0f)));
        new EntityCuboid(-0.5f, -0.5f, -2f, 0.5f, 0.5f, 2f, 22, 11, UvSizeW: 1, UvSizeH: 1, UvSizeD: 4).Emit(b, rightEarCubePose, p.HeadScale);

        var headPose = EntityParityTemplate.Mul(
            EntityParityTemplate.Mul(bodyPose, EntityParityTemplate.T(0f, -2f, -11f)),
            EntityParityTemplate.Mul(EntityParityTemplate.Er(0.7417649f, 0f, 0f), EntityParityTemplate.Rx(headPitch)));
        new EntityCuboid(-1f, -2f, -4f, 1f, 0f, 0f, 20, 17, UvSizeW: 2, UvSizeH: 2, UvSizeD: 4).Emit(b, headPose, p.HeadScale);

        const float earZ = 0.06f;
        var rightEarPose = EntityParityTemplate.Mul(
            headPose,
            EntityParityTemplate.Mul(EntityParityTemplate.T(-1f, -2f, -0.3f), EntityParityTemplate.Er(-0.4363f, -0.1134f, 0.0524f)));
        new EntityCuboid(-1.8f, -2f, -earZ, 0.2f, 1f, earZ, 28, 8, UvSizeW: 2, UvSizeH: 3, UvSizeD: 1).Emit(b, rightEarPose, p.HeadScale);

        var leftEarPose = EntityParityTemplate.Mul(
            headPose,
            EntityParityTemplate.Mul(EntityParityTemplate.T(1f, -2f, -0.3f), EntityParityTemplate.Er(-0.4363f, 0.1134f, -0.0524f)));
        new EntityCuboid(-0.2f, -2f, -earZ, 1.8f, 1f, earZ, 28, 8, UvSizeW: 2, UvSizeH: 3, UvSizeD: 1, MirrorUv: true).Emit(b, leftEarPose, p.HeadScale);

        var rhLeg = EntityParityTemplate.Mul(root, EntityParityTemplate.T(-1.5f, 22f, 2.5f));
        new EntityCuboid(-1f, 0f, -1f, 1f, 2f, 1f, 20, 27, UvSizeW: 2, UvSizeH: 2, UvSizeD: 2).Emit(b, rhLeg, p.LegScale);
        var lhLeg = EntityParityTemplate.Mul(root, EntityParityTemplate.T(1.5f, 22f, 2.5f));
        new EntityCuboid(-1f, 0f, -1f, 1f, 2f, 1f, 20, 27, UvSizeW: 2, UvSizeH: 2, UvSizeD: 2, MirrorUv: true).Emit(b, lhLeg, p.LegScale);
        var rfLeg = EntityParityTemplate.Mul(root, EntityParityTemplate.T(1.5f, 22f, -1.5f));
        new EntityCuboid(-1f, 0f, -1f, 1f, 2f, 1f, 20, 23, UvSizeW: 2, UvSizeH: 2, UvSizeD: 2).Emit(b, rfLeg, p.LegScale);
        var lfLeg = EntityParityTemplate.Mul(root, EntityParityTemplate.T(-1.5f, 22f, -1.5f));
        new EntityCuboid(-1f, 0f, -1f, 1f, 2f, 1f, 24, 0, UvSizeW: 2, UvSizeH: 2, UvSizeD: 2, MirrorUv: true).Emit(b, lfLeg, p.LegScale);

        var rollPose = EntityParityTemplate.Mul(root, EntityParityTemplate.T(0f, 20.7f, 0.5f));
        new EntityCuboid(-3f, -3f, 6f, 3f, 3f, 6.3f, 0, 25, UvSizeW: 6, UvSizeH: 6, UvSizeD: 1).Emit(b, rollPose, p.BodyScale);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }


    private static MergedJavaBlockModel BuildArmadillo(string texRef, MinecraftNativeProfile profile, bool isBaby, float headPitch, float tailWalkPitchRad = 0f)
    {
        if (UsesPostBabyModelUpdate(profile) && isBaby)
        {
            return BuildBabyArmadillo(texRef, headPitch, tailWalkPitchRad);
        }

        var p = UsesPostBabyModelUpdate(profile)
            ? (isBaby ? new BabyProfile(0.72f, 1.10f, 0.74f) : BabyProfile.Adult)
            : (isBaby ? new BabyProfile(0.80f, 1.04f, 0.82f) : BabyProfile.Adult);
        var b = new RigBuilder(64, 64);
        // ArmadilloModel.createBodyLayer (~1.21.4): body 8x8x12, tiny tail, compact head/ears, plus roll-up cube part.
        var bodyPose = Matrix4x4.CreateTranslation(0f, 21f, 4f);
        new EntityCuboid(-4f, -7f, -10f, 4f, 1f, 2f, 0, 20).Emit(b, bodyPose, p.BodyScale);
        new EntityCuboid(-4f, -7f, -10f, 4f, 1f, 2f, 0, 40).Emit(b, bodyPose, p.BodyScale);

        var tailPitch = 0.5061f + tailWalkPitchRad;
        new EntityCuboid(-0.5f, -0.0865f, 0.0933f, 0.5f, 5.9135f, 1.0933f, 44, 53).Emit(b, Matrix4x4.Multiply(bodyPose, Matrix4x4.Multiply(Matrix4x4.CreateTranslation(0f, -3f, 1f), Matrix4x4.CreateRotationX(tailPitch))), p.BodyScale); // tail

        var headPose = Matrix4x4.Multiply(
            Matrix4x4.Multiply(bodyPose, Matrix4x4.CreateTranslation(0f, -2f, -11f)),
            Matrix4x4.CreateRotationX(headPitch));
        new EntityCuboid(-1.5f, -1f, -1f, 1.5f, 4f, 1f, 43, 15).Emit(b, Matrix4x4.Multiply(headPose, Matrix4x4.CreateRotationX(-0.3927f)), p.HeadScale); // head cube
        new EntityCuboid(-2f, -3f, 0f, 0f, 2f, 1f, 43, 10).Emit(b, Matrix4x4.Multiply(headPose, Matrix4x4.Multiply(Matrix4x4.CreateTranslation(-1f, -1f, 0f), Matrix4x4.Multiply(Matrix4x4.CreateRotationX(0.1886f), Matrix4x4.CreateRotationY(-0.3864f)))), p.HeadScale);
        new EntityCuboid(0f, -3f, 0f, 2f, 2f, 1f, 47, 10).Emit(b, Matrix4x4.Multiply(headPose, Matrix4x4.Multiply(Matrix4x4.CreateTranslation(1f, -2f, 0f), Matrix4x4.Multiply(Matrix4x4.CreateRotationX(0.1886f), Matrix4x4.CreateRotationY(0.3864f)))), p.HeadScale);

        new EntityCuboid(-1f, 0f, -1f, 1f, 3f, 1f, 51, 31).Emit(b, Matrix4x4.CreateTranslation(-2f, 21f, 4f), p.LegScale);
        new EntityCuboid(-1f, 0f, -1f, 1f, 3f, 1f, 42, 31).Emit(b, Matrix4x4.CreateTranslation(2f, 21f, 4f), p.LegScale);
        new EntityCuboid(-1f, 0f, -1f, 1f, 3f, 1f, 51, 43).Emit(b, Matrix4x4.CreateTranslation(-2f, 21f, -4f), p.LegScale);
        new EntityCuboid(-1f, 0f, -1f, 1f, 3f, 1f, 42, 43).Emit(b, Matrix4x4.CreateTranslation(2f, 21f, -4f), p.LegScale);

        // Roll-up cube used by animation state.
        new EntityCuboid(-5f, -10f, -6f, 5f, 0f, 4f, 0, 0).Emit(b, Matrix4x4.CreateTranslation(0f, 24f, 0f), p.BodyScale);
        return ApplyLivingEntityRendererPreviewBasis(b.Build(texRef));
    }

    /// <summary>
    /// <c>BreezeModel</c> (javap <c>gbm</c> 1.21.4; named <c>BreezeModel</c> 26.1.2): vanilla splits layers — <c>createBodyLayer</c> is <c>32×32</c> and retains only <c>head</c> + <c>rods</c>;
    /// <c>createWindLayer</c> is <c>128×128</c> and retains <c>wind_body</c> → <c>wind_bottom</c> → <c>wind_mid</c> → <c>wind_top</c> (<c>wind_top</c> child pose <c>T(0,−6,0)</c> under <c>wind_mid</c>, not −7).
    /// <c>createEyesLayer</c> is <c>32×32</c>. Preview merges body + <c>#wind</c> (sibling <c>breeze_wind</c>) + <c>#eyes</c>; <c>breeze_wind.png</c> / <c>breeze_eyes.png</c> paths build those layers alone.
    /// Wind tiers sample vanilla idle <c>BreezeAnimation.IDLE</c> <c>wind_mid</c>/<c>wind_top</c> POSITION keyframes from shipped IR
    /// (<see cref="DefinitionAnimationPreviewSampling"/>) from lifted <c>BreezeAnimation.IDLE</c> when present.
    /// Head pitch can add <c>BreezeAnimation.SHOOT</c> <c>head</c> ROTATION X from IR when present; <c>head</c> POSITION from the same clip adds on the head pivot.
    /// </summary>
    private static MergedJavaBlockModel BuildBreeze(
        string normalizedAssetPath,
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float swirl,
        float windAnimTimeSeconds,
        float shootHeadAdditivePitchRad,
        Vector3 shootHeadAdditiveTranslate = default)
    {
        _ = isBaby;
        var norm = normalizedAssetPath.Replace('\\', '/').TrimStart('/');
        var isEyesTexture = norm.Contains("breeze_eyes", StringComparison.OrdinalIgnoreCase);
        var isWindTexture = norm.Contains("breeze_wind", StringComparison.OrdinalIgnoreCase);
        var eyesRef = CompanionDiffuseTextureRefFromSiblingFileStem(norm, "breeze_eyes");
        var windRef = CompanionDiffuseTextureRefFromSiblingFileStem(norm, "breeze_wind");

        if (isEyesTexture)
        {
            var bEyes = new RigBuilder(32, 32);
            var eyesOnlyHeadPivot = EntityParityTemplate.Mul(
                EntityParityTemplate.T(0f, 4f, 0f),
                EntityParityTemplate.Mul(
                    EntityParityTemplate.T(shootHeadAdditiveTranslate.X, shootHeadAdditiveTranslate.Y, shootHeadAdditiveTranslate.Z),
                    EntityParityTemplate.Rx(shootHeadAdditivePitchRad)));
            new EntityCuboid(-5f, -5f, -4.2f, 5f, -2f, -0.2f, 4, 24).Emit(bEyes, eyesOnlyHeadPivot, 1f);
            new EntityCuboid(-4f, -8f, -4f, 4f, 0f, 4f, 0, 0).Emit(bEyes, eyesOnlyHeadPivot, 1f);
            return ApplyLivingEntityRendererPreviewBasis(bEyes.Build(texRef));
        }

        if (isWindTexture)
        {
            var windOnly = new RigBuilder(128, 128);
            AppendBreezeWindCuboids(windOnly, profile, windAnimTimeSeconds, "#skin");
            return ApplyLivingEntityRendererPreviewBasis(windOnly.Build(texRef));
        }

        var body = new RigBuilder(32, 32);
        var rodsRoot = EntityParityTemplate.T(0f, 8f, 0f);
        var rod1Pose = EntityParityTemplate.Mul(
            rodsRoot,
            EntityParityTemplate.Mul(
                EntityParityTemplate.T(2.5981f, -3f, 1.5f),
                EntityParityTemplate.Er(-2.7489f + swirl, -1.0472f, MathF.PI)));
        new EntityCuboid(-1f, 0f, -3f, 1f, 8f, -1f, 0, 17).Emit(body, rod1Pose, 1f);

        var rod2Pose = EntityParityTemplate.Mul(
            rodsRoot,
            EntityParityTemplate.Mul(
                EntityParityTemplate.T(-2.5981f, -3f, 1.5f),
                EntityParityTemplate.Er(-2.7489f - swirl, 1.0472f, MathF.PI)));
        new EntityCuboid(-1f, 0f, -3f, 1f, 8f, -1f, 0, 17).Emit(body, rod2Pose, 1f);

        var rod3Pose = EntityParityTemplate.Mul(
            rodsRoot,
            EntityParityTemplate.Mul(
                EntityParityTemplate.T(0f, -3f, -3f),
                EntityParityTemplate.Er(0.3927f, 0f, 0f)));
        new EntityCuboid(-1f, 0f, -3f, 1f, 8f, -1f, 0, 17).Emit(body, rod3Pose, 1f);

        var headPose = EntityParityTemplate.Mul(
            EntityParityTemplate.T(0f, 4f, 0f),
            EntityParityTemplate.Mul(
                EntityParityTemplate.T(shootHeadAdditiveTranslate.X, shootHeadAdditiveTranslate.Y, shootHeadAdditiveTranslate.Z),
                EntityParityTemplate.Rx(shootHeadAdditivePitchRad)));
        new EntityCuboid(-5f, -5f, -4.2f, 5f, -2f, -0.2f, 4, 24).Emit(body, headPose, 1f);
        new EntityCuboid(-4f, -8f, -4f, 4f, 0f, 4f, 0, 0).Emit(body, headPose, 1f);
        new EntityCuboid(-5f, -5f, -4.2f, 5f, -2f, -0.2f, 4, 24).Emit(body, headPose, 1f, "#eyes");
        new EntityCuboid(-4f, -8f, -4f, 4f, 0f, 4f, 0, 0).Emit(body, headPose, 1f, "#eyes");

        var bodyModel = body.Build(texRef, new Dictionary<string, string>(StringComparer.Ordinal) { ["eyes"] = eyesRef });
        var windModel = BuildBreezeWindCompositeForMainDiffuse(texRef, windRef, profile, windAnimTimeSeconds);
        return ApplyLivingEntityRendererPreviewBasis(MergeEntityPreviewMeshes(bodyModel, windModel));
    }


    private static MergedJavaBlockModel MergeEntityPreviewMeshes(MergedJavaBlockModel a, MergedJavaBlockModel b)
    {
        var elements = new List<ModelElement>(a.Elements.Count + b.Elements.Count);
        elements.AddRange(a.Elements);
        elements.AddRange(b.Elements);
        var textures = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kv in a.Textures)
        {
            textures[kv.Key] = kv.Value;
        }

        foreach (var kv in b.Textures)
        {
            textures[kv.Key] = kv.Value;
        }

        return new MergedJavaBlockModel
        {
            Elements = elements,
            Textures = textures,
            UsesLivingEntityRendererColumnYFlip = a.UsesLivingEntityRendererColumnYFlip ||
                                                   b.UsesLivingEntityRendererColumnYFlip,
        };
    }


    private static MergedJavaBlockModel BuildBreezeWindCompositeForMainDiffuse(
        string breezeDiffuseTexRef,
        string windTextureRef,
        MinecraftNativeProfile profile,
        float windAnimTimeSeconds)
    {
        var b = new RigBuilder(128, 128);
        AppendBreezeWindCuboids(b, profile, windAnimTimeSeconds, "#wind");
        return b.Build(breezeDiffuseTexRef, new Dictionary<string, string>(StringComparer.Ordinal) { ["wind"] = windTextureRef });
    }


    private static void AppendBreezeWindCuboids(RigBuilder b, MinecraftNativeProfile profile, float windAnimTimeSeconds, string windTexKey)
    {
        var windBodyPose = Matrix4x4.Identity;
        var windBottom = EntityParityTemplate.Mul(windBodyPose, EntityParityTemplate.T(0f, 24f, 0f));
        new EntityCuboid(-2.5f, -7f, -2.5f, 2.5f, 0f, 2.5f, 1, 83).Emit(b, windBottom, 1f, windTexKey);
        Matrix4x4 windMid;
        Matrix4x4 windTop;
        if (DefinitionAnimationPreviewSampling.SamplePosition(
                profile,
                "net.minecraft.client.animation.definitions.BreezeAnimation",
                "IDLE",
                "wind_mid",
                windAnimTimeSeconds,
                out var trMid) &&
            DefinitionAnimationPreviewSampling.SamplePosition(
                profile,
                "net.minecraft.client.animation.definitions.BreezeAnimation",
                "IDLE",
                "wind_top",
                windAnimTimeSeconds,
                out var trTop))
        {
            windMid = EntityParityTemplate.Mul(
                windBottom,
                EntityParityTemplate.Mul(EntityParityTemplate.T(0f, -7f, 0f), EntityParityTemplate.T(trMid.X, trMid.Y, trMid.Z)));
            new EntityCuboid(-6f, -6f, -6f, 6f, 0f, 6f, 74, 28).Emit(b, windMid, 1f, windTexKey);
            new EntityCuboid(-4f, -6f, -4f, 4f, 0f, 4f, 78, 32).Emit(b, windMid, 1f, windTexKey);
            new EntityCuboid(-2.5f, -6f, -2.5f, 2.5f, 0f, 2.5f, 49, 71).Emit(b, windMid, 1f, windTexKey);
            windTop = EntityParityTemplate.Mul(
                windMid,
                EntityParityTemplate.Mul(EntityParityTemplate.T(0f, -6f, 0f), EntityParityTemplate.T(trTop.X, trTop.Y, trTop.Z)));
        }
        else
        {
            windMid = windBottom;
            windTop = windMid;
        }

        new EntityCuboid(-9f, -8f, -9f, 9f, 0f, 9f, 0, 0).Emit(b, windTop, 1f, windTexKey);
        new EntityCuboid(-6f, -8f, -6f, 6f, 0f, 6f, 6, 6).Emit(b, windTop, 1f, windTexKey);
        new EntityCuboid(-2.5f, -8f, -2.5f, 2.5f, 0f, 2.5f, 105, 57).Emit(b, windTop, 1f, windTexKey);
    }
}
