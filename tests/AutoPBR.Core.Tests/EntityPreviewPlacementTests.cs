using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;
using AutoPBR.Tests.Shared;

namespace AutoPBR.Core.Tests;

public sealed class EntityPreviewPlacementTests
{
  private static readonly MinecraftNativeProfile Profile26 =
      new("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));

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
}
