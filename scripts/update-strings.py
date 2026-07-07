#!/usr/bin/env python3
"""Update specific localization keys across all cultures with curated translations."""

from __future__ import annotations

import html
import re
from pathlib import Path

LANG_DIR = Path(__file__).resolve().parents[1] / "src" / "AutoPBR.App" / "Lang"

# Curated translations. Single-word UI terms are ambiguous for machine translation
# (e.g. "Tune" -> "melody"), so these are set by hand in the intended sense:
#   TabNormals: "Tune" = fine-tune / adjust generation settings
#   AoAdvanced: "Ambient Occlusion"
#   SpecularSection: "Smoothness" (material property)
#   SpecularAdvanced: "Channels"
TRANSLATIONS: dict[str, dict[str, str]] = {
    "Preview3DViewportSection": {
        "en": "Parallax",
        "de": "Parallaxe",
        "es": "Paralaje",
        "fr": "Parallaxe",
        "pt": "Paralaxe",
        "ru": "Параллакс",
        "ja": "パララックス",
        "zh-Hans": "视差",
        "hi": "पैरालैक्स",
        "ar": "البارالاكس",
    },
    "Preview3DCameraExpanderSection": {
        "en": "Camera",
        "de": "Kamera",
        "es": "Cámara",
        "fr": "Caméra",
        "pt": "Câmera",
        "ru": "Камера",
        "ja": "カメラ",
        "zh-Hans": "相机",
        "hi": "कैमरा",
        "ar": "الكاميرا",
    },
    "TabNormals": {
        "en": "Tune",
        "de": "Feinabstimmung",
        "es": "Ajustar",
        "fr": "Régler",
        "pt": "Ajustar",
        "ru": "Настройка",
        "ja": "調整",
        "zh-Hans": "调整",
        "hi": "समायोजन",
        "ar": "ضبط",
    },
    "AoAdvanced": {
        "en": "Ambient Occlusion",
        "de": "Umgebungsokklusion",
        "es": "Oclusión ambiental",
        "fr": "Occlusion ambiante",
        "pt": "Oclusão ambiental",
        "ru": "Окклюзия окружения",
        "ja": "アンビエントオクルージョン",
        "zh-Hans": "环境光遮蔽",
        "hi": "एम्बिएंट ऑक्लूजन",
        "ar": "الإطباق المحيط",
    },
    "SpecularSection": {
        "en": "Smoothness",
        "de": "Glätte",
        "es": "Suavidad",
        "fr": "Lissage",
        "pt": "Suavidade",
        "ru": "Гладкость",
        "ja": "滑らかさ",
        "zh-Hans": "光滑度",
        "hi": "चिकनाहट",
        "ar": "النعومة",
    },
    "SpecularAdvanced": {
        "en": "Channels",
        "de": "Kanäle",
        "es": "Canales",
        "fr": "Canaux",
        "pt": "Canais",
        "ru": "Каналы",
        "ja": "チャンネル",
        "zh-Hans": "通道",
        "hi": "चैनल",
        "ar": "القنوات",
    },
}

CULTURES = ["de", "es", "fr", "pt", "ru", "ja", "zh-Hans", "hi", "ar"]


def set_value(text: str, key: str, value: str) -> tuple[str, bool]:
    escaped = html.escape(value, quote=False)
    pattern = re.compile(
        r'(<data name="' + re.escape(key) + r'"[^>]*>\s*<value>)(.*?)(</value>)',
        re.DOTALL,
    )
    new_text, n = pattern.subn(lambda m: m.group(1) + escaped + m.group(3), text)
    return new_text, n > 0


def main() -> None:
    for culture in CULTURES:
        path = LANG_DIR / f"Resources.{culture}.resx"
        text = path.read_text(encoding="utf-8")
        for key, table in TRANSLATIONS.items():
            value = table[culture]
            text, ok = set_value(text, key, value)
            print(f"{culture}: {key} [{'ok' if ok else 'MISSING'}]")
        path.write_text(text, encoding="utf-8")
    print("Done.")


if __name__ == "__main__":
    main()
