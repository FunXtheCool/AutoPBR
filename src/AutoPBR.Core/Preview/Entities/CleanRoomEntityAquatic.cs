using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

using AutoPBR.Core.Models;

namespace AutoPBR.Core.Preview;

internal sealed partial class CleanRoomEntityModelRuntime
{
    // Fish, dolphin, axolotl, frog, turtle, guardian, nautilus.

    /// <summary>
    /// Aquatic-family fallback when <see cref="TryBuildSpecific"/> did not match. Vanilla stems in <see cref="AquaticKeys"/> almost
    /// always resolve to dedicated rigs (cod, salmon, dolphin, …); this path is rare. Mesh/UVs match Java <c>CodModel</c>
    /// (<see cref="BuildCod"/> — <c>getTexturedModelData</c>, <c>32×32</c> atlas).
    /// Exposed <see langword="internal"/> for parity tests.
    /// </summary>
    internal static MergedJavaBlockModel BuildAquatic(
        string texRef,
        MinecraftNativeProfile profile,
        bool isBaby,
        float tailSway) =>
        BuildCod(texRef, profile, isBaby, tailSway);

}
