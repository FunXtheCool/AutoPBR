using System.Numerics;
using System.Text.Json;

namespace AutoPBR.Core.Preview;

/// <summary>
/// Options for geometry IR body-layer emission on <see cref="CleanRoomEntityModelRuntime"/>.
/// </summary>
internal readonly struct GeometryIrMeshEmitOptions
{
    public Matrix4x4 RootTransform { get; init; }

    public float DefaultPartScale { get; init; }

    public int AtlasWidth { get; init; }

    public int AtlasHeight { get; init; }

    /// <summary>Preview-only thickness for zero-extent cuboid axes (vanilla sheets use true zero depth).</summary>
    public float PreviewDegenerateAxisThickness { get; init; }

    public GeometryIrEmitFidelity Fidelity { get; init; }

    public Func<string, float>? ResolvePartScale { get; init; }

    /// <summary>Adjust composed part pose (e.g. tail sway on top of IR rest pose).</summary>
    public Func<string, Matrix4x4, Matrix4x4>? TryGetPartPoseOverride { get; init; }

    /// <summary>Official JVM name for per-model emit policy (inflate UV footprint).</summary>
    public string? OfficialJvmName { get; init; }

    /// <summary>Normalized texture archive path for catalog emit policy.</summary>
    public string? NormalizedAssetPath { get; init; }

    /// <summary>
    /// When true and a compile-time cuboid table exists, emit corners from codegen and poses from IR
    /// (<see cref="GeometryIrCodegenTables"/>).
    /// </summary>
    public bool PreferCodegenCuboids { get; init; }

    /// <summary>When set, only parts whose <c>id</c> is in this set are emitted (equipment armor subsets).</summary>
    public IReadOnlySet<string>? IncludePartIds { get; init; }

    /// <summary>When set, only parts for which this returns true emit cuboids (multi-layer path filtering).</summary>
    public Func<string, bool>? ShouldEmitPartCuboids { get; init; }

    /// <summary>When set, cuboids for which this returns false are skipped (e.g. eyes layer omitting texCrop sheets).</summary>
    public Func<JsonElement, bool>? ShouldEmitIrCuboid { get; init; }

    /// <summary>
    /// When cuboid IR omits per-cuboid atlas tags, resolve atlas dimensions per part (e.g. Breeze wind tiers on 128²).
    /// </summary>
    public Func<string, (int Width, int Height)>? ResolvePartAtlasDimensions { get; init; }

    /// <summary>Override lifted cuboid <c>textureKey</c> by part id (e.g. main Breeze diffuse maps wind tiers to <c>#wind</c>).</summary>
    public Func<string, string?>? ResolvePartTextureKey { get; init; }

    /// <summary>
    /// Compose child poses from Java <c>PartPose.offsetAndRotation</c> in texel space. Row-matrix storage uses
    /// <c>Er × T</c> (see <see cref="EntityParityTemplate.PartPose"/>), composed as <c>local * parent</c>.
    /// </summary>
    public bool UseColumnTranslationTimesRotationPartPose { get; init; }

    /// <summary>
    /// When true with <see cref="GeometryIrEmitFidelity.Parity"/>, apply IR <c>inflate</c> for interactive preview shells
    /// (parity golden tests keep inflate off via <see cref="ForParity"/> without this flag).
    /// </summary>
    public bool PreviewApplyCubeDeformationInflate { get; init; }

    internal bool ResolveUseColumnTranslationTimesRotationPartPose() =>
        UseColumnTranslationTimesRotationPartPose;

    public static GeometryIrMeshEmitOptions Default => ForViewport();

    public static GeometryIrMeshEmitOptions ForViewport() => new()
    {
        RootTransform = Matrix4x4.Identity,
        DefaultPartScale = 1f,
        AtlasWidth = 64,
        AtlasHeight = 64,
        PreviewDegenerateAxisThickness = 0.08f,
        Fidelity = GeometryIrEmitFidelity.Viewport
    };

    public static GeometryIrMeshEmitOptions ForParity(int atlasWidth = 64, int atlasHeight = 64) => new()
    {
        RootTransform = Matrix4x4.Identity,
        DefaultPartScale = 1f,
        AtlasWidth = atlasWidth,
        AtlasHeight = atlasHeight,
        PreviewDegenerateAxisThickness = 0f,
        Fidelity = GeometryIrEmitFidelity.Parity
    };

    public GeometryIrMeshEmitOptions WithOfficialJvmPoseComposeDefaults(string? officialJvmName)
    {
        if (string.IsNullOrWhiteSpace(officialJvmName))
        {
            return this;
        }

        return this with
        {
            OfficialJvmName = officialJvmName,
            UseColumnTranslationTimesRotationPartPose =
                GeometryIrEmitPolicy.UsesColumnPartPoseOffsetAndRotation(officialJvmName),
        };
    }
}
