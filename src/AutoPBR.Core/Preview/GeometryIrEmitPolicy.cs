using System.Numerics;
using System.Text.Json;



namespace AutoPBR.Core.Preview;



/// <summary>

/// Per-model geometry emit rules (inflate vs UV footprint) from packaged JSON policy.

/// </summary>

public static class GeometryIrEmitPolicy

{

    public enum InflateUvFootprint

    {

        PreInflateIntegerExtents,

        PostInflateMeshExtents

    }



    public static InflateUvFootprint GetInflateUvFootprint(string? officialJvmName)

    {

        if (string.IsNullOrWhiteSpace(officialJvmName))

        {

            return InflateUvFootprint.PreInflateIntegerExtents;

        }



        try

        {

            var path = Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native",

                "minecraft_26.1.2_geometry_emit_policy.json");

            if (!File.Exists(path))

            {

                return InflateUvFootprint.PreInflateIntegerExtents;

            }



            using var doc = JsonDocument.Parse(File.ReadAllText(path));

            if (doc.RootElement.TryGetProperty("by_official_jvm", out var byJvm) &&

                byJvm.TryGetProperty(officialJvmName, out var rule) &&

                rule.TryGetProperty("inflate_uv_footprint", out var footprint))

            {

                var s = footprint.GetString();

                if (string.Equals(s, "post_inflate_mesh_extents", StringComparison.Ordinal))

                {

                    return InflateUvFootprint.PostInflateMeshExtents;

                }

            }

        }

        catch

        {

            // fall through

        }



        return InflateUvFootprint.PreInflateIntegerExtents;

    }



    /// <summary>

    /// <c>GhastModel.animateTentacles</c> (javap 26.1.2):

    /// <c>tentacle.xRot = 0.4f + 0.2f * sin(ageInTicks * 0.3f + index)</c>.

    /// </summary>

    internal static float ComputeGhastAnimateTentaclesXRot(int tentacleIndex, float ageInTicks) =>

        0.4f + 0.2f * MathF.Sin(ageInTicks * 0.3f + tentacleIndex);



    internal static bool IsGhastFamilyJvm(string? officialJvmName)
    {
        if (string.IsNullOrWhiteSpace(officialJvmName))
        {
            return false;
        }

        if (officialJvmName.Contains(".ghast.", StringComparison.OrdinalIgnoreCase))
        {
            return !officialJvmName.Contains("Harness", StringComparison.OrdinalIgnoreCase);
        }

        // Pre-restructure mesh hosts (e.g. net.minecraft.client.model.GhastModel) omit the ghast package segment.
        return string.Equals(officialJvmName, "net.minecraft.client.model.GhastModel", StringComparison.Ordinal) ||
               string.Equals(officialJvmName, "net.minecraft.client.model.HappyGhastModel", StringComparison.Ordinal);
    }



    internal static bool TryParseGhastFamilyTentacleIndex(string partId, out int tentacleIndex)

    {

        tentacleIndex = -1;

        if (!partId.StartsWith("tentacle", StringComparison.OrdinalIgnoreCase))

        {

            return false;

        }



        var suffix = partId.AsSpan("tentacle".Length);

        return suffix.Length > 0 && int.TryParse(suffix, out tentacleIndex) && tentacleIndex >= 0;

    }



    /// <summary>
    /// Javap <c>createBodyLayer</c> lifts tentacles as <c>addBox(-1,0,-1,2,h,2)</c> (+Y in part space).
    /// Ghast-family preview skips LER mirror (root already carries <c>MeshTransformer.scaling(4.5f)</c>), so +Y grows
    /// into the body shell; reorient to −Y hang-down at the ModelPart attachment (y=0).
    /// </summary>
    internal static bool TryReorientGhastFamilyTentacleCuboidYForModelSpace(
        string? officialJvmName,
        string partId,
        ref float y0,
        ref float y1)
    {
        if (!IsGhastFamilyJvm(officialJvmName) ||
            !TryParseGhastFamilyTentacleIndex(partId, out _))
        {
            return false;
        }

        if (y0 >= -1e-5f && y1 > y0)
        {
            (y0, y1) = (-y1, -y0);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Ghast skin atlases use a single <c>texOffs(0,0)</c> unfold: body <c>16³</c>, tentacles <c>2×h×2</c>.
    /// </summary>

    internal static bool TryApplyGhastFamilyCuboidUvFootprint(

        string? officialJvmName,

        string partId,

        float y0,

        float y1,

        ref int uvSizeW,

        ref int uvSizeH,

        ref int uvSizeD)

    {

        if (!IsGhastFamilyJvm(officialJvmName))

        {

            return false;

        }



        if (string.Equals(partId, "body", StringComparison.OrdinalIgnoreCase))

        {

            uvSizeW = 16;

            uvSizeH = 16;

            uvSizeD = 16;

            return true;

        }



        if (!partId.StartsWith("tentacle", StringComparison.OrdinalIgnoreCase))

        {

            return false;

        }



        uvSizeW = 2;

        uvSizeD = 2;

        uvSizeH = Math.Max(1, (int)MathF.Round(MathF.Abs(y1 - y0)));

        return true;

    }



    /// <summary>

    /// Lifted axolotl gill sheets use <c>z=0</c> with <c>faceMask: north/south</c>; hand <c>BuildAxolotl</c> uses

    /// <c>z=-1..1</c> so unfolded UV depth matches <c>uvSpan</c>. Expand before preview thicken.

    /// </summary>

    internal static bool TryExpandAxolotlGillCuboidZExtents(

        string? officialJvmName,

        string partId,

        ref float z0,

        ref float z1)

    {

        if (string.IsNullOrWhiteSpace(officialJvmName) ||

            !officialJvmName.Contains(".animal.axolotl.", StringComparison.Ordinal) ||

            !partId.EndsWith("_gills", StringComparison.Ordinal))

        {

            return false;

        }



        if (MathF.Abs(z1 - z0) >= 0.05f)

        {

            return false;

        }



        z0 = -1f;

        z1 = 1f;

        return true;

    }



    /// <summary>
    /// Bind pose must stay on ModelPart block-stack even when
    /// <see cref="EntityPreviewDebugSettings.UseLegacyTranslationTimesRotationPartPose"/> is enabled for A/B elsewhere.
    /// </summary>
    internal static bool IgnoresLegacyPartPoseDebugSwitch(string? officialJvmName) =>
        string.Equals(officialJvmName, "net.minecraft.client.model.animal.dolphin.DolphinModel", StringComparison.Ordinal) ||
        string.Equals(officialJvmName, "net.minecraft.client.model.animal.dolphin.BabyDolphinModel", StringComparison.Ordinal);

    /// <summary>
    /// Block-entity preview composites that match CleanRoom <c>Mul(T, Er)</c> row compose instead of ModelPart block-stack.
    /// </summary>
    internal static bool UsesTranslationTimesRotationPartPose(string? officialJvmName, string? partId = null)
    {
        if (string.IsNullOrWhiteSpace(officialJvmName))
        {
            return false;
        }

        if (string.Equals(
                officialJvmName,
                "net.minecraft.client.model.DecoratedPotModel.previewComposite",
                StringComparison.Ordinal))
        {
            return true;
        }

        if (string.Equals(
                officialJvmName,
                "net.minecraft.client.model.object.boat.BoatModel.createChestBoatModel",
                StringComparison.Ordinal))
        {
            return partId is null or "chest_bottom" or "chest_lid" or "chest_lock";
        }

        if (officialJvmName.Contains(".object.boat.RaftModel", StringComparison.Ordinal))
        {
            return partId is "bottom" or "chest_bottom" or "chest_lid" or "chest_lock";
        }

        return false;
    }

    /// <summary>
    /// <c>DolphinModel.setupAnim</c> (javap 26.1.2): body <c>xRot/yRot</c> from render state; swim wobble only when
    /// <c>isMoving</c> (lifted setup-anim IR omits the branch guard).
    /// </summary>
    internal static void ApplyDolphinSetupAnimPose(
        IReadOnlyDictionary<string, float> renderState,
        VanillaSetupAnimRuntime.PoseResult pose)
    {
        const float degToRad = 0.017453292f;
        var ageInTicks = renderState.GetValueOrDefault("ageInTicks", 0f);
        var isMoving = renderState.GetValueOrDefault("isMoving", 1f) > 0.5f;

        pose.Parts["body"] = new VanillaSetupAnimRuntime.PartPose
        {
            XRot = renderState.GetValueOrDefault("xRot", 0f) * degToRad,
            YRot = renderState.GetValueOrDefault("yRot", 0f) * degToRad,
            Assigned = VanillaSetupAnimRuntime.PartPoseChannel.XRot |
                       VanillaSetupAnimRuntime.PartPoseChannel.YRot,
        };

        if (!isMoving)
        {
            return;
        }

        var body = pose.Parts["body"];
        body.XRot += -0.05f + 0.05f * MathF.Cos(ageInTicks * 0.3f);
        pose.Parts["tail"] = new VanillaSetupAnimRuntime.PartPose
        {
            XRot = -0.1f * MathF.Cos(ageInTicks * 0.3f),
            Assigned = VanillaSetupAnimRuntime.PartPoseChannel.XRot,
        };
        pose.Parts["tailFin"] = new VanillaSetupAnimRuntime.PartPose
        {
            XRot = -0.2f * MathF.Cos(ageInTicks * 0.3f),
            Assigned = VanillaSetupAnimRuntime.PartPoseChannel.XRot,
        };
    }

}


