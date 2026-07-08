using System.Text.RegularExpressions;

namespace AutoPBR.Preview;

public static partial class GeometryJavapPoseOracle
{
    private static partial class Parser
    {
        private static IEnumerable<string> FindSameClassMeshDefinitionFactories(string scopeText)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (Match m in SameClassMeshDefinitionInvokeRegex.Matches(scopeText))
            {
                var method = m.Groups[1].Value;
                if (seen.Add(method))
                {
                    yield return method;
                }
            }
        }

        private static void MergeKnownSameClassLegHelper(
            string hostText,
            string scopeText,
            Dictionary<string, PartPose> poses,
            MeshParamContext ctx)
        {
            if (!scopeText.Contains("Method createLegs:", StringComparison.Ordinal))
            {
                return;
            }

            var methodText = ExtractMethodText(hostText, "createLegs");
            if (methodText is null)
            {
                return;
            }

            var legPoses = ParseBindings(methodText, ctx);
            foreach (var (partId, pose) in legPoses)
            {
                poses[partId] = pose;
            }
        }

        private static void MergeSameClassVoidHelpers(
            string hostText,
            string scopeText,
            Dictionary<string, PartPose> poses,
            MeshParamContext ctx)
        {
            foreach (Match m in SameClassStaticInvokeRegex.Matches(scopeText))
            {
                if (!m.Value.Contains(")V", StringComparison.Ordinal))
                {
                    continue;
                }

                var methodName = m.Groups[1].Value;
                var methodText = ExtractMethodText(hostText, methodName);
                if (methodText is null)
                {
                    continue;
                }

                var helperPoses = ParseBindings(methodText, ctx);
                foreach (var (partId, pose) in helperPoses)
                {
                    poses[partId] = pose;
                }
            }
        }

        public static string? TryFindCrossClassMeshFactoryOwner(string javapText) =>
            TryFindCrossClassMeshFactory(javapText)?.Owner;

        private static (string Owner, string Method)? TryFindCrossClassMeshFactory(string javapText)
        {
            foreach (Match m in MeshDelegateInvokeRegex.Matches(javapText))
            {
                var owner = m.Groups[1].Value.Replace('/', '.');
                if (!owner.Contains('.') ||
                    owner.Contains("LayerDefinition", StringComparison.Ordinal) ||
                    owner.Contains("builders.MeshDefinition", StringComparison.Ordinal))
                {
                    continue;
                }

                return (owner, m.Groups[2].Value);
            }

            return null;
        }

        private static int? TryExtractCreateBodyMeshAgeParam(string javapText)
        {
            var lines = FoldWrappedLines(javapText.Split('\n'));
            for (var i = 0; i < lines.Count; i++)
            {
                if (!lines[i].Contains("createBodyMesh:(", StringComparison.Ordinal))
                {
                    continue;
                }

                for (var j = i - 1; j >= Math.Max(0, i - 8); j--)
                {
                    if (TryParseBipushLine(lines[j], out var age))
                    {
                        return age;
                    }
                }
            }

            return null;
        }
    }
}
