namespace AutoPBR.Core.Models;

/// <summary>Single-texture PBR preview PNG plus optional brick height probe diagnostics.</summary>
public sealed record PreviewRenderResult(byte[] PngBytes, string? BrickProbeDebugText);
