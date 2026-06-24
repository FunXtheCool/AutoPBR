namespace AutoPBR.Core.Preview;

/// <summary>Async-local preview overrides for entity static mesh builds (Explore pose/size selectors).</summary>
public static class EntityPreviewBuildContext
{
    private static readonly AsyncLocal<string?> CurrentPoseIdSlot = new();
    private static readonly AsyncLocal<string?> CurrentSizeIdSlot = new();

    public static string? CurrentPoseId => CurrentPoseIdSlot.Value;

    public static string? CurrentSizeId => CurrentSizeIdSlot.Value;

    public static IDisposable UsePose(string? poseId)
    {
        var previous = CurrentPoseIdSlot.Value;
        CurrentPoseIdSlot.Value = poseId;
        return new SlotScope(CurrentPoseIdSlot, previous);
    }

    public static IDisposable UseSize(string? sizeId)
    {
        var previous = CurrentSizeIdSlot.Value;
        CurrentSizeIdSlot.Value = sizeId;
        return new SlotScope(CurrentSizeIdSlot, previous);
    }

    private sealed class SlotScope(AsyncLocal<string?> slot, string? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            slot.Value = previous;
            _disposed = true;
        }
    }
}
