namespace AutoPBR.Core.Models;

/// <summary>
/// Dispatch tier used by GPU bone fast-fill when reusing a stable
/// <see cref="EntityEmulatedPreviewRebakeContext.GpuBoneDispatchRoute"/> across animation ticks.
/// </summary>
public enum EntityGpuBoneDispatchKind : byte
{
    /// <summary>No stable route.</summary>
    None = 0,

    /// <summary>Parity catalog builder (entity texture parity catalog).</summary>
    ParityCatalog,
}

/// <summary>Family bucket mirrored from preview routing (unused; retained for binary compatibility).</summary>
[Obsolete("Family fallback meshes removed; only parity-catalog IR routes are cached.")]
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

    /// <summary>Resolved geometry IR JVM when the catalog path emitted runtime IR.</summary>
    public string? GeometryIrOfficialJvm { get; init; }

    public static EntityGpuBoneDispatchRoute ForParity(string builderMethod, string? geometryIrOfficialJvm = null) =>
        new()
        {
            Kind = EntityGpuBoneDispatchKind.ParityCatalog,
            ParityBuilderMethod = builderMethod,
            GeometryIrOfficialJvm = geometryIrOfficialJvm,
        };

    public bool Equals(EntityGpuBoneDispatchRoute other) =>
        Kind == other.Kind &&
        string.Equals(ParityBuilderMethod, other.ParityBuilderMethod, StringComparison.Ordinal) &&
        string.Equals(GeometryIrOfficialJvm, other.GeometryIrOfficialJvm, StringComparison.Ordinal);

    public override bool Equals(object? obj) => obj is EntityGpuBoneDispatchRoute other && Equals(other);

    public override int GetHashCode() =>
        HashCode.Combine((int)Kind, ParityBuilderMethod, GeometryIrOfficialJvm);

    public static bool operator ==(EntityGpuBoneDispatchRoute left, EntityGpuBoneDispatchRoute right) => left.Equals(right);

    public static bool operator !=(EntityGpuBoneDispatchRoute left, EntityGpuBoneDispatchRoute right) => !left.Equals(right);
}
