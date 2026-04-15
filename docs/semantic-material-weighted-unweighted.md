# Semantic material tags: Weighted vs Unweighted

This document describes how **material** tags interact with **MiniLM** (sentence embeddings) in Resource Explorer, and what the **Weighted** / **Unweighted** **flag** tags mean.

For the broader tag model (keywords, rules, UI), see [tag-keyword-system.md](tag-keyword-system.md).

---

## Purpose

- **Save ML work**: If a texture title/path already matches **material** rules by **keyword heuristic** (same substring rules as keyword mode), we **do not** run the MiniLM `matcher.Match` pipeline for that file.
- **Surface the decision in the UI**: Two built-in **flags** (`weighted`, `unweighted`) show whether material tags for that row came from **heuristics only** or from the **full embedding + similarity** path.

These flags are **informational** (like other Explore tags): they do not change normals, height, or specular output.

---

## Flag tags

| Id | Display name | Meaning |
|----|----------------|--------|
| `weighted` | Weighted | MiniLM **did** run for material resolution on this texture (no heuristic material hit, or ML was required to refine). |
| `unweighted` | Unweighted | Material tags came from **keyword/heuristic** matching only, **or** semantic ML is off, **or** the UI is still in a **deferred** first-pass preview (see below). |

Rules live in `TagRulePresets.DefaultFlags` with **empty keywords**; the app assigns exactly one of these flags in code—users do not match them via path keywords.

Icons and emoji fallbacks are defined in `MaterialTagGlyphs` (`flag_weighted.png`, `flag_unweighted.png`).

---

## Resolution order (`MaterialTagSemanticResolution`)

Implementation: `AutoPBR.Core/MaterialTagSemanticResolution.cs`.

1. **Semantic ML unavailable or deferred**
   - Conditions: MiniLM **disabled**, matcher **not loaded**, **`deferSemanticMl`** true (first paint before background semantic refresh), or equivalent.
   - Material ids: **`TagRulePresets.GetMatchingMaterialTagIds`** (keyword substring on name + path below namespace), then **`MaterialTagMlPostProcessor.Apply`**.
   - **`usedSemanticMl`** = false (no embedding match).

2. **Semantic ML on and not deferred**
   - **Heuristic pass**: `TagRuleApplicator.GetMatchingTagIds(..., TagRuleKind.Material)` — same keyword list as material rules.
   - If **any** material rule matches → use those ids → post-processor → **`usedSemanticMl`** = false. MiniLM **`Match`** is **skipped**.
   - If **no** heuristic match → run **`MaterialTagSemanticMatcher.Match`** (dictionary evidence optional) → post-processor → **`usedSemanticMl`** = true.

The post-processor can still adjust or cap tags (e.g. low-confidence **unknown**); flags reflect whether **MiniLM similarity** ran, not every post-processing branch.

---

## Weighted / Unweighted on the flag list

`MaterialTagSemanticResolution.AppendWeightedUnweightedFlags` appends **exactly one** of `weighted` / `unweighted` after path-derived flags (`FlagTagResolver.Resolve`). Any previous weighted/unweighted entries are stripped first.

| Situation | Flag |
|-----------|------|
| Semantic ML off, or no matcher | `unweighted` |
| `deferSemanticMl` true (preview) | `unweighted` |
| Semantic on, not deferred, **`usedSemanticMl`** true | `weighted` |
| Semantic on, not deferred, **`usedSemanticMl`** false (heuristic material match) | `unweighted` |

Explore combines these with manual tag add/remove like other auto tags (`ExploreTreeController.ComputeEffectiveTags`).

---

## Other callers

- **`TextureScanner.GetEffectiveMaterialTagIds`** uses `ResolveMaterialTags` for plant/brick-style checks but **does not** append Weighted/Unweighted flags—only material ids matter there.

---

## Related code

| Piece | Role |
|--------|------|
| `FlagTagResolver.WeightedId` / `UnweightedId` | Stable ids |
| `MaterialTagSemanticResolution.ResolveMaterialTags` | Heuristic-first vs ML material ids + `usedSemanticMl` |
| `MaterialTagSemanticResolution.AppendWeightedUnweightedFlags` | Sets the flag tag on Explore’s effective list |
| `MaterialTagSemanticMatcher.Match` | Full MiniLM path when heuristics return nothing |
