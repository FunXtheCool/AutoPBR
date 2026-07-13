
namespace AutoPBR.Core.Tests;

/// <summary>
/// Post–26.1 humanoids reuse the adult mesh and apply uniform <c>LivingEntity.getAgeScale()</c>
/// (<c>DEFAULT_BABY_SCALE = 0.5F</c>) at render time. The 26.1.2 entity texture inventory has no
/// separate <c>*_baby</c> diffuse paths for skeleton / player / pillager, so mesh bake stays adult-sized.
/// </summary>
public sealed class PostBabyUniformHumanoidMeshParityTests
{
    private static readonly MinecraftNativeProfile Profile2612 = new("26.1.2", "unused", new Version(26, 1, 2));

    private static bool HasElementSize(MergedJavaBlockModel model, float x, float y, float z, float epsilon = 0.3f)
    {
        foreach (var e in model.Elements)
        {
            var dx = MathF.Abs(e.To[0] - e.From[0]);
            var dy = MathF.Abs(e.To[1] - e.From[1]);
            var dz = MathF.Abs(e.To[2] - e.From[2]);
            if (MathF.Abs(dx - x) <= epsilon &&
                MathF.Abs(dy - y) <= epsilon &&
                MathF.Abs(dz - z) <= epsilon)
            {
                return true;
            }
        }

        return false;
    }

    [Fact]
    public void SkeletonFamily_AdultOn2612_KeepsTorsoBox_AndBabyPathIsUncatalogued()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        const string adultPath = "assets/minecraft/textures/entity/skeleton/skeleton.png";
        const string babyPath = "assets/minecraft/textures/entity/skeleton/skeleton_baby.png";

        Assert.True(EntityTextureParityCatalog.IsCatalogued(adultPath));
        Assert.False(EntityTextureParityCatalog.IsCatalogued(babyPath));

        Assert.True(runtime.TryBuildStaticMesh(
            adultPath,
            Profile2612,
            idlePhase01: 0f,
            animationTimeSeconds: 0f,
            out var adult));
        Assert.True(HasElementSize(adult, 8f, 12f, 4f, epsilon: 0.15f));

        Assert.True(runtime.TryBuildStaticMesh(
            babyPath,
            Profile2612,
            idlePhase01: 0f,
            animationTimeSeconds: 0f,
            out _,
            out var babyProv));
        Assert.Equal(PreviewMeshDriverKind.ErrorPlaceholder, babyProv.Kind);
    }

    [Fact]
    public void PlayerWide_AdultOn2612_KeepsTorsoBox_AndBabyPathIsUncatalogued()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        const string adultPath = "assets/minecraft/textures/entity/player/wide/steve.png";
        const string babyPath = "assets/minecraft/textures/entity/player/wide/steve_baby.png";

        Assert.True(EntityTextureParityCatalog.IsCatalogued(adultPath));
        Assert.False(EntityTextureParityCatalog.IsCatalogued(babyPath));

        Assert.True(runtime.TryBuildStaticMesh(
            adultPath,
            Profile2612,
            idlePhase01: 0f,
            animationTimeSeconds: 0f,
            out var adult));
        Assert.True(HasElementSize(adult, 8f, 12f, 4f, epsilon: 0.15f));

        Assert.True(runtime.TryBuildStaticMesh(
            babyPath,
            Profile2612,
            idlePhase01: 0f,
            animationTimeSeconds: 0f,
            out _,
            out var babyProv));
        Assert.Equal(PreviewMeshDriverKind.ErrorPlaceholder, babyProv.Kind);
    }

    [Fact]
    public void Pillager_AdultOn2612_KeepsMainRobeBox_AndBabyPathIsUncatalogued()
    {
        var runtime = EntityModelRuntimeFactory.Create();
        const string adultPath = "assets/minecraft/textures/entity/illager/pillager.png";
        const string babyPath = "assets/minecraft/textures/entity/illager/pillager_baby.png";

        Assert.True(EntityTextureParityCatalog.IsCatalogued(adultPath));
        Assert.False(EntityTextureParityCatalog.IsCatalogued(babyPath));

        Assert.True(runtime.TryBuildStaticMesh(
            adultPath,
            Profile2612,
            idlePhase01: 0f,
            animationTimeSeconds: 0f,
            out var adult));
        Assert.True(HasElementSize(adult, 8f, 12f, 6f, epsilon: 0.15f));

        Assert.True(runtime.TryBuildStaticMesh(
            babyPath,
            Profile2612,
            idlePhase01: 0f,
            animationTimeSeconds: 0f,
            out _,
            out var babyProv));
        Assert.Equal(PreviewMeshDriverKind.ErrorPlaceholder, babyProv.Kind);
    }
}
