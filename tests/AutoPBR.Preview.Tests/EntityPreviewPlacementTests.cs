using AutoPBR.Core.Models;
using AutoPBR.Preview;

namespace AutoPBR.Preview.Tests;

public sealed class EntityPreviewPlacementTests
{
  private static readonly MinecraftNativeProfile Profile26 =
      new("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));

  public static TheoryData<string> BabyCentroidCases => new()
  {
    "assets/minecraft/textures/entity/armadillo/armadillo_baby.png",
    "assets/minecraft/textures/entity/axolotl/axolotl_blue_baby.png",
    "assets/minecraft/textures/entity/cow/cow_temperate_baby.png",
    "assets/minecraft/textures/entity/horse/donkey_baby.png",
    "assets/minecraft/textures/entity/sheep/sheep_baby.png",
    "assets/minecraft/textures/entity/zombie/drowned_baby.png",
  };

  [Fact]
  public void Cow_temperate_bind_pose_foot_contact_near_preview_floor_after_placement()
  {
    const string path = "assets/minecraft/textures/entity/cow/cow_temperate.png";
    var runtime = EntityModelRuntimeFactory.Create();
    Assert.True(runtime.TryBuildStaticMesh(
        path,
        Profile26,
        0f,
        0f,
        out var merged,
        applyGeometryIrSetupAnimMotion: false));

    var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(merged, "minecraft");
    var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
    var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < ordered.Count; i++)
    {
      pathToIdx[ordered[i]] = i;
      texSizes[ordered[i]] = (64, 64);
    }

    Assert.True(MinecraftModelBaker.TryBake(merged, "minecraft", pathToIdx, texSizes, out var verts, out _, out _));

    var rebake = new EntityEmulatedPreviewRebakeContext
    {
      PackZipPath = "pack.zip",
      AssetArchivePath = path,
      NativeRootDirectory = Profile26.RootDirectory,
      NativeProfileName = Profile26.Name,
      ModelDefaultNamespace = "minecraft",
      IdlePhase01 = 0f,
      OrderedTextureZipPaths = ordered.ToArray()
    };
    EntityPreviewPlacement.TryPopulateRebakeElementPartIds(rebake, Profile26, merged.Elements.Count);
    var placement = EntityPreviewPlacement.ApplyToPreviewVertices(
        verts,
        MinecraftModelBaker.FloatsPerVertex,
        rebake.ElementPartIds,
        EntityPreviewPlacement.DefaultFloorY);

    Assert.True(placement.GroundLiftY >= 0f);
    var floor = EntityPreviewPlacement.DefaultFloorY + EntityPreviewGrounding.DefaultClearance;
    var minY = float.PositiveInfinity;
    for (var i = 1; i < verts.Length; i += MinecraftModelBaker.FloatsPerVertex)
    {
      minY = MathF.Min(minY, verts[i]);
    }

    Assert.InRange(minY, floor - 0.08f, floor + 0.08f);
  }

  [Theory]
  [MemberData(nameof(BabyCentroidCases))]
  public void Baby_rebake_records_meaningful_part_centroid_diagnostics(string texturePath)
  {
    var runtime = EntityModelRuntimeFactory.Create();
    Assert.True(runtime.TryBuildStaticMesh(
        texturePath,
        Profile26,
        idlePhase01: 0f,
        animationTimeSeconds: 0f,
        out var merged,
        applyGeometryIrSetupAnimMotion: false));

    var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(merged, "minecraft");
    Assert.NotEmpty(ordered);

    var rebake = new EntityEmulatedPreviewRebakeContext
    {
      PackZipPath = "pack.zip",
      AssetArchivePath = texturePath,
      NativeRootDirectory = AppContext.BaseDirectory,
      NativeProfileName = Profile26.Name,
      NativeParsedVersion = Profile26.ParsedVersion?.ToString(),
      ModelDefaultNamespace = "minecraft",
      IdlePhase01 = 0f,
      OrderedTextureZipPaths = ordered.ToArray()
    };

    var materials = ordered.Select(_ => CreateMaps(64, 64)).ToArray();
    Assert.True(EntityEmulatedPreviewRebaker.TryRebakeMesh(
        rebake,
        materials,
        animationTimeSeconds: 0f,
        out _,
        out _,
        out _,
        applyGeometryIrSetupAnimMotion: false));

    AssertDiagnosticCentroid(texturePath, "body", rebake.LastBodyCentroidY);
    AssertDiagnosticCentroid(texturePath, "head", rebake.LastHeadCentroidY);
    AssertDiagnosticCentroid(texturePath, "leg", rebake.LastLegCentroidY);
  }

  private static PreviewTextureMaps CreateMaps(int width, int height) => new()
  {
    Width = width,
    Height = height,
    DiffuseRgba = new byte[width * height * 4],
    NormalRgba = new byte[width * height * 4],
    SpecularRgba = new byte[width * height * 4],
    HeightRgba = new byte[width * height * 4],
  };

  private static void AssertDiagnosticCentroid(string texturePath, string partLabel, float y)
  {
    if (MathF.Abs(y) <= 1e-5f)
    {
      return;
    }

    Assert.True(
        float.IsFinite(y) && y >= -32f && y <= 4f,
        $"{texturePath} {partLabel} centroid diagnostic should stay in entity model range, got y={y:F3}");
  }
}
