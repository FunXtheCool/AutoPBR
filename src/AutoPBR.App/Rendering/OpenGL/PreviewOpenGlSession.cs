namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>Startup OpenGL profile request applied before the preview context is created.</summary>
internal static class PreviewOpenGlSession
{
    /// <summary>True when <see cref="Models.UserSettings.PreviewUseOpenGl4"/> was read at app launch.</summary>
    public static bool RequestedDesktopGl4 { get; set; }
}
