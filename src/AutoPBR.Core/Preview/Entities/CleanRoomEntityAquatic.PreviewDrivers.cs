using System;
using System.Collections.Generic;
using System.Numerics;
// ReSharper disable CheckNamespace



namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{

    /// <summary>
    /// Preview swim offset for <see cref="BuildDolphin"/> tail pitch: vanilla motion is state-driven; this adds a bounded
    /// sine on <paramref name="animationTimeSeconds"/> so dolphin previews drift over time.
    /// </summary>
    internal static float ComputePreviewDolphinSwimOscillation(float animationTimeSeconds) =>
        MathF.Sin(animationTimeSeconds * (MathF.PI * 2f * 1.2f)) * 0.08f;

}
