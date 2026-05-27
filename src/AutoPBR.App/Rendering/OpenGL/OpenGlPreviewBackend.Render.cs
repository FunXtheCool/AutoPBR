using System.Buffers.Binary;
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
    /// <summary>Called from <see cref="AutoPBR.App.Controls.GlPbrPreviewControl.OnOpenGlRender"/> only.</summary>
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
}
