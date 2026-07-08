namespace AutoPBR.Contracts.Ml;

/// <summary>ONNX specular model path resolution inputs (decoupled from full conversion options).</summary>
public sealed record MlSpecularPathOptions(
    bool Enabled,
    string? DefaultModelPath,
    IReadOnlyDictionary<int, string>? ModelPathsByResolution);
