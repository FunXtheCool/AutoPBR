using System.Globalization;
using System.Text;

using Xunit.Abstractions;

namespace AutoPBR.Core.Tests;

public sealed class ParityCatalogMeshDriverSurveyDiagnostics(ITestOutputHelper output)
{
    [Fact]
    public void Survey_cleanroom_failure_reasons()
    {
        var detailed = ParityCatalogIrSurveyHelper.RunDetailed();
        var cleanRoom = detailed.Summary.CleanRoom;
        var reasons = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var byBuilder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in detailed.Rows.Where(r => r.DriverKind == PreviewMeshDriverKind.CleanRoom))
        {
            byBuilder[row.BuilderMethod] = byBuilder.GetValueOrDefault(row.BuilderMethod) + 1;
            var reason = row.IrFailureReason ?? "unknown";
            reasons[reason] = reasons.GetValueOrDefault(reason) + 1;
        }

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"CleanRoom paths: {cleanRoom}");
        sb.AppendLine("By failure reason:");
        foreach (var kv in reasons.OrderByDescending(kv => kv.Value))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"  {kv.Value,4}  {kv.Key}");
        }

        sb.AppendLine("By builder_method:");
        foreach (var kv in byBuilder.OrderByDescending(kv => kv.Value))
        {
            sb.AppendLine(CultureInfo.InvariantCulture, $"  {kv.Value,4}  {kv.Key}");
        }

        output.WriteLine(sb.ToString());
    }
}
