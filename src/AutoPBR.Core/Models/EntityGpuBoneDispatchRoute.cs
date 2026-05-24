namespace AutoPBR.Core.Models;

/// <summary>
/// Dispatch tier used by GPU bone fast-fill when reusing a stable
/// <see cref="EntityEmulatedPreviewRebakeContext.GpuBoneDispatchRoute"/> across animation ticks.
/// </summary>
public enum EntityGpuBoneDispatchKind : byte
{
    /// <summary>No stable route (non-catalog specific-model branch).</summary>
    None = 0,

    /// <summary>Parity catalog builder (entity texture parity catalog).</summary>
    ParityCatalog,

    /// <summary>Generic family mesh after specific-model routing misses (humanoid / quadruped / flying / aquatic).</summary>
    FamilyFallback,

    /// <summary>Non-catalog <c>TryBuildSpecific</c> branch index (1-based, stable ordering in <c>CleanRoomEntityModelRuntime</c>).</summary>
    SpecificModelSlot
}

/// <summary>Family bucket mirrored from preview routing (unknown is never cached).</summary>
public enum EntityGpuBoneFamily : byte
{
    Humanoid = 1,
    Quadruped = 2,
    Flying = 3,
    Aquatic = 4
}

/// <summary>Closed-form bone-fill dispatch key: parity builder name or resolved stem family.</summary>
public readonly struct EntityGpuBoneDispatchRoute : IEquatable<EntityGpuBoneDispatchRoute>
{
    public EntityGpuBoneDispatchKind Kind { get; init; }

    /// <summary>Parity template method name when <see cref="Kind"/> is <see cref="EntityGpuBoneDispatchKind.ParityCatalog"/>.</summary>
    public string? ParityBuilderMethod { get; init; }

    /// <summary>Resolved geometry IR JVM when the catalog path emitted runtime IR (for LER policy on non-catalog rebakes).</summary>
    public string? GeometryIrOfficialJvm { get; init; }

    /// <summary>Family bucket when <see cref="Kind"/> is <see cref="EntityGpuBoneDispatchKind.FamilyFallback"/>.</summary>
    public EntityGpuBoneFamily Family { get; init; }

    /// <summary>1-based slot when <see cref="Kind"/> is <see cref="EntityGpuBoneDispatchKind.SpecificModelSlot"/>.</summary>
    public int SpecificSlot { get; init; }

    public static EntityGpuBoneDispatchRoute ForParity(string builderMethod, string? geometryIrOfficialJvm = null) =>
        new()
        {
            Kind = EntityGpuBoneDispatchKind.ParityCatalog,
            ParityBuilderMethod = builderMethod,
            GeometryIrOfficialJvm = geometryIrOfficialJvm,
            Family = default
        };

    public static EntityGpuBoneDispatchRoute ForFamily(EntityGpuBoneFamily family) =>
        new()
        {
            Kind = EntityGpuBoneDispatchKind.FamilyFallback,
            ParityBuilderMethod = null,
            Family = family
        };

    public static EntityGpuBoneDispatchRoute ForSpecificSlot(int specificSlot) =>
        new()
        {
            Kind = EntityGpuBoneDispatchKind.SpecificModelSlot,
            ParityBuilderMethod = null,
            Family = default,
            SpecificSlot = specificSlot
        };

    public bool Equals(EntityGpuBoneDispatchRoute other) =>
        Kind == other.Kind &&
        Family == other.Family &&
        SpecificSlot == other.SpecificSlot &&
        string.Equals(ParityBuilderMethod, other.ParityBuilderMethod, StringComparison.Ordinal) &&
        string.Equals(GeometryIrOfficialJvm, other.GeometryIrOfficialJvm, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is EntityGpuBoneDispatchRoute other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine((int)Kind, Family, SpecificSlot, ParityBuilderMethod, GeometryIrOfficialJvm);

    public static bool operator ==(EntityGpuBoneDispatchRoute left, EntityGpuBoneDispatchRoute right) => left.Equals(right);

    public static bool operator !=(EntityGpuBoneDispatchRoute left, EntityGpuBoneDispatchRoute right) => !left.Equals(right);
}
