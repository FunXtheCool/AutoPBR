namespace AutoPBR.App.Rendering.Abstractions;

/// <summary>How diffuse alpha is interpreted for clean-room entity preview rigs (<see cref="AutoPBR.Core.Models.PreviewModelSubject.AnimationPreset"/> <c>entity_emulated</c>).</summary>
public enum PreviewEntityAlphaMode
{
    /// <summary>Ignore diffuse alpha (fully opaque shaded surface).</summary>
    // ReSharper disable once UnusedMember.Global — default / unset maps to opaque in preview shaders.
    Opaque = 0,

    /// <summary>Discard fragments below <see cref="PreviewRenderSettings.AlphaCutoff"/>.</summary>
    Cutout = 1,

    /// <summary>Use src-alpha blending (preview-only; imperfect depth ordering).</summary>
    Blend = 2
}
