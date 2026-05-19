using System.Globalization;
using System.Text.RegularExpressions;
// ReSharper disable HeuristicUnreachableCode


namespace AutoPBR.Tools.GeometryCompiler;

internal static partial class JavapFloatGeometryMeshLift
{
    /// <summary>
    /// Drops bytecode lines in mutually exclusive <c>iload_1</c> + <c>ifeq</c>/<c>ifne</c> arms (e.g. PlayerModel slim vs wide).
    /// Uses boolean factory default <c>false</c> (0), matching <c>GeometryReferenceBake.invokeWithDefaults</c>.
    /// </summary>
    private static List<string> PruneUnreachableMeshFactoryBranches(List<string> lines)
    {
        if (lines.Count == 0)
        {
            return lines;
        }

        const int booleanFactorySlot = 1;
        var excludedOffsets = new HashSet<int>();

        for (var i = 0; i < lines.Count - 1; i++)
        {
            if (!JavapBytecodeStreamAnalyzer.TryParseIloadLocalSlot(lines[i], out var slot) || slot != booleanFactorySlot)
            {
                continue;
            }

            var branchLine = i + 1;
            while (branchLine < lines.Count && branchLine < i + 4 &&
                   !JavapBytecodeStreamAnalyzer.TryParseConditionalBranch(lines[branchLine], out _, out _))
            {
                branchLine++;
            }

            if (branchLine >= lines.Count ||
                !JavapBytecodeStreamAnalyzer.TryParseConditionalBranch(lines[branchLine], out var op, out var jumpTarget))
            {
                continue;
            }

            if (!JavapBytecodeStreamAnalyzer.TryParseLineBytecodeOffset(lines[branchLine], out var branchInsnOffset))
            {
                continue;
            }

            if (string.Equals(op, "ifeq", StringComparison.Ordinal))
            {
                // Factory default is false (0): jump to wide arm; slim fall-through is dead.
                var fallThrough = branchInsnOffset + 1;
                AddExcludedBytecodeOffsets(excludedOffsets, lines, fallThrough, jumpTarget);
            }
            else if (string.Equals(op, "ifne", StringComparison.Ordinal))
            {
                // Factory default is false (0): wide arm at jumpTarget is dead until merge goto.
                if (TryFindForwardGotoTarget(lines, branchLine, out var mergeTarget))
                {
                    AddExcludedBytecodeOffsets(excludedOffsets, lines, jumpTarget, mergeTarget);
                }
            }
        }

        if (excludedOffsets.Count == 0)
        {
            return lines;
        }

        var pruned = new List<string>(lines.Count);
        foreach (var line in lines)
        {
            if (JavapBytecodeStreamAnalyzer.TryParseLineBytecodeOffset(line, out var offset) && excludedOffsets.Contains(offset))
            {
                continue;
            }

            pruned.Add(line);
        }

        return pruned;
    }

    private static void AddExcludedBytecodeOffsets(HashSet<int> excluded, IReadOnlyList<string> lines, int fromInclusive,
        int toExclusive)
    {
        foreach (var line in lines)
        {
            if (JavapBytecodeStreamAnalyzer.TryParseLineBytecodeOffset(line, out var offset) && offset >= fromInclusive && offset < toExclusive)
            {
                excluded.Add(offset);
            }
        }
    }

    private static bool TryFindForwardGotoTarget(IReadOnlyList<string> lines, int afterBranchLine, out int targetOffset)
    {
        targetOffset = 0;
        for (var i = afterBranchLine + 1; i < lines.Count && i < afterBranchLine + 256; i++)
        {
            if (!JavapBytecodeStreamAnalyzer.TryParseConditionalBranch(lines[i], out var op, out var target) ||
                !string.Equals(op, "goto", StringComparison.Ordinal))
            {
                continue;
            }

            targetOffset = target;
            return true;
        }

        return false;
    }
}
