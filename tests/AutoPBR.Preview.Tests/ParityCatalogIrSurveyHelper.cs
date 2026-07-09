using AutoPBR.Core.Models;
using AutoPBR.Preview;

namespace AutoPBR.Core.Tests;

internal static class ParityCatalogIrSurveyHelper
{
    internal readonly record struct SurveyResult(
        int Total,
        int Ir,
        int ErrorPlaceholder,
        int Failed,
        IReadOnlyList<string> ErrorPlaceholderPaths,
        IReadOnlyDictionary<string, int> FailureReasons);

    internal readonly record struct DetailedSurveyResult(
        SurveyResult Summary,
        IReadOnlyList<ParityCatalogEntityPreviewDiagnostics.Row> Rows);

    internal static MinecraftNativeProfile ResolveDefaultProfile()
    {
        var nativeRoot = Path.Combine(AppContext.BaseDirectory, "Data", "minecraft-native");
        return MinecraftNativeProfileResolver.ResolveAutoLatestModern(nativeRoot)
               ?? new MinecraftNativeProfile(
                   NativeIrVersionLabels.ModernGeometryLabel,
                   nativeRoot,
                   new Version(26, 1, 2));
    }

    internal static SurveyResult Run(MinecraftNativeProfile? profileOverride = null)
    {
        var detailed = RunDetailed(profileOverride);
        return detailed.Summary;
    }

    internal static DetailedSurveyResult RunDetailed(MinecraftNativeProfile? profileOverride = null)
    {
        var profile = profileOverride ?? ResolveDefaultProfile();
        var paths = EntityTextureParityCatalog.GetCataloguedDiffusePathsWithManifestRules();
        var rows = ParityCatalogEntityPreviewDiagnostics.SurveyAllCatalog(profile);
        var ir = 0;
        var errorPlaceholder = 0;
        var failed = new List<string>();
        var errorPlaceholderPaths = new List<string>();
        var reasons = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            if (!row.BuildSucceeded)
            {
                failed.Add(row.TexturePath);
                continue;
            }

            if (row.DriverKind == PreviewMeshDriverKind.RuntimeGeometryIrJson)
            {
                ir++;
            }
            else if (row.DriverKind == PreviewMeshDriverKind.ErrorPlaceholder)
            {
                errorPlaceholder++;
                errorPlaceholderPaths.Add(row.TexturePath);
                var reason = row.IrFailureReason ?? "unknown";
                reasons[reason] = reasons.GetValueOrDefault(reason) + 1;
            }
        }

        var summary = new SurveyResult(paths.Count, ir, errorPlaceholder, failed.Count, errorPlaceholderPaths, reasons);
        return new DetailedSurveyResult(summary, rows);
    }
}
