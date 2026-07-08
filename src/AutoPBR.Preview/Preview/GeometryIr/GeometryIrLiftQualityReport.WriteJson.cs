using System.Text.Json;

namespace AutoPBR.Preview.GeometryIr;

/// <summary>Metrics for geometry IR shards (lift quality baseline / regression).</summary>
public static partial class GeometryIrLiftQualityReport
{
    private static readonly JsonSerializerOptions WriteJsonOptions = new() { WriteIndented = true };

    public static void WriteJson(Document doc, string outputPath)
    {
        var entries = new List<object>();
        foreach (var e in doc.Entries)
        {
            entries.Add(new
            {
                officialJvmName = e.OfficialJvmName,
                extractionStatus = e.ExtractionStatus,
                cuboidCount = e.CuboidCount,
                maxTreeDepth = e.MaxTreeDepth,
                rootChildCount = e.RootChildCount,
                suspectedFlatNestedPartCount = e.SuspectedFlatNestedPartCount,
                liftWarningCounts = e.LiftWarningCounts,
                referenceCuboidsMatch = e.ReferenceCuboidsMatch,
                referenceCompareMessage = e.ReferenceCompareMessage,
                referencePosesMatch = e.ReferencePosesMatch,
                referencePoseCompareMessage = e.ReferencePoseCompareMessage,
                referenceMeshMatch = e.ReferenceMeshMatch,
                referenceMeshCompareMessage = e.ReferenceMeshCompareMessage,
                referenceWorldPoseMatch = e.ReferenceWorldPoseMatch,
                referenceWorldPoseCompareMessage = e.ReferenceWorldPoseCompareMessage,
                referenceHierarchyMatch = e.ReferenceHierarchyMatch,
                referenceHierarchyMessage = e.ReferenceHierarchyMessage,
                extractionBindingGap = e.ExtractionBindingGap,
                assemblyGatePass = e.AssemblyGatePass,
                javapPoseOracleMatch = e.JavapPoseOracleMatch,
                javapPoseOracleMessage = e.JavapPoseOracleMessage
            });
        }

        var root = new
        {
            schemaVersion = 2,
            versionLabel = doc.VersionLabel,
            generatedUtc = doc.GeneratedUtc.ToString("O"),
            okEntryCount = doc.OkEntryCount,
            prioritizedBacklogJvmNames = doc.PrioritizedBacklogJvmNames,
            entries
        };

        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(outputPath, JsonSerializer.Serialize(root, WriteJsonOptions));
    }
}
