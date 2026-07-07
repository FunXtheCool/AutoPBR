#!/usr/bin/env python3
"""Append missing localization keys without rewriting existing .resx structure."""

from __future__ import annotations

import html
import re
import time
import xml.etree.ElementTree as ET
from pathlib import Path

from deep_translator import GoogleTranslator

LANG_DIR = Path(__file__).resolve().parents[1] / "src" / "AutoPBR.App" / "Lang"
EN_FILE = LANG_DIR / "Resources.resx"
DESIGNER_FILE = LANG_DIR / "Resources.Designer.cs"
LOCALIZED_FILE = LANG_DIR / "LocalizedStrings.cs"

CULTURES: dict[str, str] = {
    "de": "de",
    "es": "es",
    "fr": "fr",
    "pt": "pt",
    "ru": "ru",
    "ja": "ja",
    "zh-Hans": "zh-CN",
    "hi": "hi",
    "ar": "ar",
}

NEW_ENGLISH: dict[str, str] = {
    "AiTaggingSection": "AI Tagging",
    "NormalsAndHeightSection": "Normals and Height",
    "NormalsSection": "Normals",
    "SpecularSection": "Specular",
    "MetalnessSection": "Metalness",
    "SpecularPorositySection": "Porosity",
    "AiDrivenPbrSection": "AI Driven PBR",
    "AiNormalsSection": "AI Normals",
    "AiSpecularSection": "AI Specular",
    "Preview3DCameraExpanderSection": "3D Preview Camera",
    "WindowMinimize": "Minimize",
    "WindowMaximize": "Maximize",
    "WindowClose": "Close",
    "DebugMlMatch": "Debug ML Match…",
    "ExploreManualOverrideTooltip": "Checked = include, Unchecked = exclude, Indeterminate = use other rules",
    "SetAsPreview": "Set as Preview",
    "TagRuleIdWatermark": "e.g. mytag",
    "TagRuleDisplayNameWatermark": "e.g. My Tag",
    "EditAiModels": "Edit AI Models",
    "EditAiModelsWarning": "Warning: Do not edit these model paths unless you know what you are doing.",
    "MlResolution16x16": "16×16",
    "MlResolution32x32": "32×32",
    "MlResolution64x64": "64×64",
    "MlResolution128x128": "128×128",
    "MlResolution256x256": "256×256",
    "UvDebugButton": "UV Debug",
    "EntityDebugButton": "Entity Debug",
    "PreviewPoseLabel": "Pose",
    "PreviewSizeLabel": "Size",
    "PreviewContextTypeLabel": "Context Type",
    "SelectResourcePackTitle": "Select resource pack (.zip or .jar)",
    "SelectOutputFolderTitle": "Select output folder",
    "SelectMinecraftAssetsTitle": "Select Minecraft version or assets folder",
    "SemanticDebugWindowTitle": "MiniLM Semantic Match Debug",
    "Preview3DCameraResetKeyFormat": "{0} key",
    "CustomTagDefaultDisplayName": "Custom Tag",
    "CustomRuleDefaultDisplayName": "Custom Rule",
    "Status_GpuPreviewStarting": "Starting GPU preview…",
    "Status_GpuPreviewPreparingShaderSources": "Preparing shader sources…",
    "Status_GpuPreviewPreparing": "Preparing GPU preview…",
    "Status_GpuPreviewClearingShaderCache": "Clearing shader cache…",
    "Status_GpuPreviewCompilingMainShaders": "Compiling main preview shaders…",
    "Status_GpuPreviewCompilingShadowShaders": "Compiling shadow shaders…",
    "Status_GpuPreviewCreatingShadowMaps": "Creating shadow maps…",
    "Status_GpuPreviewUploadingMeshes": "Uploading preview meshes…",
    "Status_GpuPreviewInitializingLineOverlay": "Initializing line overlay…",
    "Status_GpuPreviewInitializingSkyDome": "Initializing sky dome…",
    "Status_GpuPreviewInitializingAtmosphere": "Initializing atmosphere…",
    "Status_GpuPreviewFinalizing": "Finalizing preview…",
    "Status_GpuPreviewLoadingGodRays": "Loading god rays…",
    "Status_GpuPreviewLoadingClouds": "Loading volumetric clouds…",
    "Status_GpuPreviewLoadingTaa": "Loading preview TAA…",
    "Status_GpuPreviewReady": "Ready",
    "Status_GpuPreviewPreviewReady": "Preview ready",
    "Status_GpuPreviewCoreReady": "Core preview ready",
    "UvDebugTitle": "UV Debug",
    "UvDebugGlobalControls": "Global UV Debug Controls",
    "UvDebugGlobalControlsDescription": "These override production UV bake policy when toggled. Entity cuboids use baked-in Java parity by default.",
    "UvDebugResetOverrides": "Reset overrides (production defaults)",
    "UvDebugFlipU": "Flip U (swap u0/u1)",
    "UvDebugFlipV": "Flip V (swap v0/v1)",
    "UvDebugOffsetU": "U offset (px)",
    "UvDebugOffsetV": "V offset (px)",
    "UvDebugFaceSemanticRouting": "Face semantic routing (post-transform face semantics)",
    "UvDebugSwapNorthSouth": "Swap face routing North / South",
    "UvDebugSwapEastWest": "Swap face routing East / West",
    "UvDebugSwapUpDown": "Swap face routing Up / Down",
    "UvDebugBakerConversion": "Baker UV conversion",
    "UvDebugPreserveDirectionalBounds": "Preserve directional UV bounds (disable min/max normalization)",
    "UvDebugUseBottomLeftUvOrigin": "Use bottom-left UV origin conversion (GL style)",
    "UvDebugFaceCornerOrdering": "Face corner ordering / winding",
    "UvDebugCornerOrderDefault": "Default",
    "UvDebugCornerOrderRotate90": "Rotate 90",
    "UvDebugCornerOrderRotate180": "Rotate 180",
    "UvDebugCornerOrderRotate270": "Rotate 270",
    "UvDebugCornerOrderReverseWinding": "Reverse winding",
    "UvDebugGlobalFaceRotation": "Global face rotation",
    "UvDebugRotation0": "0°",
    "UvDebugRotation90": "90°",
    "UvDebugRotation180": "180°",
    "UvDebugRotation270": "270°",
    "UvDebugLiveChangesHint": "Changes apply live and trigger preview refresh. Use these to isolate texOffs vs baker conversion vs corner-order issues.",
    "EntityPreviewDebugTitle": "Entity Preview Debug",
    "EntityPreviewDebugBabyFixesSection": "Baby preview live fixes",
    "EntityPreviewDebugBabyFixesDescription": "Trial/error switches for baby body/head/leg separation in Explore. Changes rebake mesh immediately — load a baby texture (fox_baby, cow_temperate_baby, donkey_baby, …) and toggle one fix at a time.",
    "EntityPreviewDebugBaselineCompare": "Baseline compare",
    "EntityPreviewDebugForceCpuSkinning": "Force CPU skinning (12-float preview VBO)",
    "EntityPreviewDebugForceCpuSkinningTooltip": "Correct mesh on CPU but separated on GPU ⇒ GPU bind/W() path, not IR walk.",
    "EntityPreviewDebugLogDrawContract": "Log entity draw contract every frame",
    "EntityPreviewDebugLogDrawContractTooltip": "Watch bodyY/headY/legY in the preview log while toggling fixes.",
    "EntityPreviewDebugLayerDebugFalseColor": "Layer debug false-color (depth batches)",
    "EntityPreviewDebugLayerDebugFalseColorTooltip": "Tint each draw batch by PreviewDepthLayerKind (base gray, cutout cyan, cosmetic magenta, emissive yellow).",
    "EntityPreviewDebugPartPoseCompose": "Part pose compose (rebakes mesh)",
    "EntityPreviewDebugPartPoseErTimesT": "Er × T (production)",
    "EntityPreviewDebugPartPoseLegacyTxEr": "T × Er (legacy adult explode)",
    "EntityPreviewDebugPartPoseLegacyTxErTooltip": "Usually wrong for adults; try on babies if limbs detach along parent rotation.",
    "EntityPreviewDebugLerFold": "LER fold (rebakes mesh)",
    "EntityPreviewDebugLerPolicyDefault": "Policy default",
    "EntityPreviewDebugLerStandardWorldRoot": "StandardWorldRoot (column S·M)",
    "EntityPreviewDebugLerRightComposeLocalChain": "RightComposeLocalChain (M·S)",
    "EntityPreviewDebugLerRightComposeLocalChainTooltip": "Per-element M×S — can detach rotated body cuboids on quadrupeds.",
    "EntityPreviewDebugLerSkip": "Skip LER",
    "EntityPreviewDebugPartTreeRepair": "Part-tree repair",
    "EntityPreviewDebugSkipAllPartTreeRepair": "Skip all part-tree repair (raw lifted IR)",
    "EntityPreviewDebugSkipAllPartTreeRepairTooltip": "Bypass GeometryIrPartTreeRepair entirely — see lifter output before any baby fixes.",
    "EntityPreviewDebugIndividualRepairSteps": "Individual repair steps (production defaults ON)",
    "EntityPreviewDebugIndividualRepairStepsDescription": "Uncheck a step to see whether it helps or hurts the current baby. Skip-all overrides these.",
    "EntityPreviewDebugRepairGlobalReparent": "Global reparent (ears, head→head_parts, horns, breeze wind, …)",
    "EntityPreviewDebugRepairQuadrupedLegReparent": "Quadruped leg reparent under body",
    "EntityPreviewDebugRepairQuadrupedLegReparentTooltip": "When not a flat root bake — baby donkey/horse head-stack class uses head-stack rule below.",
    "EntityPreviewDebugRepairForceLegReparent": "Force leg reparent on flat quadruped bake (experimental)",
    "EntityPreviewDebugRepairForceLegReparentTooltip": "Try on fox/cow/chicken babies where legs stay root siblings with body-relative offsets.",
    "EntityPreviewDebugRepairHeadStackLegReparent": "Head-stack nested: reparent flat root legs (baby equine)",
    "EntityPreviewDebugRepairHeadStackLegReparentTooltip": "BabyHorseModel / BabyDonkeyModel — head under body but legs still at root.",
    "EntityPreviewDebugRepairRemoveDuplicateRootSiblings": "Remove duplicate root siblings",
    "EntityPreviewDebugRepairCollapseInnerBody": "Collapse inner_body / fleece trim under body",
    "EntityPreviewDebugRepairDeduplicateNestedPartIds": "Deduplicate nested part ids",
    "EntityPreviewDebugRepairZeroEquineRootOffset": "Zero equine createBodyLayer root offset (horse/donkey)",
    "EntityPreviewDebugRepairZeroEquineRootOffsetTooltip": "Drops root T(0,24,0) anchor on equine createBodyLayer/createBabyLayer bakes.",
    "EntityPreviewDebugSuggestedWorkflow": "Suggested baby workflow: (1) skip-all repair to see raw IR, (2) re-enable production steps one by one, (3) try force leg reparent or head-stack rule on equine babies, (4) compare CPU vs GPU skinning, (5) only then try T×Er or RightCompose LER if clusters still drift.",
}


def load_keys(path: Path) -> dict[str, str]:
    root = ET.parse(path).getroot()
    return {
        data.attrib["name"]: (data.find("value").text or "")
        for data in root.findall("data")
    }


def append_entries(path: Path, entries: dict[str, str]) -> int:
    if not entries:
        return 0
    text = path.read_text(encoding="utf-8")
    added = 0
    chunks: list[str] = []
    for key, value in entries.items():
        if f'name="{key}"' in text:
            continue
        escaped = html.escape(value, quote=False)
        chunks.append(
            f'  <data name="{key}" xml:space="preserve">\n    <value>{escaped}</value>\n  </data>'
        )
        added += 1
    if not chunks:
        return 0
    insertion = "\n".join(chunks) + "\n"
    if "</root>" not in text:
        raise RuntimeError(f"Invalid resx: {path}")
    text = text.replace("</root>", insertion + "</root>", 1)
    path.write_text(text, encoding="utf-8")
    return added


def append_csharp_properties(path: Path, keys: list[str], template: str, *, before: str) -> None:
    text = path.read_text(encoding="utf-8")
    new_props: list[str] = []
    for key in keys:
        prop = template.format(key=key)
        if f" {key} =>" not in text:
            new_props.append(prop)
    if not new_props:
        return
    block = "\n".join(new_props) + "\n"
    text = text.replace(before, block + before, 1)
    path.write_text(text, encoding="utf-8")


def translate_one(translator: GoogleTranslator, text: str) -> str:
    protected = re.sub(r"\{(\d+)\}", lambda m: f"__PH{m.group(1)}__", text)
    for attempt in range(3):
        try:
            result = translator.translate(protected)
            return re.sub(r"__PH(\d+)__", r"{\1}", result)
        except Exception:
            time.sleep(0.4 * (attempt + 1))
    return text


def main() -> None:
    en_keys = load_keys(EN_FILE)
    added_en = append_entries(EN_FILE, NEW_ENGLISH)
    if added_en:
        print(f"Added {added_en} keys to English resx")
        en_keys = load_keys(EN_FILE)
        new_keys = [k for k in NEW_ENGLISH if k in load_keys(EN_FILE)]
        append_csharp_properties(
            DESIGNER_FILE,
            new_keys,
            '    public static string {key} => GetString("{key}");',
            before="    public static string GetStatusString",
        )
        append_csharp_properties(
            LOCALIZED_FILE,
            new_keys,
            "    public static string {key} => Resources.{key};",
            before="}",
        )

    en_keys = load_keys(EN_FILE)
    for culture, target in CULTURES.items():
        path = LANG_DIR / f"Resources.{culture}.resx"
        culture_keys = load_keys(path)
        missing = {k: en_keys[k] for k in en_keys if k not in culture_keys}
        if not missing:
            print(f"{culture}: complete ({len(culture_keys)} keys)")
            continue

        print(f"{culture}: translating {len(missing)} keys...")
        translator = GoogleTranslator(source="en", target=target)
        translated: dict[str, str] = {}
        items = list(missing.items())
        for i, (key, english) in enumerate(items, 1):
            translated[key] = translate_one(translator, english)
            if i % 25 == 0 or i == len(items):
                print(f"  {culture}: {i}/{len(items)}")
        count = append_entries(path, translated)
        print(f"{culture}: appended {count} entries")
        time.sleep(0.3)

    print("Done.")


if __name__ == "__main__":
    main()
