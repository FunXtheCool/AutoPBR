#!/usr/bin/env python3
"""Finalize AutoPBR.Preview / AutoPBR.Contracts assembly split."""
from __future__ import annotations

import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

# --- Contracts namespace ---
for p in (ROOT / "src/AutoPBR.Contracts").rglob("*.cs"):
    c = p.read_text(encoding="utf-8")
    c = re.sub(
        r"^namespace AutoPBR\.Core\.Preview\.GeometryIr;",
        "namespace AutoPBR.Contracts.GeometryIr;",
        c,
        count=1,
        flags=re.M,
    )
    p.write_text(c, encoding="utf-8")

# --- Preview namespace rename ---
for p in (ROOT / "src/AutoPBR.Preview").rglob("*.cs"):
    c = p.read_text(encoding="utf-8")
    c = c.replace("AutoPBR.Core.Preview", "AutoPBR.Preview")
    c = c.replace("namespace AutoPBR.Core;", "namespace AutoPBR.Preview;")
    p.write_text(c, encoding="utf-8")

# --- Solution-wide namespace/using updates (skip obj/bin) ---
for p in ROOT.rglob("*.cs"):
    if any(x in p.parts for x in ("obj", "bin", ".git")):
        continue
    if "AutoPBR.Preview" in str(p) and p.is_relative_to(ROOT / "src/AutoPBR.Preview"):
        continue
    c = p.read_text(encoding="utf-8")
    n = c.replace("AutoPBR.Core.Preview", "AutoPBR.Preview")
    n = n.replace("using AutoPBR.Contracts.GeometryIr;", "using AutoPBR.Contracts.GeometryIr;")
    if n != c:
        p.write_text(n, encoding="utf-8")

# --- Split ResourcePackConverter preview renderer ---
core_converter = ROOT / "src/AutoPBR.Core/ResourcePackConverter.cs"
text = core_converter.read_text(encoding="utf-8")
marker = "    /// <summary>\n    /// Build a 2D composite preview"
idx = text.find(marker)
if idx == -1:
    raise SystemExit("preview marker not found")
head = text[:idx].rstrip() + "\n}\n"
preview_body = text[idx:]
preview_body = preview_body.replace(
    "public static class ResourcePackConverter",
    "public static class ResourcePackPreviewRenderer",
    1,
)
preview_file = ROOT / "src/AutoPBR.Preview/ResourcePackPreviewRenderer.cs"
preview_file.write_text(
    "using System.IO.Compression;\nusing System.Numerics;\n\n"
    "using AutoPBR.Core;\nusing AutoPBR.Core.Models;\nusing AutoPBR.Preview;\n\n"
    "namespace AutoPBR.Preview;\n\n" + preview_body,
    encoding="utf-8",
)
core_converter.write_text(head, encoding="utf-8")

# --- Item flat sprite policy in Core ---
policy = ROOT / "src/AutoPBR.Core/ItemFlatSpriteTagPolicy.cs"
if not policy.exists():
    policy.write_text(
        """namespace AutoPBR.Core;

internal static class ItemFlatSpriteTagPolicy
{
    public static bool IsItemFlatSpriteExempt(IEnumerable<string> effectiveTagIds)
    {
        foreach (var id in effectiveTagIds)
        {
            if (id.Equals(FlagTagResolver.BlockId, StringComparison.OrdinalIgnoreCase) ||
                id.Equals(FlagTagResolver.EntityId, StringComparison.OrdinalIgnoreCase) ||
                id.Equals(FlagTagResolver.ArmorId, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
""",
        encoding="utf-8",
    )

mat = ROOT / "src/AutoPBR.Core/MaterialTagSemanticResolution.cs"
mc = mat.read_text(encoding="utf-8")
mc = mc.replace("using AutoPBR.Core.Preview;\n", "")
mc = mc.replace(
    "PreviewPathPolicy.IsItemFlatSpriteExempt(effectiveTagIds)",
    "ItemFlatSpriteTagPolicy.IsItemFlatSpriteExempt(effectiveTagIds)",
)
mat.write_text(mc, encoding="utf-8")

# --- Update PreviewService ---
ps = ROOT / "src/AutoPBR.App/Services/PreviewService.cs"
psc = ps.read_text(encoding="utf-8")
psc = psc.replace("ResourcePackConverter.RenderPreview", "ResourcePackPreviewRenderer.RenderPreview")
psc = psc.replace("using AutoPBR.Core;\n", "using AutoPBR.Core;\nusing AutoPBR.Preview;\n")
ps.write_text(psc, encoding="utf-8")

print("assembly split script done")
