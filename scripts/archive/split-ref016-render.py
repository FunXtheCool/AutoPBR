#!/usr/bin/env python3
"""Mechanically split OpenGlPreviewBackend.Render.cs into pass partials (REF-016)."""
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SRC = ROOT / "src/AutoPBR.App/Rendering/OpenGL"
RENDER = SRC / "OpenGlPreviewBackend.Render.cs"

HEADER = """using System.Buffers.Binary;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

using AutoPBR.App.Rendering.Abstractions;
using AutoPBR.App.Rendering.Scene;
using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

using Avalonia.OpenGL;
using Avalonia.Platform;

using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

/// <summary>OpenGL implementation of <see cref="IRenderPreviewBackend"/>; GPU entry points must run on the OpenGL thread (Avalonia <see cref="AutoPBR.App.Controls.GlPbrPreviewControl"/> callbacks).</summary>
public sealed partial class OpenGlPreviewBackend
{
"""

FOOTER = "}\n"

FRAME = """using System.Numerics;

using AutoPBR.App.Rendering.Abstractions;
using AutoPBR.App.Rendering.Scene;
using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

using Silk.NET.OpenGL;

namespace AutoPBR.App.Rendering.OpenGL;

public sealed partial class OpenGlPreviewBackend
{
    private ref struct GlRenderFrame
    {
        public GL Gl;
        public int DefaultFbo;
        public int VpX;
        public int VpY;
        public int Vw;
        public int Vh;
        public PreviewRenderSettings Settings;
        public IRenderPreviewScene Scene;
        public PreviewMaterial? Material;
        public PreviewModelSubject? BlockModel;
        public PreviewMaterial[]? BlockSlots;
        public double Rotation;
        public double RenderTime;
        public Vector3 OrbitBaseTarget;
        public Vector3 OrbitPan;
        public Vector3 DebugFlyWorldOffset;
        public float OrbitYaw;
        public float OrbitPitch;
        public float OrbitDistance;
        public bool MeshDirty;
        public bool MaterialDirty;
        public bool EntityEmulatedPreview;
        public EntityEmulatedPreviewRebakeContext? EntityRebakeCtx;
        public bool EntityEmulatedMaterialsOk;
        public float EntityEmulatedAnimClock;
        public bool EntityEmulatedPauseEdge;
        public bool UploadedLiveEntityAnim;
        public bool EntityBoneSnapshotValid;
        public int EntityBoneSnapshotCount;
        public Vector3 WorldLightDir;
        public Matrix4x4 ShadowVp;
        public Matrix4x4 ModelMatrix;
        public int EntityAlphaModeUniform;
        public bool EntityBlendDraw;
        public bool EnableParallaxEff;
        public bool EnableParallaxAoEff;
        public bool EnableNormalMapEff;
        public bool EnableSpecularMapEff;
        public bool EnableParallaxShadowEff;
        public bool ShadowAvailable;
        public bool DrewAtmosphereSky;
        public Vector3 Eye;
        public Vector3 LookTarget;
        public Matrix4x4 Proj;
        public Matrix4x4 View;
        public Vector3 LightDir;
    }
}
"""

ORCHESTRATOR = HEADER + """    /// <summary>Called from <see cref="AutoPBR.App.Controls.GlPbrPreviewControl.OnOpenGlRender"/> only.</summary>
    internal void GlRender(GlInterface glInterface, int framebuffer, int pixelWidth, int pixelHeight)
    {
        _ = glInterface;
        PreviewRenderSettings settings;
        IRenderPreviewScene? scene;
        PreviewMaterial? material;
        PreviewModelSubject? blockModel;
        PreviewMaterial[]? blockSlots;
        double rotation;
        double renderTime;
        Vector3 orbitBaseTarget;
        Vector3 orbitPan;
        Vector3 debugFlyWorldOffset;
        float orbitYaw;
        float orbitPitch;
        float orbitDistance;
        lock (_sync)
        {
            if (!_gpuAlive || _gl is null || _program is null || !_program.IsValid || _albedo is null ||
                _normal is null || _spec is null || _height is null || _mesh is null || _groundMesh is null ||
                _neutralNormal is null || _neutralSpec is null || _neutralHeight is null)
            {
                return;
            }

            settings = CloneSettings(_settings);
            scene = _scene;
            material = _material;
            blockModel = _blockModelSubject;
            blockSlots = _blockModelSlots;
            rotation = _rotationAccum;
            renderTime = _renderTimeAccum;
            orbitBaseTarget = _orbitBaseTarget;
            orbitPan = _orbitPan;
            debugFlyWorldOffset = _debugFlyWorldOffset;
            orbitYaw = _orbitYaw;
            orbitPitch = _orbitPitch;
            orbitDistance = _orbitDistance;
        }

        var gl = _gl!;
        var defaultFbo = framebuffer;
        var vpX = 0;
        var vpY = 0;
        var vw = Math.Max(1, pixelWidth);
        var vh = Math.Max(1, pixelHeight);

        if (defaultFbo != 0)
        {
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, (uint)defaultFbo);
        }
        else
        {
            gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        }

        gl.Viewport(vpX, vpY, (uint)vw, (uint)vh);
        gl.Disable(EnableCap.ScissorTest);

        if (scene is null)
        {
            gl.ClearColor(0.12f, 0.12f, 0.14f, 1f);
            gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            return;
        }

        bool meshDirty;
        bool materialDirty;
        lock (_sync)
        {
            meshDirty = _meshDirty;
            materialDirty = _materialDirty;
        }

        var frame = new GlRenderFrame
        {
            Gl = gl,
            DefaultFbo = defaultFbo,
            VpX = vpX,
            VpY = vpY,
            Vw = vw,
            Vh = vh,
            Settings = settings,
            Scene = scene,
            Material = material,
            BlockModel = blockModel,
            BlockSlots = blockSlots,
            Rotation = rotation,
            RenderTime = renderTime,
            OrbitBaseTarget = orbitBaseTarget,
            OrbitPan = orbitPan,
            DebugFlyWorldOffset = debugFlyWorldOffset,
            OrbitYaw = orbitYaw,
            OrbitPitch = orbitPitch,
            OrbitDistance = orbitDistance,
            MeshDirty = meshDirty,
            MaterialDirty = materialDirty,
        };

        GlRenderPassSetup(ref frame);
        GlRenderPassShadow(ref frame);
        GlRenderPassScene(ref frame);
        GlRenderPassPost(ref frame);
    }
""" + FOOTER

PASS_WRAPPERS = {
    "Setup": ("GlRenderPassSetup", 99, 400),
    "Shadow": ("GlRenderPassShadow", 401, 545),
    "Scene": ("GlRenderPassScene", 547, 816),
    "Post": ("GlRenderPassPost", 818, 821),
}

# Longest identifiers first to avoid partial replacements.
IDENT_MAP = [
    ("entityEmulatedMaterialsOk", "frame.EntityEmulatedMaterialsOk"),
    ("entityEmulatedPauseEdge", "frame.EntityEmulatedPauseEdge"),
    ("entityEmulatedAnimClock", "frame.EntityEmulatedAnimClock"),
    ("entityEmulatedPreview", "frame.EntityEmulatedPreview"),
    ("entityBoneSnapshotValid", "frame.EntityBoneSnapshotValid"),
    ("entityBoneSnapshotCount", "frame.EntityBoneSnapshotCount"),
    ("enableParallaxShadowEff", "frame.EnableParallaxShadowEff"),
    ("enableParallaxAoEff", "frame.EnableParallaxAoEff"),
    ("enableNormalMapEff", "frame.EnableNormalMapEff"),
    ("enableSpecularMapEff", "frame.EnableSpecularMapEff"),
    ("enableParallaxEff", "frame.EnableParallaxEff"),
    ("entityAlphaModeUniform", "frame.EntityAlphaModeUniform"),
    ("uploadedLiveEntityAnim", "frame.UploadedLiveEntityAnim"),
    ("debugFlyWorldOffset", "frame.DebugFlyWorldOffset"),
    ("entityBlendDraw", "frame.EntityBlendDraw"),
    ("orbitBaseTarget", "frame.OrbitBaseTarget"),
    ("entityRebakeCtx", "frame.EntityRebakeCtx"),
    ("drewAtmosphereSky", "frame.DrewAtmosphereSky"),
    ("shadowAvailable", "frame.ShadowAvailable"),
    ("worldLightDir", "frame.WorldLightDir"),
    ("materialDirty", "frame.MaterialDirty"),
    ("orbitDistance", "frame.OrbitDistance"),
    ("orbitPitch", "frame.OrbitPitch"),
    ("orbitYaw", "frame.OrbitYaw"),
    ("modelMatrix", "frame.ModelMatrix"),
    ("defaultFbo", "frame.DefaultFbo"),
    ("lookTarget", "frame.LookTarget"),
    ("blockSlots", "frame.BlockSlots"),
    ("blockModel", "frame.BlockModel"),
    ("renderTime", "frame.RenderTime"),
    ("shadowVp", "frame.ShadowVp"),
    ("meshDirty", "frame.MeshDirty"),
    ("orbitPan", "frame.OrbitPan"),
    ("lightDir", "frame.LightDir"),
    ("material", "frame.Material"),
    ("settings", "frame.Settings"),
    ("rotation", "frame.Rotation"),
    ("defaultFbo", "frame.DefaultFbo"),
    ("vpX", "frame.VpX"),
    ("vpY", "frame.VpY"),
    ("scene", "frame.Scene"),
    ("vw", "frame.Vw"),
    ("vh", "frame.Vh"),
    ("proj", "frame.Proj"),
    ("view", "frame.View"),
    ("eye", "frame.Eye"),
    ("gl", "frame.Gl"),
]

VAR_DECL_REWRITES = [
    (r"\bvar entityEmulatedPreview =", "frame.EntityEmulatedPreview ="),
    (r"\bvar entityRebakeCtx =", "frame.EntityRebakeCtx ="),
    (r"\bvar entityEmulatedMaterialsOk =", "frame.EntityEmulatedMaterialsOk ="),
    (r"\bvar entityEmulatedPauseEdge =", "frame.EntityEmulatedPauseEdge ="),
    (r"\bvar uploadedLiveEntityAnim =", "frame.UploadedLiveEntityAnim ="),
    (r"\bvar entityBoneSnapshotValid =", "frame.EntityBoneSnapshotValid ="),
    (r"\bvar entityBoneSnapshotCount =", "frame.EntityBoneSnapshotCount ="),
    (r"\bvar worldLightDir =", "frame.WorldLightDir ="),
    (r"\bvar shadowVp =", "frame.ShadowVp ="),
    (r"\bvar modelMatrix =", "frame.ModelMatrix ="),
    (r"\bvar entityAlphaModeUniform =", "frame.EntityAlphaModeUniform ="),
    (r"\bvar entityBlendDraw =", "frame.EntityBlendDraw ="),
    (r"\bvar enableParallaxEff =", "frame.EnableParallaxEff ="),
    (r"\bvar enableParallaxAoEff =", "frame.EnableParallaxAoEff ="),
    (r"\bvar enableNormalMapEff =", "frame.EnableNormalMapEff ="),
    (r"\bvar enableSpecularMapEff =", "frame.EnableSpecularMapEff ="),
    (r"\bvar enableParallaxShadowEff =", "frame.EnableParallaxShadowEff ="),
    (r"\bvar shadowAvailable =", "frame.ShadowAvailable ="),
    (r"\bvar drewAtmosphereSky =", "frame.DrewAtmosphereSky ="),
    (r"\bvar proj =", "frame.Proj ="),
    (r"\bvar view =", "frame.View ="),
    (r"\bvar eye =", "frame.Eye ="),
    (r"\bvar lookTarget =", "frame.LookTarget ="),
    (r"\bvar lightDir =", "frame.LightDir ="),
    (r"\bfloat entityEmulatedAnimClock = 0f;", "frame.EntityEmulatedAnimClock = 0f;"),
]


def indent_block(text: str, extra: int = 8) -> str:
    lines = text.splitlines()
    return "\n".join((" " * extra + ln if ln.strip() else ln) for ln in lines)


def rewrite_locals(body: str) -> str:
    out = body
    for pattern, repl in VAR_DECL_REWRITES:
        out = re.sub(pattern, repl, out)
    for ident, repl in IDENT_MAP:
        out = re.sub(rf"\b{re.escape(ident)}\b", repl, out)
    out = re.sub(r"\bout var frame\.", "out frame.", out)
    out = re.sub(r"\bvar frame\.", "frame.", out)
    return out


def main() -> None:
    lines = RENDER.read_text(encoding="utf-8").splitlines()
    method_start = 22
    body_lines = lines[method_start + 1 : -2]

    (SRC / "OpenGlPreviewBackend.Render.Frame.cs").write_text(FRAME, encoding="utf-8")
    RENDER.write_text(ORCHESTRATOR, encoding="utf-8")

    for suffix, (method_name, start_1, end_1) in PASS_WRAPPERS.items():
        start_idx = start_1 - 24
        end_idx = end_1 - 23
        chunk = body_lines[start_idx:end_idx]
        body = rewrite_locals("\n".join(chunk))
        content = (
            HEADER
            + f"    private void {method_name}(ref GlRenderFrame frame)\n    {{\n"
            + indent_block(body)
            + "\n    }\n"
            + FOOTER
        )
        path = SRC / f"OpenGlPreviewBackend.Render.Pass{suffix}.cs"
        path.write_text(content, encoding="utf-8")
        print(f"Wrote {path.name}: {len(content.splitlines())} lines")


if __name__ == "__main__":
    main()
