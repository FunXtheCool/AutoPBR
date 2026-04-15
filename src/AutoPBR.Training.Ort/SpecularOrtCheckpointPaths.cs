namespace AutoPBR.Training.Ort;

/// <summary>Resolves ORT CheckpointState path like Python <c>train_spec_ort</c>.</summary>
public static class SpecularOrtCheckpointPaths
{
    public static string ResolveLoadPath(string ortArtifactsDir, string? ortCheckpointPathOrNull)
    {
        var artifactsDir = Path.GetFullPath(ortArtifactsDir);
        var sessionCkpt = string.IsNullOrWhiteSpace(ortCheckpointPathOrNull)
            ? Path.Combine(artifactsDir, "ort_training_state")
            : Path.GetFullPath(ortCheckpointPathOrNull);

        if (Directory.Exists(sessionCkpt) || File.Exists(sessionCkpt))
        {
            return sessionCkpt;
        }

        var bootstrap = Path.Combine(artifactsDir, "checkpoint");
        if (Directory.Exists(bootstrap) || File.Exists(bootstrap))
        {
            return bootstrap;
        }

        throw new FileNotFoundException(
            $"No ORT checkpoint. Expected session state at '{sessionCkpt}' or bootstrap at '{bootstrap}'. " +
            "Generate artifacts with Python ml_specular.generate_ort_training_artifacts.",
            sessionCkpt);
    }

    public static string SessionSavePath(string ortArtifactsDir, string? ortCheckpointPathOrNull) =>
        string.IsNullOrWhiteSpace(ortCheckpointPathOrNull)
            ? Path.Combine(Path.GetFullPath(ortArtifactsDir), "ort_training_state")
            : Path.GetFullPath(ortCheckpointPathOrNull);
}
