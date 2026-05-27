#!/usr/bin/env python3
"""Mechanical partial-class split for REF-018b (Group L follow-up)."""

from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
GC = ROOT / "src" / "AutoPBR.Tools.GeometryCompiler"


def read_lines(path: Path) -> list[str]:
    return path.read_text(encoding="utf-8").splitlines(keepends=True)


def write_partial(
    path: Path,
    body_lines: list[str],
    *,
    usings: list[str],
    ns: str,
    class_decl: str,
) -> None:
    header = [f"{u}\n" for u in usings]
    header += ["\n", f"namespace {ns};\n", "\n", f"{class_decl}\n", "{\n"]
    footer = ["}\n"]
    path.write_text("".join(header + body_lines + footer), encoding="utf-8")


def slice_body(lines: list[str], start: int, end: int) -> list[str]:
    """1-based inclusive line numbers inside class braces."""
    return [lines[i - 1] for i in range(start, end + 1)]


def split_geometry_compiler_host() -> None:
    src = GC / "GeometryCompilerHost.cs"
    lines = read_lines(src)
    usings = ["using System.Text.Json;", "using System.Text.Json.Nodes;", ""]
    decl = "internal sealed partial class GeometryCompilerHost"

    main = (
        slice_body(lines, 8, 47)
        + ["\n"]
        + slice_body(lines, 246, 265)
    )
    write_partial(GC / "GeometryCompilerHost.cs", main, usings=usings, ns="AutoPBR.Tools.GeometryCompiler", class_decl=decl)

    write_partial(
        GC / "GeometryCompilerHost.Batch.cs",
        slice_body(lines, 49, 244),
        usings=usings,
        ns="AutoPBR.Tools.GeometryCompiler",
        class_decl=decl,
    )
    write_partial(
        GC / "GeometryCompilerHost.ProcessOne.cs",
        slice_body(lines, 267, 420),
        usings=usings,
        ns="AutoPBR.Tools.GeometryCompiler",
        class_decl=decl,
    )
    write_partial(
        GC / "GeometryCompilerHost.ShardFinalize.cs",
        slice_body(lines, 422, 615),
        usings=usings,
        ns="AutoPBR.Tools.GeometryCompiler",
        class_decl=decl,
    )
    write_partial(
        GC / "GeometryCompilerHost.ShardIndex.cs",
        slice_body(lines, 617, 767),
        usings=usings,
        ns="AutoPBR.Tools.GeometryCompiler",
        class_decl=decl,
    )


def split_javap_class_disassembly() -> None:
    src = GC / "JavapClassDisassembly.cs"
    lines = read_lines(src)
    usings = [
        "using System.Collections.Concurrent;",
        "using System.Diagnostics;",
        "using System.Text;",
        "using System.Text.RegularExpressions;",
        "",
    ]
    decl = (
        "/// <summary>\n"
        "/// Runs <c>javap -c</c> for a single class and exposes helpers to slice method bodies from stdout.\n"
        "/// </summary>\n"
        "internal static partial class JavapClassDisassembly"
    )

    write_partial(
        GC / "JavapClassDisassembly.cs",
        slice_body(lines, 13, 154),
        usings=usings,
        ns="AutoPBR.Tools.GeometryCompiler",
        class_decl=decl,
    )
    write_partial(
        GC / "JavapClassDisassembly.MethodExtract.cs",
        slice_body(lines, 156, 302),
        usings=usings,
        ns="AutoPBR.Tools.GeometryCompiler",
        class_decl=decl,
    )
    write_partial(
        GC / "JavapClassDisassembly.MeshConcat.cs",
        slice_body(lines, 304, 393),
        usings=usings,
        ns="AutoPBR.Tools.GeometryCompiler",
        class_decl=decl,
    )
    write_partial(
        GC / "JavapClassDisassembly.MeshConcatDeep.cs",
        slice_body(lines, 395, 653),
        usings=usings,
        ns="AutoPBR.Tools.GeometryCompiler",
        class_decl=decl,
    )
    write_partial(
        GC / "JavapClassDisassembly.SupertypeResolve.cs",
        slice_body(lines, 655, 764),
        usings=usings,
        ns="AutoPBR.Tools.GeometryCompiler",
        class_decl=decl,
    )
    write_partial(
        GC / "JavapClassDisassembly.MappedExtract.cs",
        slice_body(lines, 766, 808),
        usings=usings,
        ns="AutoPBR.Tools.GeometryCompiler",
        class_decl=decl,
    )


def main() -> None:
    split_geometry_compiler_host()
    split_javap_class_disassembly()
    print("REF-018b split complete.")


if __name__ == "__main__":
    main()
