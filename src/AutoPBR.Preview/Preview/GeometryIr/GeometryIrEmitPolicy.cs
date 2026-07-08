using System.Numerics;
using System.Text.Json;



namespace AutoPBR.Preview.GeometryIr;



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
        UsesModelPartTranslateAndRotateBindPoseJvm(officialJvmName);

    /// <summary>
    /// Bind pose must use vanilla <c>ModelPart.translateAndRotate</c> block-stack compose, not column <c>Er × T</c>,
    /// when rotated child parts attach to a parent chain (bee wings, dolphin fins, …).
    /// </summary>
    internal static bool UsesModelPartTranslateAndRotateBindPoseJvm(string? officialJvmName)
    {
        if (string.IsNullOrWhiteSpace(officialJvmName))
        {
            return false;
        }

        return officialJvmName.Contains(".animal.bee.", StringComparison.OrdinalIgnoreCase) ||
               officialJvmName.Contains(".animal.dolphin.", StringComparison.OrdinalIgnoreCase) ||
               officialJvmName.Contains(".animal.equine.", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Object boat/cart bind poses use <see cref="EntityParityTemplate.ModelPartRenderLocalBlock"/> on emit;
    /// mob <c>PartPose.offsetAndRotation</c> parts still use column <c>Er × T</c> when rotation is non-zero.
    /// </summary>
    internal static bool UsesObjectEntityModelPartPoseCompose(string? officialJvmName)
    {
        if (string.IsNullOrWhiteSpace(officialJvmName))
        {
            return false;
        }

        return officialJvmName.Contains(".object.boat.", StringComparison.Ordinal) ||
               officialJvmName.Contains(".object.cart.", StringComparison.Ordinal);
    }

    /// <summary>Legacy JVM-level column flag; per-pose rotation now drives column compose except object boat/cart.</summary>
    internal static bool UsesColumnPartPoseOffsetAndRotation(string? officialJvmName)
    {
        _ = officialJvmName;
        return false;
    }

    /// <summary>Legacy T × Er compose for ModelPart.translateAndRotate hosts (A/B via debug switch).</summary>
    internal static bool UsesTranslationTimesRotationPartPose(string? officialJvmName, string? partId = null)
    {
        _ = officialJvmName;
        _ = partId;
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


