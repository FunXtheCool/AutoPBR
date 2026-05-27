#!/usr/bin/env python3
"""Mechanically split MainWindow.axaml.cs by feature region (REF-016)."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src/AutoPBR.App/Views"
MAIN = SRC / "MainWindow.axaml.cs"

CHUNKS = {
    "MainWindow.axaml.cs": (1, 143),
    "MainWindow.WindowChrome.cs": (176, 471),
    "MainWindow.Explore.cs": (28, 30, 145, 174, 495, 550),  # special: fields + methods + onloaded tail
    "MainWindow.Preview.cs": (28, 30, 473, 493, 504, 589),
    "MainWindow.Menus.cs": (22, 26, 591, 752),
}


def read_lines() -> list[str]:
    return MAIN.read_text(encoding="utf-8").splitlines(keepends=True)


def header(usings: str) -> str:
    return usings + "\nnamespace AutoPBR.App.Views;\n\npublic partial class MainWindow : Window\n{\n"


def footer() -> str:
    return "}\n"


USINGS_SHELL = """using System.ComponentModel;

using AutoPBR.App.ViewModels;

using Avalonia;
using Avalonia.Controls;
"""

USINGS_CHROME = """using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
"""

USINGS_EXPLORE = """using AutoPBR.App.ViewModels;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
"""

USINGS_PREVIEW = """using System.Diagnostics;

using AutoPBR.App.Controls;
using AutoPBR.App.Services;
using AutoPBR.App.ViewModels;

using Avalonia;
using Avalonia.Controls;
"""

USINGS_MENUS = """using System.Globalization;
using System.Text;
using System.Text.Json;

using AutoPBR.App.ViewModels;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
"""


def extract_ranges(lines: list[str], ranges: list[int]) -> str:
    """ranges are 1-based inclusive line numbers, in pairs or singles."""
    out: list[str] = []
    i = 0
    while i < len(ranges):
        if i + 1 < len(ranges):
            start, end = ranges[i], ranges[i + 1]
            out.extend(lines[start - 1 : end])
            i += 2
        else:
            ln = ranges[i]
            out.append(lines[ln - 1])
            i += 1
    return "".join(out)


def main() -> None:
    lines = read_lines()

    shell_body = extract_ranges(lines, [37, 143])
    (SRC / "MainWindow.axaml.cs").write_text(
        header(USINGS_SHELL)
        + "    private const double RoundedCornerRadius = 8;\n"
        + "    private Border? _rootBorder;\n"
        + "    private double _lastUiScaleForWindow = 1.0;\n\n"
        + shell_body
        + footer(),
        encoding="utf-8",
    )

    chrome_body = extract_ranges(lines, [176, 471])
    (SRC / "MainWindow.WindowChrome.cs").write_text(
        header(USINGS_CHROME) + chrome_body + footer(), encoding="utf-8"
    )

    explore_body = (
        extract_ranges(lines, [28, 30])
        + extract_ranges(lines, [145, 174])
        + "    private void WireExploreJumpToTopOnLoaded()\n    {\n"
        + extract_ranges(lines, [496, 501])
        + "    }\n\n"
        + extract_ranges(lines, [529, 550])
    )
    (SRC / "MainWindow.Explore.cs").write_text(
        header(USINGS_EXPLORE) + explore_body + footer(), encoding="utf-8"
    )

    preview_onloaded = extract_ranges(lines, [473, 494]).replace(
        "        // Jump-to-top (Explorer) button: show when Explore tab is active and the tree list is scrolled.\n"
        "        if (ExploreTreeScrollViewer is { } exploreTree && MainTabControl is { } tabs && JumpToTopButton is not null)\n"
        "        {\n"
        "            exploreTree.ScrollChanged += (_, _) => UpdateJumpToTopButtonVisibility(tabs.SelectedIndex, exploreTree.Offset.Y);\n"
        "            tabs.SelectionChanged += (_, _) => UpdateJumpToTopButtonVisibility(tabs.SelectedIndex, exploreTree.Offset.Y);\n"
        "            UpdateJumpToTopButtonVisibility(tabs.SelectedIndex, exploreTree.Offset.Y);\n"
        "        }\n",
        "        WireExploreJumpToTopOnLoaded();\n",
    )
    preview_body = (
        extract_ranges(lines, [28, 30])
        + preview_onloaded
        + extract_ranges(lines, [504, 589])
    )
    (SRC / "MainWindow.Preview.cs").write_text(
        header(USINGS_PREVIEW) + preview_body + footer(), encoding="utf-8"
    )

    menus_body = extract_ranges(lines, [22, 26]) + extract_ranges(lines, [591, 752])
    (SRC / "MainWindow.Menus.cs").write_text(
        header(USINGS_MENUS) + menus_body + footer(), encoding="utf-8"
    )

    for name in CHUNKS:
        p = SRC / name
        if p.exists():
            print(f"{name}: {len(p.read_text(encoding='utf-8').splitlines())} lines")


if __name__ == "__main__":
    main()
