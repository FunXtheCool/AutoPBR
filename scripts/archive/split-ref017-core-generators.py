#!/usr/bin/env python3
"""Mechanical partial-class split for REF-017 (Group K)."""

from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CORE = ROOT / "src" / "AutoPBR.Core"
EMB = CORE / "Embeddings"


def read_lines(path: Path) -> list[str]:
    return path.read_text(encoding="utf-8").splitlines(keepends=True)


def write_partial(
    path: Path,
    body_lines: list[str],
    *,
    usings: list[str],
    ns: str,
    class_decl: str,
    is_static: bool = True,
) -> None:
    static_kw = "static " if is_static else ""
    header = [f"{u}\n" for u in usings]
    header += ["\n", f"namespace {ns};\n", "\n", f"{class_decl}\n", "{\n"]
    footer = ["}\n"]
    path.write_text("".join(header + body_lines + footer), encoding="utf-8")


def slice_body(lines: list[str], start: int, end: int) -> list[str]:
    """1-based inclusive line numbers for method bodies inside class braces."""
    return [lines[i - 1] for i in range(start, end + 1)]


def split_specular() -> None:
    src = CORE / "SpecularGenerator.cs"
    lines = read_lines(src)
    usings = [
        "using AutoPBR.Core.Models;",
        "using AutoPBR.Core.Atlas;",
        "using Colourful;",
        "using SixLabors.ImageSharp;",
        "using SixLabors.ImageSharp.PixelFormats;",
        "using SixLabors.ImageSharp.Processing;",
    ]
    decl = "/// <summary>\n/// Generates LabPBR-compatible specular (_s) textures from diffuse inputs.\n/// </summary>\ninternal static partial class SpecularGenerator"

    # Main shell: constants, fields, log line, tile result type
    main_body = slice_body(lines, 15, 61) + ["\n"] + slice_body(lines, 431, 437)
    write_partial(CORE / "SpecularGenerator.cs", main_body, usings=usings, ns="AutoPBR.Core", class_decl=decl)

    write_partial(
        CORE / "SpecularGenerator.Luminance.cs",
        slice_body(lines, 63, 180) + slice_body(lines, 761, 774) + slice_body(lines, 789, 811),
        usings=usings,
        ns="AutoPBR.Core",
        class_decl=decl,
    )
    write_partial(
        CORE / "SpecularGenerator.Blend.cs",
        slice_body(lines, 182, 245),
        usings=usings,
        ns="AutoPBR.Core",
        class_decl=decl,
    )
    write_partial(
        CORE / "SpecularGenerator.Generate.cs",
        slice_body(lines, 247, 429),
        usings=usings,
        ns="AutoPBR.Core",
        class_decl=decl,
    )
    write_partial(
        CORE / "SpecularGenerator.Tile.cs",
        slice_body(lines, 439, 693),
        usings=usings,
        ns="AutoPBR.Core",
        class_decl=decl,
    )
    write_partial(
        CORE / "SpecularGenerator.Heuristics.cs",
        slice_body(lines, 695, 759) + slice_body(lines, 776, 787),
        usings=usings,
        ns="AutoPBR.Core",
        class_decl=decl,
    )


def split_texture_scanner() -> None:
    src = CORE / "TextureScanner.cs"
    lines = read_lines(src)
    usings = [
        "using System.Collections.Concurrent;",
        "using AutoPBR.Core.Models;",
        "using SixLabors.ImageSharp;",
    ]
    decl = (
        "/// <summary>\n"
        "/// Scans extracted resource packs to build TextureWorkItem lists for conversion.\n"
        "/// </summary>\n"
        "internal static partial class TextureScanner"
    )

    main_body = slice_body(lines, 12, 27)
    write_partial(CORE / "TextureScanner.cs", main_body, usings=usings, ns="AutoPBR.Core", class_decl=decl)

    write_partial(
        CORE / "TextureScanner.Options.cs",
        slice_body(lines, 29, 79),
        usings=usings,
        ns="AutoPBR.Core",
        class_decl=decl,
    )
    write_partial(
        CORE / "TextureScanner.Candidates.cs",
        slice_body(lines, 81, 171),
        usings=usings,
        ns="AutoPBR.Core",
        class_decl=decl,
    )
    write_partial(
        CORE / "TextureScanner.Tags.cs",
        slice_body(lines, 173, 330),
        usings=usings,
        ns="AutoPBR.Core",
        class_decl=decl,
    )
    write_partial(
        CORE / "TextureScanner.Enumerate.cs",
        slice_body(lines, 332, 501),
        usings=usings,
        ns="AutoPBR.Core",
        class_decl=decl,
    )
    write_partial(
        CORE / "TextureScanner.ScanTextures.cs",
        slice_body(lines, 503, 667),
        usings=usings,
        ns="AutoPBR.Core",
        class_decl=decl,
    )


def split_material_tag() -> None:
    src = EMB / "MaterialTagSemanticMatcher.cs"
    lines = read_lines(src)
    usings = ["using System.Text.RegularExpressions;", "using AutoPBR.Core.Models;"]
    decl = (
        "/// <summary>\n"
        "/// Matches texture titles/paths to material tag ids using MiniLM embeddings (cosine similarity to prototypes).\n"
        "/// </summary>\n"
        "public sealed partial class MaterialTagSemanticMatcher"
    )

    main_body = (
        slice_body(lines, 11, 23)
        + slice_body(lines, 25, 29)
        + slice_body(lines, 743, 755)
    )
    write_partial(
        EMB / "MaterialTagSemanticMatcher.cs",
        main_body,
        usings=usings,
        ns="AutoPBR.Core.Embeddings",
        class_decl=decl,
        is_static=False,
    )

    emb_usings = usings
    write_partial(
        EMB / "MaterialTagSemanticMatcher.Match.cs",
        slice_body(lines, 31, 169),
        usings=emb_usings,
        ns="AutoPBR.Core.Embeddings",
        class_decl=decl,
        is_static=False,
    )
    write_partial(
        EMB / "MaterialTagSemanticMatcher.Prototypes.cs",
        slice_body(lines, 171, 297) + slice_body(lines, 578, 596),
        usings=emb_usings,
        ns="AutoPBR.Core.Embeddings",
        class_decl=decl,
        is_static=False,
    )
    write_partial(
        EMB / "MaterialTagSemanticMatcher.MatchDebug.cs",
        slice_body(lines, 299, 387),
        usings=emb_usings,
        ns="AutoPBR.Core.Embeddings",
        class_decl=decl,
        is_static=False,
    )
    write_partial(
        EMB / "MaterialTagSemanticMatcher.Dictionary.cs",
        slice_body(lines, 389, 576),
        usings=emb_usings,
        ns="AutoPBR.Core.Embeddings",
        class_decl=decl,
        is_static=False,
    )
    write_partial(
        EMB / "MaterialTagSemanticMatcher.DictionaryLookup.cs",
        slice_body(lines, 598, 741),
        usings=emb_usings,
        ns="AutoPBR.Core.Embeddings",
        class_decl=decl,
        is_static=False,
    )

    # Records stay in a separate file at end of original
    tail = "".join(lines[756:])
    (EMB / "MaterialTagSemanticMatcher.Types.cs").write_text(tail, encoding="utf-8")


def main() -> None:
    split_specular()
    split_texture_scanner()
    split_material_tag()
    print("REF-017 splits written.")


if __name__ == "__main__":
    main()
