# Tag / Keyword System – Plan

This document plans a **Tag/Keyword** system that catalogs texture types more specifically than the general filter (blocks, items, entity, particles) and drives **conversion overrides** automatically from keywords, with optional **manual user changes** and a **legend** in the file explorer.

## Goals

- **Automatic**: During scan/conversion, match texture **name** and **path** against **keywords** and apply predefined **conversion overrides** (e.g. invert height, invert specular for "brick").
- **Manual**: User can add/remove tags for specific files in the explorer so that conversion respects their choices.
- **Visible**: In the **Resource Explorer** (file tree), show a **legend of tags** per file (right-aligned on each row) so users see which overrides apply.
- **Extensible**: Tags represent specific tuning (e.g. "brick" → invert specular + invert height so light grout doesn’t “pop out”).

---

## 1. Data Model

### 1.1 Tag definition (Core)

A tag is a named rule: one or more **keywords** and a set of **overrides** to apply when the texture matches.

- **Id** (string): Stable id, e.g. `"brick"`.
- **DisplayName** (string): Shown in UI and legend, e.g. `"Brick"`.
- **Keywords** (list of strings): Match if texture **Name** or **RelativeKey** contains any keyword (case-insensitive). Example: `["brick"]` matches `stone_brick`, `bricks.png`, path `block/brick`).
- **Overrides** (TextureOverrides or a small DTO): Only the properties this tag sets (e.g. `InvertHeight = true`, `InvertSpecular = true`).

Suggested type in Core:

```csharp
// AutoPBR.Core/Models/TagRule.cs
public sealed class TagRule
{
    public required string Id { get; init; }
    public required string DisplayName { get; init; }
    public IReadOnlyList<string> Keywords { get; init; } = Array.Empty<string>();
    public TextureOverrides Overrides { get; init; } = new();
}
```

- **TextureOverrides** already has `InvertHeight`. Add **InvertSpecular** (bool) and use it in `SpecularGenerator` to invert the smoothness (R) channel so that dark areas become smoother and light areas rougher (reducing “popping” of light grout on bricks).

### 1.2 Per-path manual state (App / Explore)

For the explorer we need to store **manual tag changes** per path:

- **RemovedTagIds**: User chose “don’t apply this tag” for this path (e.g. remove "brick" from one file).
- **AddedTagIds**: User chose “apply this tag” even though keyword didn’t match (optional, for power users).

So for a given file path:

- **Auto tags** = tag ids whose Keywords match the file’s Name or RelativeKey.
- **Effective tags** = (Auto tags \ RemovedTagIds) ∪ AddedTagIds.
- **Conversion**: Apply overrides from all Effective tags (merged in a defined order, e.g. by tag id).

Storage: same host that already keeps include/exclude overrides, e.g. `IArchiveNodeHost` / `ExploreTreeController`:

- `GetTagOverrides(path)` → (added: string[], removed: string[])
- `SetTagOverrides(path, added, removed)` or separate add/remove per tag.

Use a **path key** that matches what conversion uses (e.g. archive path like `assets/minecraft/textures/block/stone_brick.png` or the same key used for include/exclude).

---

## 2. Keyword matching and application (Core)

- **Where**: Either inside `TextureScanner.ScanTextures` (after creating each `TextureWorkItem`) or in a dedicated step that takes `List<TextureWorkItem>` + `TagRule[]` + optional per-path manual overrides and mutates each item’s `Overrides`.
- **Match**: For each work item, for each `TagRule`, if `Name` or `RelativeKey` contains any of `Keywords` (ordinal, case-insensitive), consider that tag “auto-applied.”
- **Merge**: For each tag that applies (respecting manual remove/add when that data is passed in), merge that tag’s `Overrides` into `item.Overrides`: set only properties that the tag specifies (e.g. only `InvertHeight` and `InvertSpecular`), leave others unchanged so global options still apply.
- **Manual overrides**: When conversion is run “from explorer,” pass per-path added/removed tag ids (and the same tag rule set). When building work items, compute effective tags per path and merge overrides as above. When conversion is run without explorer (e.g. Convert only), only auto-tag rules apply (no per-path add/remove).

---

## 3. Conversion pipeline changes

- **TextureOverrides**: Add `InvertSpecular { get; set; }` (default false).
- **SpecularGenerator**: When `t.Overrides.InvertSpecular` is true, after computing the R (smoothness) value per pixel, set `r = 255 - r` (or equivalent) so smoothness is inverted.
- **NormalHeightGenerator**: Already uses `t.Overrides.InvertHeight`; no change.
- **TextureScanner** (or new helper): Accept `IReadOnlyList<TagRule>?` and optional `Func<string, (IReadOnlyList<string>? added, IReadOnlyList<string>? removed)>? getManualTagOverrides`. After creating each `TextureWorkItem`, compute effective tags for that path, merge overrides from those tags into `item.Overrides`.
- **AutoPbrOptions**: Add `TagRules` (and optionally a way to pass per-path manual tag overrides when conversion is triggered from the explorer so the same rules + manual edits are used).

---

## 4. Explorer UI: legend and right-aligned tags

- **Per row (file node)**: In the tree row for a **file** (not folder), show the list of **effective tags** for that path, **right-aligned** (e.g. pills/badges with tag display names). So the row layout is: `[☐] [Name] [Set as Preview] …… [tag1] [tag2]`.
- **Legend**: Expose a small “legend” of what each tag does (e.g. “Brick: invert height, invert specular”). It can sit above or below the tree, or in a tooltip/popover. Content can be generated from `TagRule.DisplayName` + a short description of the overrides (e.g. from the tag’s Overrides or a fixed string per tag id).
- **ArchiveNode**: For file nodes, add a property that the host can fill: e.g. `EffectiveTagIds` or `DisplayTags` (list of tag ids or of `{ Id, DisplayName }`). The host (controller) computes this from: auto tags (keyword match) + manual add/remove for that path. So `ArchiveNode` needs a way to get “tags for this path” from the host (e.g. host method `GetEffectiveTags(path, name, relativeKey)` returning the list to display).
- **Manual edit**: Provide a way for the user to **add/remove** a tag on the current file (e.g. context menu “Don’t apply tag ‘Brick’ here” or “Apply tag ‘Brick’ here”). That updates the host’s per-path added/removed sets and refreshes the displayed tags.

---

## 5. Default tag rules

- **Brick** (id: `"brick"`):
  - Keywords: `["brick"]`
  - Overrides: `InvertHeight = true`, `InvertSpecular = true`
  - Rationale: Bricks often have light grout and darker flat regions; inverting height and specular prevents the grout from appearing to “pop out.”

Additional rules (e.g. wood, metal, foliage) can be added later using the same mechanism.

---

## 6. Implementation order (suggested)

1. **Core – overrides**
   - Add `InvertSpecular` to `TextureOverrides`.
   - Implement invert-smoothness in `SpecularGenerator` when `InvertSpecular` is true.

2. **Core – tag rules**
   - Add `TagRule` (and optionally a small `TagRuleSet` or default list).
   - Add a function that, given a `TextureWorkItem` and `TagRule[]`, returns which tag ids match (by keyword).
   - Add a function that merges multiple tags’ overrides into one `TextureOverrides` (only set non-default fields from each tag).
   - Integrate into scan/conversion: e.g. `TextureScanner.ScanTextures` accepts optional tag rules and optional manual overrides; after creating each work item, compute effective tags and merge overrides into `item.Overrides`.

3. **Options**
   - Add `TagRules` to `AutoPbrOptions` (default = built-in list including "brick").
   - When building options for conversion from the app, pass tag rules and, if coming from explorer, per-path added/removed tag ids.

4. **Explore – storage**
   - In `ExploreTreeController` (or `IArchiveNodeHost`), add storage for per-path tag add/remove: e.g. `Dictionary<string, (HashSet<string> added, HashSet<string> removed)>`.
   - Add `GetEffectiveTags(archivePath, name, relativeKey)` that returns tag ids to display and to use for conversion for that path.

5. **Explore – UI**
   - In the tree `DataTemplate` for file nodes, add a right-aligned control (e.g. `ItemsControl` or stack of pills) bound to the node’s effective tags (from host).
   - Add a small legend (list of tag display names + short description) above/below the tree.
   - Add context menu or small menu to add/remove a tag for the selected file; wire to host’s set methods and refresh.

6. **Conversion from explorer**
   - When user runs Convert and the current pack was scanned/explored, build the ignore set from existing overrides and also build per-path tag overrides (added/removed) from the controller and pass them into options (or into the step that applies tag overrides to work items). Ensure work items are built with the same path key format so manual overrides apply to the correct entries.

**Phase 3 (persistence, clear, localization)**

7. **Persistence**
   - Save manual tag overrides (added/removed per texture key) keyed by pack path under `AppData/AutoPBR/tag_overrides/{packKey}.json`. Load when setting scan data for that pack; save when user adds/removes a tag.

8. **Clear tag overrides**
   - "Clear tag overrides" button in Explore tab: clears all manual add/remove for the current pack, persists, and refreshes displayed tags on all loaded nodes.

9. **Localization**
   - Resource strings for: Clear tag overrides (button + tooltip), context menu ("Don't apply '{0}' here" / "Apply '{0}' here"). Host exposes `GetTagMenuHeader(displayName, isApplied)` for localized menu text.

**Phase 4 (custom tag rules + UI)**

10. **Custom tag rules**
    - User-definable rules: Id, DisplayName, Keywords (comma-separated), InvertHeight, InvertSpecular. Stored in `UserSettings.CustomTagRules` and persisted by `UserSettingsSynchronizer`.
    - **Effective tag rules** = default presets (e.g. brick, wood) + custom rules. Used for scan, explore legend, and conversion when building options from the app.
    - Explore host gets rules via `SetTagRulesProvider(Func<IReadOnlyList<TagRule>>)` so the tree and legend use the same effective rules.

11. **Tag rules UI (Tune tab)**
    - "Tag rules" card: list of custom rules (Id, Display name, Keywords, Invert height / Invert specular checkboxes, Remove). Buttons: "Add custom rule", "Refresh" (updates explore tree and legend).
    - Labels and buttons use localized strings (TagRuleId, TagRuleDisplayName, TagRuleKeywords, RemoveTagRule, InvertHeight, InvertSpecular, AddCustomTagRule, RefreshTagRules).

12. **Default presets**
    - **Wood** (id: `"wood"`): Keywords e.g. wood, plank, log, bark; Overrides: InvertHeight only (optional addition alongside brick).

**Phase 5 (richer rules, presets, export/CLI)**

13. **Built-in presets (extended)**
    - **Metal** (`metal`): keywords metal, ore, gold, iron, copper, ingot, chain, netherite, nugget → **NormalIntensity ×0.85** (softer normals on metallic-looking blocks).
    - **Foliage** (`foliage`): leaves, grass, vine, fern, flower, sapling, kelp, lily, mushroom → **HeightIntensity ×0.07** (subtler height on organic textures).

14. **Custom rule fields (Core `CustomTagRuleEntry`)**
    - Optional **NormalIntensity** / **HeightIntensity** (float, replaces global for matching textures).
    - **InvertNormalRed** / **InvertNormalGreen** (normal X/Y flip).
    - Optional **FastSpecular** (`true` / `false` / omit for global) in JSON.

15. **Legend text**
    - Brick/wood/metal/foliage use short curated descriptions; other rules use **TagOverrideDescription.Summarize** from active overrides.

16. **Export / import (Tune tab)**
    - JSON array of custom rules only (not built-ins). Same schema as settings + optional numeric/specular fields.

17. **CLI `--tag-rules path.json`**
    - Appends JSON rules after **TagRulePresets.Default** for that run (built-ins + file entries).

**Phase 6 (per-rule enable, order, filter by tag)**

18. **Enable/disable per custom rule**
    - **CustomTagRuleEntry.Enabled** (default true). When false, the rule is excluded from **GetEffectiveTagRules()** (no merge, not in legend). Persisted in settings; use "Refresh" to update legend/tree after toggling.

19. **Custom rule order**
    - **Move up** / **Move down** buttons per custom rule row. Order is persisted (list order in settings); merge order = built-ins then custom in list order.

20. **Explore filter by tag**
    - "Show tag:" dropdown in Explore (All | Brick | Wood | Metal | Foliage | custom…). When a tag is selected, tree shows only file nodes that have that tag (and ancestor folders). Combined with the existing path/name text filter.

---

## 7. Path key format

- **Scanner** produces `TextureWorkItem` with `FullPath` (extracted path) and `RelativeKey` (e.g. `\minecraft\block\stone_brick`).
- **Explore tree** uses archive paths like `assets/minecraft/textures/block/stone_brick.png`.
- Use a single **normalized key** for both tag lookup and manual overrides, e.g. derive from `RelativeKey` or from archive path by stripping `assets/`, `textures/`, and extension so that `assets/minecraft/textures/block/stone_brick.png` and the work item’s `RelativeKey` both map to the same key (e.g. `minecraft/block/stone_brick` or `\minecraft\block\stone_brick`). `ExploreTreeController.ArchivePathToTextureKey` already converts archive path to a key; use that (or its inverse) so explorer path and work item key align.

---

## 8. Summary

| Area            | Change |
|-----------------|--------|
| **TextureOverrides** | Add `InvertSpecular`. |
| **SpecularGenerator** | If `InvertSpecular`, invert smoothness (R) output. |
| **TagRule** (new) | Id, DisplayName, Keywords, Overrides. |
| **Tag application** | After creating work items, match keywords on Name/RelativeKey, merge tag overrides; support per-path add/remove when coming from explorer. |
| **Explore storage** | Per-path added/removed tag ids in controller. |
| **Explore UI**      | Right-aligned tags per file row; legend; context menu to add/remove tag. |
| **Conversion**     | Options carry tag rules and optional per-path manual tag overrides; work items get overrides applied during scan/build. |

This gives a clear path from “brick” (and future keywords) to automatic and user-adjustable conversion behavior, with a visible legend in the file explorer.
