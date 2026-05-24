using System.Numerics;

using AutoPBR.Core.Models;

using JetBrains.Annotations;

namespace AutoPBR.App.Rendering.Abstractions;

[UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
public interface IRenderPreviewBackend : IDisposable
{
    string BackendName { get; }
    bool IsInitialized { get; }
    string? LastErrorMessage { get; }

    void Initialize(RenderPreviewInitializationOptions options);
    void Resize(int width, int height);
    void SetScene(IRenderPreviewScene scene);
    void SetMaterial(PreviewMaterial? material);
    void SetBlockModelPreview(PreviewModelSubject? subject, PreviewMaterial[]? slotMaterials);
    void SetRenderSettings(PreviewRenderSettings settings);
    void RenderFrame(TimeSpan elapsed);

    /// <summary>Debug: world-space eye and look target for on-screen readout (OpenGL path).</summary>
    bool TryGetCameraDebugPose(out Vector3 eye, out Vector3 lookTarget);

    /// <summary>Debug fly: hold right mouse for FPS-style look; WASD moves while right mouse is held.</summary>
    void SetDebugFlyInput(bool rightMouseHeld, bool keyW, bool keyA, bool keyS, bool keyD);
}
