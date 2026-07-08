#!/usr/bin/env python3
"""Mechanical partial-class split for REF-019 (Group M)."""

from __future__ import annotations

from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PREVIEW = ROOT / "src" / "AutoPBR.Core" / "Preview"
ENTITIES = PREVIEW / "Entities"


def read_lines(path: Path) -> list[str]:
    return path.read_text(encoding="utf-8").splitlines(keepends=True)


def slice_body(lines: list[str], start: int, end: int) -> list[str]:
    """1-based inclusive line numbers inside the class body."""
    return [lines[i - 1] for i in range(start, end + 1)]


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
    _ = static_kw
    header = [f"{u}\n" for u in usings]
    header += ["\n", f"namespace {ns};\n", "\n", f"{class_decl}\n", "{\n"]
    footer = ["}\n"]
    path.write_text("".join(header + body_lines + footer), encoding="utf-8")


def split_lift_quality_report() -> None:
    src = PREVIEW / "GeometryIrLiftQualityReport.cs"
    lines = read_lines(src)
    usings = ["using System.Text.Json;", "using System.Text.Json.Nodes;"]
    decl = (
        "/// <summary>Metrics for geometry IR shards (lift quality baseline / regression).</summary>\n"
        "public static partial class GeometryIrLiftQualityReport"
    )

    write_partial(
        PREVIEW / "GeometryIrLiftQualityReport.cs",
        slice_body(lines, 9, 175),
        usings=usings,
        ns="AutoPBR.Core.Preview",
        class_decl=decl,
    )
    write_partial(
        PREVIEW / "GeometryIrLiftQualityReport.Analyze.cs",
        slice_body(lines, 177, 319),
        usings=usings,
        ns="AutoPBR.Core.Preview",
        class_decl=decl,
    )
    write_partial(
        PREVIEW / "GeometryIrLiftQualityReport.Hierarchy.cs",
        slice_body(lines, 321, 566),
        usings=usings,
        ns="AutoPBR.Core.Preview",
        class_decl=decl,
    )
    write_partial(
        PREVIEW / "GeometryIrLiftQualityReport.Reference.cs",
        slice_body(lines, 568, 697),
        usings=usings + ["using AutoPBR.Core.Models;"],
        ns="AutoPBR.Core.Preview",
        class_decl=decl,
    )
    write_partial(
        PREVIEW / "GeometryIrLiftQualityReport.WriteJson.cs",
        slice_body(lines, 699, 747),
        usings=usings,
        ns="AutoPBR.Core.Preview",
        class_decl=decl,
    )


def split_part_tree_repair() -> None:
    src = PREVIEW / "GeometryIrPartTreeRepair.cs"
    lines = read_lines(src)
    usings = ["using System.Text.Json;", "using System.Text.Json.Nodes;"]
    decl = (
        "/// <summary>Repairs known flat IR trees before parity emit (lift ordering gaps).</summary>\n"
        "internal static partial class GeometryIrPartTreeRepair"
    )

    write_partial(
        PREVIEW / "GeometryIrPartTreeRepair.cs",
        slice_body(lines, 17, 231),
        usings=usings,
        ns="AutoPBR.Core.Preview",
        class_decl=decl,
    )
    write_partial(
        PREVIEW / "GeometryIrPartTreeRepair.TreeOps.cs",
        slice_body(lines, 235, 669),
        usings=usings,
        ns="AutoPBR.Core.Preview",
        class_decl=decl,
    )


def split_minecraft_model_baker() -> None:
    src = PREVIEW / "MinecraftModelBaker.cs"
    lines = read_lines(src)
    usings = ["using System.Numerics;", "using AutoPBR.Core.Models;"]
    decl = "internal static partial class MinecraftModelBaker"

    write_partial(
        PREVIEW / "MinecraftModelBaker.cs",
        slice_body(lines, 9, 247),
        usings=usings,
        ns="AutoPBR.Core.Preview",
        class_decl=decl,
    )
    write_partial(
        PREVIEW / "MinecraftModelBaker.FaceEmit.cs",
        slice_body(lines, 249, 621),
        usings=usings,
        ns="AutoPBR.Core.Preview",
        class_decl=decl,
    )


def split_parity_motion() -> None:
    src = ENTITIES / "CleanRoomEntityGeometryIrParityMotion.cs"
    lines = read_lines(src)
    usings = [
        "using System.Numerics;",
        "using System.Text.Json;",
        "using AutoPBR.Core.Models;",
    ]
    decl = "internal sealed partial class CleanRoomEntityModelRuntime"

    write_partial(
        ENTITIES / "CleanRoomEntityGeometryIrParityMotion.cs",
        slice_body(lines, 19, 167),
        usings=usings,
        ns="AutoPBR.Core.Preview",
        class_decl=decl,
        is_static=False,
    )
    write_partial(
        ENTITIES / "CleanRoomEntityGeometryIrParityMotion.SetupAnim.cs",
        slice_body(lines, 169, 450),
        usings=usings,
        ns="AutoPBR.Core.Preview",
        class_decl=decl,
        is_static=False,
    )
    write_partial(
        ENTITIES / "CleanRoomEntityGeometryIrParityMotion.PreviewPasses.cs",
        slice_body(lines, 454, 672),
        usings=usings,
        ns="AutoPBR.Core.Preview",
        class_decl=decl,
        is_static=False,
    )


def main() -> None:
    split_lift_quality_report()
    split_part_tree_repair()
    split_minecraft_model_baker()
    split_parity_motion()
    print("REF-019 splits written.")


if __name__ == "__main__":
    main()
