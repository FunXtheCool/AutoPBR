using System.Numerics;

namespace AutoPBR.Core.Tests;

/// <summary>
/// Baby parity-catalog previews must resolve dedicated Baby* geometry IR hosts and emit a compact rig
/// (parented part poses), not scattered root-level adult cuboids.
/// </summary>
public sealed class BabyCatalogGeometryIrPreviewTests
{
    private static readonly MinecraftNativeProfile Profile26 =
        new("26.1.2", Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native", "26.1.2"), new Version(26, 1, 2));

    private static readonly MinecraftNativeProfile RootProfile =
        new("root", Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native"), null);

    public static TheoryData<string, string> BabyPilotTextures => new()
    {
        { "assets/minecraft/textures/entity/bear/polarbear_baby.png", "net.minecraft.client.model.animal.polarbear.BabyPolarBearModel" },
        { "assets/minecraft/textures/entity/cow/cow_temperate_baby.png", "net.minecraft.client.model.animal.cow.BabyCowModel" },
        { "assets/minecraft/textures/entity/hoglin/hoglin_baby.png", "net.minecraft.client.model.monster.hoglin.BabyHoglinModel" },
        { "assets/minecraft/textures/entity/fox/fox_baby.png", "net.minecraft.client.model.animal.fox.BabyFoxModel" },
        { "assets/minecraft/textures/entity/panda/panda_baby.png", "net.minecraft.client.model.animal.panda.BabyPandaModel" },
    };

    [Theory]
    [MemberData(nameof(BabyPilotTextures))]
    public void Resolver_and_preview_use_baby_geometry_ir_host(string texturePath, string expectedBabyJvm)
    {
        var norm = texturePath.Replace('\\', '/').TrimStart('/');
        var stem = Path.GetFileNameWithoutExtension(norm).ToLowerInvariant();
        var isBaby = CleanRoomEntityModelRuntime.LooksLikeBabyTexture(stem, norm);
        Assert.True(isBaby);

        var rule = EntityTextureParityCatalog.ResolveRule(norm, stem);
        Assert.NotNull(rule);

        Assert.True(
            GeometryIrParityJvmResolver.TryResolveLiftedRoot(
                Profile26, rule, norm, stem, isBaby: true, out var resolvedJvm, out _),
            $"no lifted shard for {texturePath}");
        Assert.Equal(expectedBabyJvm, resolvedJvm);

        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(texturePath, Profile26, 0f, 0f, out var mesh, out var provenance));
        Assert.Contains(expectedBabyJvm, provenance.Detail ?? "", StringComparison.Ordinal);
        Assert.True(mesh.Elements.Count >= 4);

        var compactness = ComputeMeshOriginCompactness(mesh);
        Assert.True(
            compactness.MaxOriginDistanceFromCentroid < 28f,
            $"{texturePath}: parts too scattered (max origin distance {compactness.MaxOriginDistanceFromCentroid:F2})");
        Assert.True(
            compactness.HasNonIdentityRelativeOffset,
            $"{texturePath}: all element origins coincide with centroid — likely flat/unparented emit");
    }

    [Fact]
    public void Polarbear_baby_dedicated_ir_uses_unit_scale_not_vanilla_uniform_half()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/bear/polarbear_baby.png",
            Profile26,
            0f,
            0f,
            out var baby,
            out _));
        Assert.True(HasElementSize(baby, 6f, 5f, 4f, epsilon: 0.15f));
        Assert.True(HasElementSize(baby, 8f, 7f, 12f, epsilon: 0.4f));
        Assert.False(HasElementSize(baby, 3f, 2.5f, 2f, epsilon: 0.2f));
    }

    [Fact]
    public void Dedicated_baby_ir_uses_unit_cuboid_scale_when_profile_is_unversioned_root()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/cow/cow_temperate_baby.png",
            RootProfile,
            0f,
            0f,
            out var baby,
            out var provenance));
        Assert.Contains("BabyCowModel", provenance.Detail ?? "", StringComparison.Ordinal);

        Assert.True(
            HasWorldElementSize(baby, 8f, 6f, 12f, epsilon: 0.25f),
            "dedicated Baby* IR must preserve the baked baby body cuboid size");
        Assert.False(
            HasWorldElementSize(baby, 4f, 3f, 6f, epsilon: 0.25f),
            "dedicated Baby* IR must not apply legacy half-scale when the profile version is unknown");
    }

    private static bool HasElementSize(MergedJavaBlockModel model, float w, float h, float d, float epsilon)
    {
        foreach (var el in model.Elements)
        {
            var ex = MathF.Abs(el.To[0] - el.From[0]);
            var ey = MathF.Abs(el.To[1] - el.From[1]);
            var ez = MathF.Abs(el.To[2] - el.From[2]);
            if (MathF.Abs(ex - w) <= epsilon && MathF.Abs(ey - h) <= epsilon && MathF.Abs(ez - d) <= epsilon)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasWorldElementSize(MergedJavaBlockModel model, float w, float h, float d, float epsilon)
    {
        foreach (var el in model.Elements)
        {
            var size = ComputeWorldAabbSize(el);
            if (MathF.Abs(size.X - w) <= epsilon &&
                MathF.Abs(size.Y - h) <= epsilon &&
                MathF.Abs(size.Z - d) <= epsilon)
            {
                return true;
            }
        }

        return false;
    }

    private static Vector3 ComputeWorldAabbSize(ModelElement el)
    {
        var min = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector3(float.NegativeInfinity, float.NegativeInfinity, float.NegativeInfinity);
        for (var xi = 0; xi < 2; xi++)
        {
            var x = xi == 0 ? el.From[0] : el.To[0];
            for (var yi = 0; yi < 2; yi++)
            {
                var y = yi == 0 ? el.From[1] : el.To[1];
                for (var zi = 0; zi < 2; zi++)
                {
                    var z = zi == 0 ? el.From[2] : el.To[2];
                    var p = Vector3.Transform(new Vector3(x, y, z), el.LocalToParent);
                    min = Vector3.Min(min, p);
                    max = Vector3.Max(max, p);
                }
            }
        }

        return max - min;
    }

    [Fact]
    public void Polarbear_baby_mesh_is_tighter_than_adult_polarbear()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/bear/polarbear.png",
            Profile26,
            0f,
            0f,
            out var adult,
            out _));
        Assert.True(runtime.TryBuildStaticMesh(
            "assets/minecraft/textures/entity/bear/polarbear_baby.png",
            Profile26,
            0f,
            0f,
            out var baby,
            out var babyProv));
        Assert.Contains("BabyPolarBearModel", babyProv.Detail ?? "", StringComparison.Ordinal);

        var adultSpan = ComputeMeshOriginCompactness(adult).BoundingSpan;
        var babySpan = ComputeMeshOriginCompactness(baby).BoundingSpan;
        Assert.True(babySpan < adultSpan * 0.92f, $"baby span {babySpan:F2} vs adult {adultSpan:F2}");
    }

    private static (float MaxOriginDistanceFromCentroid, float BoundingSpan, bool HasNonIdentityRelativeOffset)
        ComputeMeshOriginCompactness(MergedJavaBlockModel model)
    {
        if (model.Elements.Count == 0)
        {
            return (0f, 0f, false);
        }

        var origins = new List<Vector3>(model.Elements.Count);
        foreach (var el in model.Elements)
        {
            var c = new Vector3(
                (el.From[0] + el.To[0]) * 0.5f,
                (el.From[1] + el.To[1]) * 0.5f,
                (el.From[2] + el.To[2]) * 0.5f);
            origins.Add(Vector3.Transform(c, el.LocalToParent));
        }

        var centroid = Vector3.Zero;
        foreach (var o in origins)
        {
            centroid += o;
        }

        centroid /= origins.Count;

        var maxDist = 0f;
        var min = origins[0];
        var max = origins[0];
        foreach (var o in origins)
        {
            maxDist = MathF.Max(maxDist, Vector3.Distance(o, centroid));
            min = Vector3.Min(min, o);
            max = Vector3.Max(max, o);
        }

        var span = MathF.Max(max.X - min.X, MathF.Max(max.Y - min.Y, max.Z - min.Z));
        var hasOffset = false;
        foreach (var o in origins)
        {
            if (Vector3.Distance(o, centroid) > 0.35f)
            {
                hasOffset = true;
                break;
            }
        }

        return (maxDist, span, hasOffset);
    }
}
