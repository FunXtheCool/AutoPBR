using AutoPBR.App.Rendering.OpenGL;

namespace AutoPBR.App.Tests;

public sealed class PreviewGlCapabilitiesTests
{
    [Fact]
    public void GlesAngle_UsesCompatibilityFeatureSet()
    {
        var caps = PreviewGlCapabilities.FromStrings(
            "OpenGL ES 3.0 (ANGLE 2.1.1)",
            "Google Inc.",
            "ANGLE D3D11",
            "GL_EXT_disjoint_timer_query",
            forceOpenGlEs: true);

        Assert.True(caps.IsOpenGlEs);
        Assert.Equal(3, caps.Major);
        Assert.Equal(0, caps.Minor);
        Assert.True(caps.TextureArrays);
        Assert.True(caps.TimerQuery);
        Assert.False(caps.BufferStorage);
        Assert.False(caps.CanUsePersistentUploadRing);
        Assert.False(caps.ShaderStorageBuffers);
        Assert.False(caps.ComputeShaders);
        Assert.False(caps.ImageLoadStore);
        Assert.Contains("persistentUpload=off", caps.FormatDiagnostic(), StringComparison.Ordinal);
        Assert.Contains("GLES-safe uploads", caps.FormatContextSuffix(), StringComparison.Ordinal);
    }

    [Fact]
    public void DesktopGl33_DoesNotAssumeModernAcceleration()
    {
        var caps = PreviewGlCapabilities.FromStrings(
            "3.3.0 Compatibility Profile Context",
            "Vendor",
            "Renderer",
            string.Empty,
            forceOpenGlEs: false);

        Assert.False(caps.IsOpenGlEs);
        Assert.True(caps.TextureArrays);
        Assert.True(caps.TimerQuery);
        Assert.False(caps.BufferStorage);
        Assert.False(caps.CanUsePersistentUploadRing);
        Assert.False(caps.ShaderStorageBuffers);
        Assert.False(caps.ComputeShaders);
        Assert.False(caps.MultiDrawIndirect);
    }

    [Fact]
    public void DesktopGl40_KeepsGl46SystemsDisabled()
    {
        var caps = PreviewGlCapabilities.FromStrings(
            "4.0.0 NVIDIA 999.00",
            "NVIDIA",
            "RTX",
            string.Empty,
            forceOpenGlEs: false);

        Assert.False(caps.IsOpenGlEs);
        Assert.True(caps.TextureArrays);
        Assert.True(caps.TimerQuery);
        Assert.False(caps.BufferStorage);
        Assert.False(caps.CanUsePersistentUploadRing);
        Assert.False(caps.ShaderStorageBuffers);
        Assert.False(caps.ComputeShaders);
        Assert.False(caps.ImageLoadStore);
        Assert.False(caps.SpirV);
    }

    [Fact]
    public void DesktopGl46_EnablesCoreModernSystems()
    {
        var caps = PreviewGlCapabilities.FromStrings(
            "4.6.0 NVIDIA 999.00",
            "NVIDIA",
            "RTX",
            "GL_ARB_bindless_texture",
            forceOpenGlEs: false);

        Assert.False(caps.IsOpenGlEs);
        Assert.True(caps.BufferStorage);
        Assert.True(caps.CanUsePersistentUploadRing);
        Assert.True(caps.ShaderStorageBuffers);
        Assert.True(caps.ComputeShaders);
        Assert.True(caps.ImageLoadStore);
        Assert.True(caps.ShaderAtomics);
        Assert.True(caps.MultiDrawIndirect);
        Assert.True(caps.TimerQuery);
        Assert.True(caps.TextureArrays);
        Assert.True(caps.BindlessTextures);
        Assert.True(caps.SpirV);
        Assert.True(caps.SeparablePrograms);
        Assert.Contains("persistentUpload=on", caps.FormatDiagnostic(), StringComparison.Ordinal);
        Assert.Contains("persistent uploads", caps.FormatContextSuffix(), StringComparison.Ordinal);
    }

    [Fact]
    public void DesktopExtensions_CanEnableIndividualSystemsBelowCoreVersion()
    {
        var caps = PreviewGlCapabilities.FromStrings(
            "4.3.0 Mesa",
            "Mesa",
            "Renderer",
            "GL_ARB_buffer_storage GL_ARB_gl_spirv",
            forceOpenGlEs: false);

        Assert.True(caps.BufferStorage);
        Assert.True(caps.CanUsePersistentUploadRing);
        Assert.True(caps.ShaderStorageBuffers);
        Assert.True(caps.ComputeShaders);
        Assert.True(caps.ImageLoadStore);
        Assert.True(caps.MultiDrawIndirect);
        Assert.True(caps.SpirV);
    }
}
