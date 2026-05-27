using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.AnimationCompiler;

internal static partial class SetupAnimLift
{
    private static bool HasQuadrupedFourLegAssignments(JsonArray assignments)
    {
        var legs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var n in assignments)
        {
            if (n is not JsonObject o ||
                !string.Equals((string?)o["property"], "xRot", StringComparison.Ordinal))
            {
                continue;
            }

            var part = (string?)o["partField"];
            if (part is "rightHindLeg" or "leftHindLeg" or "rightFrontLeg" or "leftFrontLeg")
            {
                legs.Add(part);
            }
        }

        return legs.Count == 4;
    }

}
