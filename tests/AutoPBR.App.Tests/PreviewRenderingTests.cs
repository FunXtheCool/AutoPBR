using System.Numerics;

using AutoPBR.App.Rendering;
using AutoPBR.App.Rendering.Abstractions;
using AutoPBR.App.Rendering.OpenGL;
using AutoPBR.App.Rendering.Scene;
using AutoPBR.Core.Models;

namespace AutoPBR.App.Tests;

public class PreviewRenderingTests
{
    [Fact]
    public void BlockModelPreviewSceneUsesBlockModelKind()
    {
        var mesh = PreviewMeshFactory.CreateUnitCube("subject");
        var scene = BlockModelPreviewSceneFactory.Create(new PreviewRenderSettings(), mesh);
        Assert.Equal(PreviewSceneKind.BlockModel, scene.SceneKind);
        Assert.Same(mesh, scene.Meshes[0]);
    }

    [Fact]
    public void CubeMeshFactoryCreatesSixFacesWithExpectedIndices()
    {
        var mesh = PreviewMeshFactory.CreateUnitCube();
        Assert.Equal(24, mesh.VertexCount);
        Assert.Equal(24 * PreviewMesh.FloatsPerVertex, mesh.InterleavedVertices.Length);
        Assert.Equal(36, mesh.Indices.Length);
    }

    [Fact]
    public void ItemPlaneMeshFactoryCreatesQuadIndices()
    {
        var mesh = PreviewMeshFactory.CreateItemPlane();
        Assert.Equal(4, mesh.VertexCount);
        Assert.Equal(6, mesh.Indices.Length);
    }

    [Fact]
    public void PreviewMaterialMapperCopiesMapsFromCore()
    {
        var diffuse = new byte[4];
        diffuse[0] = 10;
        diffuse[1] = 20;
        diffuse[2] = 30;
        diffuse[3] = 240;
        var maps = new PreviewTextureMaps
        {
            Width = 1,
            Height = 1,
            DiffuseRgba = diffuse,
            NormalRgba = [1, 2, 3, 4],
            SpecularRgba = [5, 6, 7, 8],
            HeightRgba = [9, 10, 11, 12],
            IsPlantForNoHeight = true
        };
        var mat = PreviewMaterialMapper.FromCoreMaps(maps);
        Assert.Equal(1, mat.Width);
        Assert.Equal(1, mat.Height);
        Assert.True(mat.IsPlantForNoHeight);
        Assert.Equal(10, mat.AlbedoRgba.Span[0]);
        Assert.Equal(4, mat.NormalRgba!.Value.Span[3]);
        Assert.Equal(8, mat.SpecularRgba!.Value.Span[3]);
        Assert.Equal(12, mat.HeightRgba!.Value.Span[3]);
    }

    [Fact]
    public void RenderSettingsDefaultsAreUsable()
    {
        var s = new PreviewRenderSettings();
        Assert.Equal(1f, s.NormalStrength);
        Assert.True(s.EnableParallax);
        Assert.True(s.NearestTextureFilter);
        Assert.True(s.ShowBackgroundGrid);
        Assert.True(s.ShowCornerAxes);
        Assert.True(s.DrawPreviewSubject);
        Assert.Equal(PreviewEntityAlphaMode.Cutout, s.EntityAlphaMode);
        Assert.True(s.EnableEntityLabPbrShading);
        Assert.False(s.EnableEntityParallax);
    }

    [Fact]
    public void EmptySubjectMeshHasNoIndices()
    {
        var mesh = PreviewMeshFactory.CreateEmptySubjectPlaceholder();
        Assert.Equal(0, mesh.VertexCount);
        Assert.Empty(mesh.Indices);
        Assert.Empty(mesh.InterleavedVertices);
    }

    [Fact]
    public void PreviewGroundPlaneTilingUvSpansOneTilePerWorldUnit()
    {
        const float h = 14f;
        var mesh = PreviewMeshFactory.CreatePreviewGroundPlane(halfExtent: h, worldY: -1f, metersPerTile: 1f);
        Assert.Equal(4, mesh.VertexCount);
        Assert.Equal(6, mesh.Indices.Length);
        var v = mesh.InterleavedVertices;
        const int s = PreviewMesh.FloatsPerVertex;
        Assert.Equal(0f, v[6]);
        Assert.Equal(0f, v[7]);
        Assert.Equal(2f * h, v[s + 6]);
        Assert.Equal(0f, v[s + 7]);
        Assert.Equal(2f * h, v[2 * s + 6]);
        Assert.Equal(2f * h, v[2 * s + 7]);
        Assert.Equal(0f, v[3 * s + 6]);
        Assert.Equal(2f * h, v[3 * s + 7]);
    }

    [Fact]
    public void GridLinesFactoryVertexBuffersAreSevenFloatsPerVertex()
    {
        var grid = PreviewGridLinesFactory.BuildGrid(1f, 0.5f, -0.5f, 1, 1, 1, 1);
        Assert.Equal(0, grid.Length % PreviewGridLinesFactory.FloatsPerVertex);
        var axes = PreviewGridLinesFactory.BuildAxes(1f, 1, 0, 0, 0, 1, 0, 0, 0, 1);
        Assert.Equal(0, axes.Length % PreviewGridLinesFactory.FloatsPerVertex);
        Assert.Equal(6 * PreviewGridLinesFactory.FloatsPerVertex, axes.Length);
    }

    [Fact]
    public void SubjectPlacementUnitCubeDoesNotNeedLiftForCurrentStageFloor()
    {
        // Block/item preview uses a centered ±0.5 cube; the grid is slightly lower (see PreviewStageConstants).
        // This documents the real staging relationship — not every preview mesh dips through the floor.
        var mesh = PreviewMeshFactory.CreateUnitCube();
        var lift = PreviewSubjectPlacement.ComputeLiftToAvoidGroundClip(
            mesh.InterleavedVertices,
            clearance: 0.002f);
        Assert.Equal(0f, lift);
    }

    [Fact]
    public void SubjectPlacementComputesPositiveLiftWhenLowestVertexIsBelowFloor()
    {
        // Unit test for PreviewSubjectPlacement math only: any baked mesh (entity rigs included) whose minimum Y
        // falls below floor+clearance gets a compensating lift. Horse/pig/chicken parity belongs in Core mesh tests;
        // here we only need one vertex row with Y through the floor plane.
        var floor = PreviewStageConstants.GridWorldY;
        var verts = new float[PreviewMesh.FloatsPerVertex];
        verts[1] = floor - 0.25f;

        var lift = PreviewSubjectPlacement.ComputeLiftToAvoidGroundClip(verts, floor, clearance: 0.002f);
        Assert.True(lift > 0f);
        Assert.Equal(floor + 0.002f - verts[1], lift, precision: 5);
    }

    [Fact]
    public void SubjectPlacementLiftedMeshMinYStaysAtOrAboveGridClearance()
    {
        var mesh = PreviewMeshFactory.CreateUnitCube();
        var lift = PreviewSubjectPlacement.ComputeLiftToAvoidGroundClip(
            mesh.InterleavedVertices,
            clearance: 0.002f);
        var lifted = PreviewSubjectPlacement.ApplyLift(mesh.InterleavedVertices, lift);
        var minY = float.PositiveInfinity;
        for (var i = 1; i < lifted.Length; i += PreviewMesh.FloatsPerVertex)
        {
            minY = MathF.Min(minY, lifted[i]);
        }

        Assert.True(minY >= PreviewStageConstants.GridWorldY + 0.0019f);
    }

    [Fact]
    public void SubjectPlacementLiftPreservesEmulatedEntityRebakeAndGpuSkinningFlags()
    {
        var mesh = PreviewMeshFactory.CreateUnitCube();
        var verts = (float[])mesh.InterleavedVertices.Clone();
        const int s = PreviewMesh.FloatsPerVertex;
        for (var i = 1; i < verts.Length; i += s)
        {
            verts[i] -= 2f;
        }

        var mats = new PreviewTextureMaps[]
        {
            new()
            {
                Width = 1,
                Height = 1,
                DiffuseRgba = new byte[4]
            }
        };

        var rebake = new EntityEmulatedPreviewRebakeContext
        {
            PackZipPath = "pack.zip",
            AssetArchivePath = "assets/minecraft/textures/entity/horse/horse_white.png",
            NativeRootDirectory = Path.GetTempPath(),
            NativeProfileName = "26.1.2",
            NativeParsedVersion = "26.1.2",
            ModelDefaultNamespace = "minecraft",
            IdlePhase01 = 0.2f,
            OrderedTextureZipPaths = ["assets/minecraft/textures/entity/horse/horse_white.png"]
        };

        var subject = new PreviewModelSubject
        {
            InterleavedVertices = verts,
            Indices = mesh.Indices,
            DrawBatches = [new PreviewDrawBatch(0, mesh.Indices.Length, 0)],
            Materials = mats,
            AnimationPreset = "entity_emulated",
            EmulatedRebake = rebake,
            GpuEntityBoneSkinning = true,
            VertexStrideFloats = 12,
            EntityGpuMeshSpaceLiftY = 0.01f
        };

        var lifted = PreviewSubjectPlacement.LiftSubjectIfClipping(subject);
        Assert.NotSame(subject, lifted);
        Assert.NotNull(lifted.EmulatedRebake);
        Assert.Same(rebake, lifted.EmulatedRebake);
        Assert.True(lifted.GpuEntityBoneSkinning);
        Assert.Equal(12, lifted.VertexStrideFloats);
        Assert.Equal(0.01f, lifted.EntityGpuMeshSpaceLiftY);
    }

    [Fact]
    public void RenderSettingsGenesisDefaultsAreSensible()
    {
        var s = new PreviewRenderSettings();
        Assert.True(s.EnableSss);
        Assert.True(s.EnableParallaxShadow);
        Assert.True(s.EnableIbl);
        Assert.True(s.EnableAtmosphericSky);
        Assert.Equal(2.6f, s.AtmosphereTurbidity);
        Assert.Equal(16f, s.AtmosphereSunIntensity);
        Assert.Equal(1.35f, s.AtmosphereHorizonFalloff);
        Assert.Equal(1f, s.SssStrength);
        Assert.Equal(0.6f, s.IblStrength);
        Assert.Equal(1f, s.EmissionStrength);
    }

    [Fact]
    public void RenderSettingsShadowDefaultsAreSensible()
    {
        var s = new PreviewRenderSettings();
        Assert.True(s.EnableShadows);
        Assert.Equal(1024, s.ShadowMapResolution);
        Assert.Equal(0.0008f, s.ShadowMinBias);
        Assert.Equal(0.005f, s.ShadowMaxBias);
        // Phase 3 stub: persisted boolean only, defaults to false in Phase 2.
        Assert.False(s.EnableShadowCascades);
    }

    [Fact]
    public void LightDirectionFromYawPitchProducesUnitVector()
    {
        // Sample a grid of yaw / pitch values; PreviewLightMath should always return unit-length.
        for (var yaw = -180.0; yaw <= 180.0; yaw += 45.0)
        {
            for (var pitch = -89.0; pitch <= 89.0; pitch += 30.0)
            {
                var dir = PreviewLightMath.LightDirectionFromYawPitch(yaw, pitch);
                Assert.InRange(dir.Length(), 0.999f, 1.001f);
            }
        }
    }

    [Fact]
    public void LightDirectionFromYawPitchRoundtripsThroughInverse()
    {
        // Avoid the polar singularity at |pitch| ~ 90 where atan2(x,z) is undefined for pure Y axes.
        var yawPitchPairs = new (double Yaw, double Pitch)[]
        {
            (-35.0, -55.0),
            (0.0, 0.0),
            (45.0, 12.0),
            (-120.0, -30.0),
            (170.0, 60.0),
            (90.0, -45.0)
        };
        foreach (var (yaw, pitch) in yawPitchPairs)
        {
            var dir = PreviewLightMath.LightDirectionFromYawPitch(yaw, pitch);
            var (yawBack, pitchBack) = PreviewLightMath.LightYawPitchFromDirection(dir);
            Assert.Equal(yaw, yawBack, 4);
            Assert.Equal(pitch, pitchBack, 4);
        }
    }

    [Fact]
    public void LightDirectionFromYawPitchDefaultsMatchPriorHardcodedSun()
    {
        // Defaults in PreviewRenderSettings (-35 yaw, -55 pitch) replace the prior fallback
        // (-0.35, -0.85, -0.4); shadow ortho should now follow the user-controlled sun.
        var dir = PreviewLightMath.LightDirectionFromYawPitch(-35.0, -55.0);
        // Sun above horizon -> light propagates downward (Y < 0).
        Assert.True(dir.Y < 0f);
        // Yaw -35 with default pitch should mostly hit -X / +Z cone like the prior fallback (sign-wise).
        Assert.True(dir.X < 0f);
        Assert.True(dir.Z > 0f);
    }

    [Fact]
    public void LightMathPickShadowViewUpFallsBackForVerticalLight()
    {
        // A light pointing straight up/down should not pick the parallel +Y as the shadow up vector,
        // otherwise the cross product in lookAt collapses.
        var straightDown = new Vector3(0f, -1f, 0f);
        var up = PreviewLightMath.PickShadowViewUp(straightDown);
        Assert.Equal(Vector3.UnitZ, up);

        var slanted = PreviewLightMath.LightDirectionFromYawPitch(45.0, -30.0);
        var slantedUp = PreviewLightMath.PickShadowViewUp(slanted);
        Assert.Equal(Vector3.UnitY, slantedUp);
    }
}

public class GlslIncludeResolverTests
{
    [Fact]
    public void ResolveInlinesIncludesWithBeginEndMarkers()
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["main.glsl"] = "before\n//!include \"helper.glsl\"\nafter\n",
            ["helper.glsl"] = "HELPER_BODY\n"
        };

        var output = GlslIncludeResolver.Resolve("main.glsl", n => files[n]);
        // Entry file does NOT get begin/end markers (so #version stays the first token).
        Assert.DoesNotContain("// --- begin main.glsl ---", output);
        Assert.DoesNotContain("// --- end main.glsl ---", output);
        // Nested include DOES get markers.
        Assert.Contains("// --- begin helper.glsl ---", output);
        Assert.Contains("// --- end helper.glsl ---", output);
        Assert.Contains("HELPER_BODY", output);
        Assert.Contains("before", output);
        Assert.Contains("after", output);
        var helperIdx = output.IndexOf("HELPER_BODY", StringComparison.Ordinal);
        var beforeIdx = output.IndexOf("before", StringComparison.Ordinal);
        var afterIdx = output.IndexOf("after", StringComparison.Ordinal);
        Assert.True(beforeIdx < helperIdx && helperIdx < afterIdx);
    }

    [Fact]
    public void ResolveKeepsEntryVersionDirectiveAsFirstToken()
    {
        // Critical: #version must remain the first non-whitespace token after include flattening,
        // otherwise GLSL ES drivers (and the strip pass in GlslSourceAdapter) will reject the source.
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["entry.frag"] = "#version 330 core\n//!include \"x.glsl\"\nMAIN_BODY\n",
            ["x.glsl"] = "X_BODY\n"
        };
        var output = GlslIncludeResolver.Resolve("entry.frag", n => files[n]);
        var trimmed = output.TrimStart();
        Assert.StartsWith("#version 330 core", trimmed, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveIsIncludeGuardedAgainstCycles()
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["a.glsl"] = "//!include \"b.glsl\"\nA_TAIL\n",
            ["b.glsl"] = "//!include \"a.glsl\"\nB_TAIL\n"
        };

        var output = GlslIncludeResolver.Resolve("a.glsl", n => files[n]);
        Assert.Contains("A_TAIL", output);
        Assert.Contains("B_TAIL", output);
        Assert.Contains("skip duplicate include", output);
    }

    [Fact]
    public void ResolveResolvesRelativePathsWithinSubfolders()
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["genesis.frag"] = "//!include \"common/brdf.glsl\"\n",
            ["common/brdf.glsl"] = "//!include \"common.glsl\"\nBRDF_BODY\n",
            ["common/common.glsl"] = "COMMON_BODY\n"
        };

        var output = GlslIncludeResolver.Resolve("genesis.frag", n => files[n]);
        Assert.Contains("COMMON_BODY", output);
        Assert.Contains("BRDF_BODY", output);
        Assert.Contains("// --- begin common/brdf.glsl ---", output);
        Assert.Contains("// --- begin common/common.glsl ---", output);
        // Entry file (genesis.frag) MUST NOT get the begin/end marker.
        Assert.DoesNotContain("// --- begin genesis.frag ---", output);
    }

    [Fact]
    public void ResolveAbsolutePathStartsAtShadersRoot()
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["genesis.frag"] = "//!include \"common/brdf.glsl\"\n",
            ["common/brdf.glsl"] = "//!include \"/common/common.glsl\"\nBRDF\n",
            ["common/common.glsl"] = "COMMON\n"
        };

        var output = GlslIncludeResolver.Resolve("genesis.frag", n => files[n]);
        Assert.Contains("COMMON", output);
        Assert.Contains("BRDF", output);
    }

    [Fact]
    public void ResolveDepthCapTriggersOnSelfReference()
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        // Build a long chain that exceeds MaxIncludeDepth: a0 -> a1 -> ... -> a10
        for (var i = 0; i <= 10; i++)
        {
            var next = i < 10 ? $"//!include \"a{i + 1}.glsl\"\n" : string.Empty;
            files[$"a{i}.glsl"] = next + $"BODY_{i}\n";
        }

        var ex = Assert.Throws<InvalidOperationException>(() =>
            GlslIncludeResolver.Resolve("a0.glsl", n => files[n]));
        Assert.Contains("include depth", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ResolveMissingFileSurfacesAClearError()
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["main.glsl"] = "//!include \"nope.glsl\"\n"
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            GlslIncludeResolver.Resolve("main.glsl", n =>
                files.TryGetValue(n, out var v)
                    ? v
                    : throw new FileNotFoundException(n)));
        Assert.Contains("nope.glsl", ex.Message);
    }

    [Fact]
    public void ResolveNonDirectiveCommentsAreLeftAlone()
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["main.glsl"] = "// regular comment\n//!includes \"x\"\n//include \"x\"\nDONE\n"
        };

        var output = GlslIncludeResolver.Resolve("main.glsl", n => files[n]);
        Assert.Contains("// regular comment", output);
        Assert.Contains("//!includes \"x\"", output);
        Assert.Contains("//include \"x\"", output);
        Assert.Contains("DONE", output);
    }

    [Fact]
    public void ResolveAtmosphereIncludeFromCommonFolder()
    {
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["atmo_skyview.frag"] = "//!include \"common/atmosphere.glsl\"\nMAIN\n",
            ["common/atmosphere.glsl"] = "ATM_BODY\n"
        };

        var output = GlslIncludeResolver.Resolve("atmo_skyview.frag", n => files[n]);
        Assert.Contains("ATM_BODY", output);
        Assert.Contains("MAIN", output);
        Assert.Contains("// --- begin common/atmosphere.glsl ---", output);
    }
}
