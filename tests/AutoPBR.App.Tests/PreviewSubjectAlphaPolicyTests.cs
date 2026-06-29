using AutoPBR.App.Rendering;
using AutoPBR.App.Rendering.Abstractions;

namespace AutoPBR.App.Tests;

public sealed class PreviewSubjectAlphaPolicyTests
{
    [Theory]
    [InlineData(PreviewSceneKind.BlockModel, false, PreviewEntityAlphaMode.Opaque, PreviewEntityAlphaMode.Cutout)]
    [InlineData(PreviewSceneKind.BlockCube, false, PreviewEntityAlphaMode.Opaque, PreviewEntityAlphaMode.Cutout)]
    [InlineData(PreviewSceneKind.BlockModel, true, PreviewEntityAlphaMode.Blend, PreviewEntityAlphaMode.Blend)]
    [InlineData(PreviewSceneKind.ItemPlane, false, PreviewEntityAlphaMode.Cutout, PreviewEntityAlphaMode.Opaque)]
    public void ResolveAlphaModeUniform_matches_subject_kind(
        PreviewSceneKind sceneKind,
        bool entityEmulated,
        PreviewEntityAlphaMode entityMode,
        PreviewEntityAlphaMode expected)
    {
        var uniform = PreviewSubjectAlphaPolicy.ResolveAlphaModeUniform(sceneKind, entityEmulated, entityMode);
        Assert.Equal((int)expected, uniform);
    }
}
