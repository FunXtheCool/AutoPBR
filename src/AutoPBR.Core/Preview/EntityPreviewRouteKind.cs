namespace AutoPBR.Core.Preview;

/// <summary>
/// How a vanilla entity diffuse path resolves in <see cref="CleanRoomEntityModelRuntime"/> after
/// <see cref="CleanRoomEntityModelRuntime.TryBuildSpecific"/> vs family fallbacks.
/// </summary>
internal enum EntityPreviewRouteKind
{
    InvalidPath,
    SpecificMesh,
    HumanoidFamilyFallback,
    QuadrupedFamilyFallback,
    FlyingFamilyFallback,
    AquaticFamilyFallback,
    UnknownNoMesh,
}
