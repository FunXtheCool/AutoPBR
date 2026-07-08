namespace AutoPBR.Preview;

/// <summary>Single-texture PBR preview PNG plus optional brick height probe diagnostics.</summary>
public sealed record PreviewRenderResult(byte[] PngBytes, string? BrickProbeDebugText);
