namespace AutoPBR.Tools.AnimationCompiler;

/// <summary>
/// Resolves concrete model classes that own <c>setupAnim</c> when the batch entry is abstract or a mesh-only outer name.
/// </summary>
internal static class ModelHostClassCandidates
{
    public static IEnumerable<string> Enumerate(string officialJvmName)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in EnumerateRaw(officialJvmName))
        {
            if (seen.Add(s))
            {
                yield return s;
            }
        }
    }

    private static IEnumerable<string> EnumerateRaw(string officialJvmName)
    {
        yield return officialJvmName;
        var idx = officialJvmName.LastIndexOf('.');
        if (idx < 0)
        {
            yield break;
        }

        var pkg = officialJvmName[..idx];
        var simple = officialJvmName[(idx + 1)..];
        if (!simple.EndsWith("Model", StringComparison.Ordinal))
        {
            yield break;
        }

        var stem = simple[..^"Model".Length];
        if (stem.Length == 0)
        {
            yield break;
        }

        const string abstractPrefix = "Abstract";
        if (stem.StartsWith(abstractPrefix, StringComparison.Ordinal) &&
            stem.Length > abstractPrefix.Length)
        {
            yield return $"{pkg}.{stem[abstractPrefix.Length..]}Model";
        }

        const string likeSuffix = "Like";
        if (stem.EndsWith(likeSuffix, StringComparison.Ordinal) && stem.Length > likeSuffix.Length)
        {
            yield return $"{pkg}.{stem[..^likeSuffix.Length]}Model";
        }

        yield return $"{pkg}.Adult{stem}Model";
        yield return $"{pkg}.Baby{stem}Model";
        yield return $"{pkg}.Cold{stem}Model";
        yield return $"{pkg}.Warm{stem}Model";
    }
}
