using System.Text.Json;
using System.Text.Json.Nodes;

namespace AutoPBR.Core.Preview;

/// <summary>Metrics for geometry IR shards (lift quality baseline / regression).</summary>
public static partial class GeometryIrLiftQualityReport
{
    private static List<string> ReadExtractionNotes(JsonElement shardRoot)
    {
        var notes = new List<string>();
        if (!shardRoot.TryGetProperty("extractionNotes", out var notesEl) ||
            notesEl.ValueKind != JsonValueKind.Array)
        {
            return notes;
        }

        foreach (var note in notesEl.EnumerateArray())
        {
            var text = note.GetString();
            if (!string.IsNullOrEmpty(text))
            {
                notes.Add(text);
            }
        }

        return notes;
    }

    private static bool HasAddChildBindingExtractionNote(IReadOnlyList<string> extractionNotes) =>
        extractionNotes.Any(IndicatesAddChildHierarchyBindingNote);

    private static bool HasExtractionMissingAddChildNote(IReadOnlyList<string> extractionNotes) =>
        extractionNotes.Any(IndicatesAddChildExtractionBindingGap);

    /// <summary>
    /// Lift parser logs "No PartDefinition … addChild binding lines found" when javap uses flat
    /// addOrReplaceChild only — not a hierarchy lift failure.
    /// </summary>
    private static bool IndicatesAddChildHierarchyBindingNote(string note) =>
        note.Contains("addChild binding", StringComparison.OrdinalIgnoreCase) &&
        !note.Contains("No PartDefinition", StringComparison.OrdinalIgnoreCase) &&
        !note.Contains("no addChild binding lines found", StringComparison.OrdinalIgnoreCase);

    private static bool IndicatesAddChildExtractionBindingGap(string note) =>
        note.Contains("missing addChild", StringComparison.OrdinalIgnoreCase) ||
        IndicatesAddChildHierarchyBindingNote(note);

    private static bool LegsStillRootSiblings(JsonElement shardRoot)
    {
        if (!shardRoot.TryGetProperty("roots", out var roots) || roots.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var root in roots.EnumerateArray())
        {
            if (!root.TryGetProperty("children", out var children) || children.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var rootChildIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var ch in children.EnumerateArray())
            {
                if (ch.TryGetProperty("id", out var idEl))
                {
                    var id = idEl.GetString();
                    if (!string.IsNullOrEmpty(id))
                    {
                        rootChildIds.Add(id);
                    }
                }
            }

            if (!rootChildIds.Contains("body"))
            {
                continue;
            }

            if (BodyLegPartIds.Any(rootChildIds.Contains))
            {
                return true;
            }
        }

        return false;
    }

    private static (bool Match, string? Message) EvaluateReferenceHierarchyMatch(
        JsonElement shardRoot,
        int flatNested,
        bool legsAtRoot,
        bool bindingNote,
        bool? referenceLegsAtRoot)
    {
        if (referenceLegsAtRoot == true && LegsNestedUnderBody(shardRoot))
        {
            return (false, "IR nests legs under body but reference_java uses vanilla flat root siblings");
        }

        if (flatNested > 0 && legsAtRoot)
        {
            if (referenceLegsAtRoot == false)
            {
                if (bindingNote)
                {
                    return (false, "extractionNotes mention addChild binding; hierarchy not lifted");
                }

                // Composed-flat: IR mirrors flat Java factory; reference_java nests legs (world pose via topology align).
                return (true, null);
            }

            if (referenceLegsAtRoot != true)
            {
                return (false,
                    $"suspectedFlatNestedPartCount={flatNested} with body/legs still root siblings (reference unavailable)");
            }

            JsonObject? shardDoc = JsonNode.Parse(shardRoot.GetRawText()) as JsonObject;
            if (!TryGetFirstRootChildren(shardRoot, out var irRootKids) ||
                !GeometryIrPartTreeRepair.UsesVanillaFlatQuadrupedLegBake(irRootKids, shardDoc))
            {
                return (false,
                    $"suspectedFlatNestedPartCount={flatNested} with body/legs still root siblings (not vanilla flat quadruped layout)");
            }

            return (true, null);
        }

        if (bindingNote)
        {
            return (false, "extractionNotes mention addChild binding; hierarchy not lifted");
        }

        if (flatNested > 0)
        {
            return (false, $"suspectedFlatNestedPartCount={flatNested}");
        }

        return (true, null);
    }

    private static bool TryGetFirstRootChildren(JsonElement shardRoot, out JsonArray rootChildren)
    {
        rootChildren = [];
        if (!shardRoot.TryGetProperty("roots", out var roots) || roots.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var root in roots.EnumerateArray())
        {
            if (!root.TryGetProperty("children", out var children) || children.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var node = JsonNode.Parse(children.GetRawText());
            if (node is JsonArray arr)
            {
                rootChildren = arr;
                return true;
            }
        }

        return false;
    }

    private static bool LegsNestedUnderBody(JsonElement shardRoot)
    {
        if (!shardRoot.TryGetProperty("roots", out var roots) || roots.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var root in roots.EnumerateArray())
        {
            if (!TryFindPartById(root, "body", out var body))
            {
                continue;
            }

            if (!body.TryGetProperty("children", out var children) || children.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var legId in BodyLegPartIds)
            {
                foreach (var ch in children.EnumerateArray())
                {
                    if (ch.TryGetProperty("id", out var idEl) &&
                        string.Equals(idEl.GetString(), legId, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private static bool TryFindPartById(JsonElement node, string id, out JsonElement found)
    {
        if (node.TryGetProperty("id", out var idEl) &&
            string.Equals(idEl.GetString(), id, StringComparison.Ordinal))
        {
            found = node;
            return true;
        }

        if (node.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var ch in children.EnumerateArray())
            {
                if (TryFindPartById(ch, id, out found))
                {
                    return true;
                }
            }
        }

        found = default;
        return false;
    }

    private static int CountSuspectedFlatNestedAtRoot(JsonElement rootChildren)
    {
        var rootIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var ch in rootChildren.EnumerateArray())
        {
            if (ch.TryGetProperty("id", out var idEl))
            {
                var id = idEl.GetString();
                if (!string.IsNullOrEmpty(id))
                {
                    rootIds.Add(id);
                }
            }
        }

        var n = 0;
        foreach (var (parent, child) in KnownNestedPairs)
        {
            if (rootIds.Contains(child) && rootIds.Contains(parent))
            {
                n++;
            }
        }

        return n;
    }
}
