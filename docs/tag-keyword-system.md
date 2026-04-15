# Tag / keyword system

> Optional future: diffuse→metal inference without tags is described in [Metal ML spec](metal-ml-spec.md).

> **Semantic ML + Weighted/Unweighted flags**: heuristic-first material matching and the `weighted` / `unweighted` flag tags are documented in [semantic-material-weighted-unweighted.md](semantic-material-weighted-unweighted.md).

Material tags classify textures in the **Resource Explorer** (and feed future tuning). They are **informational only** right now: **tag rules do not change normals, height, or specular output.** Per-tag conversion overrides (e.g. invert height for brick) may be reintroduced later.

## Goals

- **Keywords**: Match **file name** and path below namespace (case-insensitive substring) against each rule’s **keywords**. Used whenever MiniLM is **off**, and used as the **first** pass when MiniLM is **on** (heuristic hit skips embedding for that texture).
- **MiniLM**: When **on** (and the matcher loads), run **only if** no **material** keyword heuristic matched; compares a **controlled query string** to **display name** + **semantic hint** phrases. Low-confidence results may still yield **`unknown`** for refinement.
- **Manual**: Per-file **add/remove** tag ids in Explore; persisted per pack.
- **Visible**: **Glyph** per tag on each file row (**tooltip** = display name); **legend** above the tree (glyph + tooltip with keyword/ML summary).

---

## Model bundle (MiniLM)

Ship the Optimum export of `sentence-transformers/all-MiniLM-L6-v2` next to the app:

- **Graph**: `Data/all-MiniLM-L6-v2-onnx/model.onnx`
- **Tokenizer**: `vocab.txt` (and companion JSON in the same folder)

Enable semantic suggestions in **Tune → Semantic tag suggestions (MiniLM)**. If `model.onnx` is missing or load fails while MiniLM is enabled, there are **no** ML auto-tags (keywords are not used as a fallback until you turn MiniLM off).

---

## Data model (Core)

### `TagRule` (`AutoPBR.Core/Models/TagRule.cs`)

| Field | Purpose |
|--------|--------|
| **Id** | Stable id, e.g. `brick`. |
| **DisplayName** | UI and tooltips, e.g. `Brick`. |
| **Keywords** | Auto-match if **Name** or **RelativeKey** contains any keyword (case-insensitive). |
| **SemanticHints** | English phrases used **only** for MiniLM prototype embeddings (optional). |

There is **no** `TextureOverrides` on tag rules in the current design.

### `CustomTagRuleEntry` (`AutoPBR.Core/Models/CustomTagRuleEntry.cs`)

User rules in settings / CLI JSON: **Id**, **DisplayName**, **Keywords** (comma-separated), **SemanticHints** (comma-separated), **Enabled**, plus list order for merge/UX. `ToTagRule()` builds a `TagRule`.

### Built-in presets (`TagRulePresets.Default`)

**brick**, **wood**, **metal**, **organic** (id `plant`) — each has keywords and default semantic hints for ML.

---

## Matching logic

### Keywords

`TagRuleApplicator.GetMatchingTagIds(name, relativeKey, rules)` — same rule list as Explore (built-ins + enabled custom rules). Keywords are matched with case-insensitive **substring** search on **file name** plus the relative path **after** the first segment (the pack namespace / mod id under `assets/<namespace>/`). The namespace segment itself is excluded so folder names like `mythicmetals` do not affect matching; subfolders and filenames still do (e.g. `metal` in `metal_ingot.png` matches).

### MiniLM (`AutoPBR.Core/Embeddings/`)

- **`MiniLmEmbeddingEngine`**: Tokenizes with **`BertTokenizer`** (`vocab.txt`), runs the shipped ONNX graph (`input_ids` / `attention_mask` → **`sentence_embedding`**), then **L2-normalizes** the 384-d vector for cosine similarity.
- **`MaterialTagSemanticQuery`**: Builds the embedded query so **mod namespaces** (e.g. `mythicmetals`) do not bias the model. For normal textures: **file title** plus optional **coarse type tokens** only — `block`, `item`, `entity`, `particle`, or `armor` when that path segment appears (derived from the first folder under `assets/<namespace>/textures/`, not from arbitrary mod subfolders). If the relative key contains **`optifine`**, the query uses **title + full path segments** (including namespace) so OptiFine layouts (CTM, plants, etc.) keep disambiguating context.
- **`MaterialTagSemanticMatcher`**: Embeds that query; for each rule, cosine similarity vs **DisplayName** and each **SemanticHint**; keep the **best** score per rule; return up to **N** rules with score ≥ **min similarity** (from settings). Invoked only when **material** keyword heuristics did not match (see [semantic-material-weighted-unweighted.md](semantic-material-weighted-unweighted.md)).

**Explore — auto material tags**: If **semantic MiniLM** is **off** (or matcher missing), material auto tags = **keywords only**. If MiniLM is **on**, **keyword material rules run first**; only when none match does Explore use **MiniLM** for materials. **Path-derived flags** (block/item/ore/…) are merged separately. **`weighted` / `unweighted`** flags summarize heuristic vs ML (see linked doc). Then **manual** add/remove applies: effective = (auto minus removed) ∪ added.

### Conversion / scan

`TextureScanner` **does not** apply tag overrides to `TextureWorkItem.Overrides`. Tags are for Explore (and future use). `AutoPbrOptions.TagRules` and `ManualTagOverrides` remain available for callers that need the same definitions / manual state; they do not drive PBR outputs until overrides are wired again.

---

## Explorer (App)

- **`ExploreTreeController`**: `ComputeEffectiveTags` resolves storage key; **material** ids from `MaterialTagSemanticResolution.ResolveMaterialTags` (heuristic-first, then ML); **flags** from `FlagTagResolver` plus **`AppendWeightedUnweightedFlags`**; then merges **removed** / **added** sets, returns ordered `(Id, DisplayName, Kind)` following the effective rule list.
- **`SetTagRulesProvider`**: built-in + custom rules.
- **`SetMaterialTagSemanticOptionsProvider`**: optional `MaterialTagSemanticOptions` (enabled, min similarity, max ML tags, matcher instance).
- **Persistence**: `TagOverridesPersistence` — manual add/remove per texture key, keyed by pack path.

### UI

- **Row tags**: `DisplayTagItem` with **IconGlyph** (`MaterialTagGlyphs`) and **DisplayName** on **ToolTip.Tip**.
- **Legend**: glyphs above the tree with **TooltipText** = display name + keyword/ML summary.
- **Context menu**: Apply / don’t apply tag (localized).
- **Filter**: “Show tag” dropdown (All | each rule).
- **Tune tab**: Tag rules editor (id, display name, keywords, semantic hints, enabled, reorder, import/export JSON). Separate card for **semantic MiniLM** (enable, min similarity, max ML tags).

---

## Settings

- **`UserSettings.CustomTagRules`**: list of `CustomTagRuleEntry`.
- **`UseSemanticMaterialTags`**, **`MaterialTagMinSimilarity`**, **`MaterialTagMaxCount`**: persisted; used when building `MaterialTagSemanticOptions` for Explore.

---

## CLI

`--tag-rules path.json` merges JSON `CustomTagRuleEntry` entries **after** `TagRulePresets.Default` for that run. Schema matches Core: keywords + semantic hints (no override fields).

---

## Path keys

Explorer archive paths and scanner `RelativeKey` are normalized to a common storage key for manual overrides (see `ExploreTreeController` / `ResolveTagStorageKey` and related helpers).

---

## Summary

| Area | Behavior |
|------|----------|
| **TagRule** | Id, DisplayName, Keywords, SemanticHints — **no conversion overrides**. |
| **Keywords** | Substring match on name + path below namespace; used alone when ML off, and as **heuristic-first** material pass when ML on. |
| **MiniLM** | When **on** (and matcher loads): runs for **material** tags **only if** keyword heuristics matched nothing; optional **Weighted** flag when ML ran ([details](semantic-material-weighted-unweighted.md)). |
| **Effective tags** | Auto (material: heuristic → else ML; flags: path + weighted/unweighted), then manual add/remove on top. |
| **Explore** | Glyphs, tooltips, legend, filter, context menu, persistence. |
| **Conversion** | Tags are **not** applied to `TextureOverrides` in the current implementation. |

Future work: reattach **TextureOverrides** (or a dedicated material-tuning layer) to tag ids once product rules for merge order and UI are defined.
