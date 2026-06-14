namespace AutoPBR.Core.Preview;

/// <summary>Async-local preview pose override for entity static mesh builds (Explore pose selector).</summary>
public static class EntityPreviewBuildContext
{
    private static readonly AsyncLocal<string?> CurrentPoseIdSlot = new();

    public static string? CurrentPoseId => CurrentPoseIdSlot.Value;

    public static IDisposable UsePose(string? poseId)
    {
        var previous = CurrentPoseIdSlot.Value;
        CurrentPoseIdSlot.Value = poseId;
        return new PoseScope(previous);
    }

    private sealed class PoseScope(string? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            CurrentPoseIdSlot.Value = previous;
            _disposed = true;
        }
    }
}
