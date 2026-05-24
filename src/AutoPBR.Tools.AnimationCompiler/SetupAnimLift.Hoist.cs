using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace AutoPBR.Tools.AnimationCompiler;

internal static partial class SetupAnimLift
{
    public static bool TryHoistAbstractHostSetupAnim(
        string javapExe,
        string clientJar,
        string officialJvmName,
        out JsonObject shard,
        out List<string> notes)
    {
        shard = new JsonObject();
        notes = [];
        if (!AbstractSetupAnimHoistTemplates.TryGetValue(officialJvmName, out var templateJvm))
        {
            return false;
        }

        if (!JavapRunner.TryDisassemble(javapExe, clientJar, templateJvm, out var templateDisasm, out _))
        {
            return false;
        }

        if (!TryLift(templateDisasm, templateJvm, out shard, out notes))
        {
            return false;
        }

        shard["inheritsSetupAnimFrom"] = "net.minecraft.client.model.EntityModel";
        shard["superSetupAnimCall"] = true;
        notes.RemoveAll(n =>
            n.Contains("No typed setupAnim", StringComparison.Ordinal) ||
            n.Contains("No assignments or playback", StringComparison.Ordinal));
        notes.Add(
            $"SetupAnim rules hoisted from {templateJvm} (abstract class has no local setupAnim bytecode).");
        if (shard["assignments"] is JsonArray hoistedAssignments)
        {
            TryClearFelineBlockingStackNotes(officialJvmName, hoistedAssignments, notes);
        }

        return true;
    }

    public static bool TryExtractTypedSetupAnimCode(string javapStdout, out string renderStateType, out string code)
    {
        renderStateType = "";
        code = "";
        var bestIdx = -1;
        var bestScore = int.MinValue;
        string? bestType = null;
        foreach (Match m in TypedSetupAnimRegex.Matches(javapStdout))
        {
            var param = m.Groups[1].Value.Trim();
            var score = 0;
            if (param.Contains("RenderState", StringComparison.Ordinal))
            {
                score += 10;
            }

            if (string.Equals(param, "T", StringComparison.Ordinal))
            {
                score += 5;
            }

            if (param.Contains("java.lang.Object", StringComparison.Ordinal))
            {
                score -= 20;
            }

            if (m.Index > bestIdx && score >= bestScore)
            {
                bestIdx = m.Index;
                bestScore = score;
                bestType = param;
            }
        }

        if (bestIdx < 0 || bestType is null)
        {
            return false;
        }

        renderStateType = bestType;
        return TryExtractCodeBlockAfterDeclarationIndex(javapStdout, bestIdx, out code);
    }

    private static bool TryExtractCodeBlockAfterDeclarationIndex(string javapC, int declarationIdx, out string code)
    {
        code = "";
        var codeMark = javapC.IndexOf("    Code:", declarationIdx, StringComparison.Ordinal);
        if (codeMark < 0)
        {
            return false;
        }

        var after = codeMark + "    Code:".Length;
        var end = javapC.IndexOf("\n    }", after, StringComparison.Ordinal);
        if (end < 0)
        {
            // Some javap dumps omit the closing "    }" and jump straight to the next member.
            var nextMember = NextMemberDeclarationRegex.Match(javapC, after);
            end = nextMember.Success ? nextMember.Index : javapC.Length;
        }

        code = javapC[codeMark..end];
        return true;
    }

    private static readonly Regex NextMemberDeclarationRegex = new(
        @"\n  (?:public|protected|private) ",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(2));

    private static string NormalizeTypeName(string raw) =>
        raw.Replace('/', '.').Replace('$', '.');
}
