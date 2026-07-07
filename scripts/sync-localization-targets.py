#!/usr/bin/env python3
"""Translate missing keys for specific cultures only."""

import importlib.util
from pathlib import Path

spec = importlib.util.spec_from_file_location(
    "sync", Path(__file__).resolve().parents[1] / "scripts" / "sync-localization.py"
)
sync = importlib.util.module_from_spec(spec)
spec.loader.exec_module(sync)

TARGETS = ["pt", "ru", "ja", "zh-Hans", "hi", "ar"]

if __name__ == "__main__":
    en_keys = sync.load_keys(sync.EN_FILE)
    for culture in TARGETS:
        target = sync.CULTURES[culture]
        path = sync.LANG_DIR / f"Resources.{culture}.resx"
        culture_keys = sync.load_keys(path)
        missing = {k: en_keys[k] for k in en_keys if k not in culture_keys}
        if not missing:
            print(f"{culture}: already complete")
            continue
        print(f"{culture}: translating {len(missing)} keys...")
        items = list(missing.items())
        translated: dict[str, str] = {}
        for i, (key, english) in enumerate(items, 1):
            translated[key] = sync.translate_one(
                sync.GoogleTranslator(source="en", target=target), english
            )
            if i % 50 == 0 or i == len(items):
                print(f"  {culture}: {i}/{len(items)}")
        count = sync.append_entries(path, translated)
        print(f"{culture}: appended {count} entries")
