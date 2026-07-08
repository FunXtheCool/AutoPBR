#!/usr/bin/env python3
"""Rebuild MainWindow.axaml to pre-layout-upgrade state from git HEAD + transcript replays."""

from __future__ import annotations

import json
import subprocess
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
TRANSCRIPT = Path(
    r"C:\Users\John_Phoenix\.cursor\projects\z-Cursor-Projects-AutoPBR"
    r"\agent-transcripts\1fada703-a41f-4d59-9e41-e45af7398b59"
    r"\1fada703-a41f-4d59-9e41-e45af7398b59.jsonl"
)
MAIN_WINDOW = ROOT / "src/AutoPBR.App/Views/MainWindow.axaml"
TRANSFORM = ROOT / "scripts/transform_overlay_sliders.py"


def load_head_main_window() -> str:
    return subprocess.check_output(
        ["git", "show", "HEAD:src/AutoPBR.App/Views/MainWindow.axaml"],
        cwd=ROOT,
        text=True,
    )


def apply_overlay_transform(text: str) -> str:
    tmp = MAIN_WINDOW.with_suffix(".replay.tmp.axaml")
    tmp.write_text(text, encoding="utf-8")
    subprocess.check_call([sys.executable, str(TRANSFORM), str(tmp)], cwd=ROOT)
    return tmp.read_text(encoding="utf-8")


def replay_transcript_replaces(text: str) -> str:
    stop_markers = ("RenderSettingsView", "SettingRow.axaml", "NavigateToShaderTab_Click")
    for line in TRANSCRIPT.read_text(encoding="utf-8").splitlines():
        try:
            obj = json.loads(line)
        except json.JSONDecodeError:
            continue
        for part in obj.get("message", {}).get("content", []):
            if part.get("type") != "tool_use" or part.get("name") != "StrReplace":
                continue
            inp = part.get("input", {})
            path = inp.get("path", "").replace("\\", "/")
            if not path.endswith("MainWindow.axaml"):
                continue
            old = inp.get("old_string", "")
            new = inp.get("new_string", "")
            if any(marker in new for marker in stop_markers):
                continue
            if "RenderSettingsView" in old or "ShaderTabItem" in old and "RenderSettingsView" in new:
                continue
            if old not in text:
                continue
            text = text.replace(old, new, 1)
    return text


def main() -> int:
    text = load_head_main_window()
    text = apply_overlay_transform(text)
    text = replay_transcript_replaces(text)
    MAIN_WINDOW.write_text(text, encoding="utf-8")
    print(f"Wrote {MAIN_WINDOW}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
