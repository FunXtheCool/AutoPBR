using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    // Bat, chicken, bee, ghast, blaze, vex, phantom, parrot.

    /// <summary>
    /// Flying-family fallback when <see cref="TryBuildSpecific"/> did not match: delegates to the same cuboids/poses as
    /// <see cref="BuildPhantom"/> (Java <c>PhantomModel</c> / <c>het</c>) so UVs and rig match vanilla; <paramref name="wingSpread"/>
    /// drives the same flap phase as <c>setupAnim</c>. The eyes layer reuses <paramref name="texRef"/> so callers without
    /// <c>phantom_eyes.png</c> still resolve textures.
    /// Exposed <see langword="internal"/> for parity tests — prefer dedicated rigs in <see cref="TryBuildSpecific"/>.
    /// </summary>
    internal static MergedJavaBlockModel BuildFlying(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float wingSpread) =>
        BuildPhantom(
            normalizedAssetPath: "assets/minecraft/textures/entity/phantom/phantom.png",
            texRef,
            profile,
            isBaby,
            flapTime: wingSpread,
            eyesTextureRefOverride: texRef);

}
