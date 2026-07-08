namespace AutoPBR.Preview.Parity;

/// <summary>
/// One routing row from <c>minecraft_26.1.2_entity_texture_model_manifest.json</c> (vanilla 26.1.2 entity diffuse parity).
/// </summary>
public sealed class EntityTextureParityRule
{
    public required string PathPrefix { get; init; }

    public required string BuilderMethod { get; init; }

    public string? DeobfuscatedModelClass { get; init; }

    /// <summary>
    /// Mojmap-era flat <c>net.minecraft.client.model.*</c> (and legacy renderer placeholders) for javap against pre-restructure clients.
    /// </summary>
    public string? DeobfuscatedModelClassPreRestructure { get; init; }

    public string? Notes { get; init; }

    /// <summary>
    /// When set, geometry IR is loaded from this official JVM shard name instead of <see cref="DeobfuscatedModelClass"/>.
    /// </summary>
    public string? GeometryIrOfficialJvm { get; init; }

    /// <summary>
    /// When <see cref="GeometryIrOfficialJvm"/> is not used for babies, optional explicit baby IR shard (else baby peer heuristic).
    /// </summary>
    public string? GeometryIrOfficialJvmBaby { get; init; }

    /// <summary>Optional entity skin width in texels for IR UV normalization (manifest-driven).</summary>
    public int? GeometryIrTextureWidth { get; init; }

    /// <summary>Optional entity skin height in texels for IR UV normalization (manifest-driven).</summary>
    public int? GeometryIrTextureHeight { get; init; }
}
