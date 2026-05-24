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
public sealed partial class OpenGlPreviewBackend : IRenderPreviewBackend
{
    private static readonly float DefaultOrbitBoomArmDistance =
        MathF.Sqrt(3.6f * 3.6f + 2.6f * 2.6f + 3.6f * 3.6f);

    private readonly object _sync = new();
    private GL? _gl;
    private GlShaderProgram? _program;
    private GlShaderProgram? _shadowProgram;
    private GlShadowMapTarget? _shadowTarget;
    private GlTexture2D? _albedo;
    private GlTexture2D? _normal;
    private GlTexture2D? _spec;
    private GlTexture2D? _height;
    private GlMeshBuffer? _mesh;
    private GlMeshBuffer? _groundMesh;
    private GlTexture2D? _grassGroundAlbedo;
    private GlTexture2D? _neutralNormal;
    private GlTexture2D? _neutralSpec;
    private GlTexture2D? _neutralHeight;
    private bool _grassGroundReady;
    private GlLineShaderProgram? _lineProgram;
    private uint _gridVao;
    private uint _gridVbo;
    private int _gridVertexCount;
    private uint _axesVao;
    private uint _axesVbo;
    private GlSunBillboardProgram? _sunProgram;
    private uint _sunVao;
    private uint _sunVbo;
    private GlShaderProgram? _atmoTransProgram;
    private GlShaderProgram? _atmoSkyViewProgram;
    private GlShaderProgram? _atmoSkyProgram;
    private uint _atmoQuadVao;
    private uint _atmoQuadVbo;
    private uint _atmoTransmittanceTex;
    private uint _atmoSkyViewTex;
    private uint _atmoTransmittanceFbo;
    private uint _atmoSkyViewFbo;
    private Vector3 _lastAtmoSunDir;
    private float _lastAtmoTurbidity = -1f;
    private float _lastAtmoSunIntensity = -1f;
    private float _lastAtmoHorizonFalloff = -1f;
    private bool _atmoLutsValid;
    private PreviewRenderSettings _settings = new();
    private IRenderPreviewScene? _scene;
    private PreviewMaterial? _material;
    private PreviewModelSubject? _blockModelSubject;
    private PreviewMaterial[]? _blockModelSlots;
    private bool _materialDirty = true;
    private bool _meshDirty = true;
    private double _rotationAccum;
    private double _renderTimeAccum;
    private bool _prevPauseEntityIdleAnimation;
    private float _frozenEntityIdleAnimClock;

    /// <summary>Wall-clock (see <see cref="GlRender"/> <c>renderTime</c>) minimum spacing between emulated-entity CPU mesh rebakes.</summary>
    private const double MinEmulatedEntityRebakeIntervalSeconds = 1.0 / 30.0;

    private double _lastEmulatedEntityRebakeRenderTime = double.NegativeInfinity;
    private string? _emulatedRebakeSubjectKey;
    private string? _emulatedGpuSkinPrepFailedKey;

    private readonly Matrix4x4[] _entityBoneScratch = new Matrix4x4[EntityGpuSkinningLimits.MaxBones];
    /// <summary>std140: 64 mat4 (row-major source floats interpreted as column-major in GLSL) + 16 B tail scalars.</summary>
    private const int EntitySkinningUboMatrixBytes = 64 * 64;

    private const int EntitySkinningUboTotalBytes = EntitySkinningUboMatrixBytes + 16;

    private readonly byte[] _entitySkinningUboScratch = new byte[EntitySkinningUboTotalBytes];
    private uint _entityBoneUbo;

    private const uint EntitySkinningUboBindingPoint = 2;

    private bool _disposed;
    private string? _lastError;
    private bool _gpuAlive;
    private Action<string>? _diagnosticLog;
    private bool _loggedMeshReady;
    private bool _loggedZeroIndex;

    /// <summary>Orbit camera is re-synced from the scene only when this changes (block vs item), so texture swaps keep user framing.</summary>
    private PreviewSceneKind? _orbitSyncedKind;

    private Vector3 _orbitBaseTarget;
    private float _orbitYaw;
    private float _orbitPitch;
    private float _orbitDistance = 4f;
    private Vector3 _orbitPan;
    /// <summary>Debug fly: world-space offset applied to the eye only (orbit pivot stays base+pan). Cleared on scene reseed and camera reset.</summary>
    private Vector3 _debugFlyWorldOffset;
    private bool _debugFlyRmbHeld;
    private bool _flyKeyW;
    private bool _flyKeyA;
    private bool _flyKeyS;
    private bool _flyKeyD;
    private float _orbitYawDefault;
    private float _orbitPitchDefault;
    private float _orbitDistanceDefault;
    private Vector3 _orbitPanDefault;
    private float _camOrbitRadPerPx = 0.006f;
    private float _camPanPerPx = 0.0022f;
    private float _camZoomPerWheelStep = 0.12f;
    /// <summary>Pivot-to-eye distance used when seeding orbit and when applying boom from settings (wheel zoom changes <see cref="_orbitDistance"/> temporarily).</summary>
    private float _orbitBoomArmDistance = DefaultOrbitBoomArmDistance;
    private bool _userCameraDragging;

    public string BackendName => "OpenGL";
    public bool IsInitialized => _gpuAlive && _program?.IsValid == true;
    public string? LastErrorMessage => _lastError;

    public void Initialize(RenderPreviewInitializationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        lock (_sync)
        {
            _ = options.MsaaSamples;
        }
    }

    /// <summary>Shader/renderer diagnostics (same sink as main log UI). Invoked from the GL thread.</summary>
    public void SetDiagnosticLog(Action<string>? log)
    {
        lock (_sync)
        {
            _diagnosticLog = log;
        }
    }

    private void EmitDiagnostic(string message)
    {
        Action<string>? log;
        lock (_sync)
        {
            log = _diagnosticLog;
        }

        log?.Invoke(message);
    }

    public void Resize(int width, int height)
    {
        lock (_sync)
        {
            _ = (Math.Max(1, width), Math.Max(1, height));
        }
    }

    public void SetScene(IRenderPreviewScene scene)
    {
        lock (_sync)
        {
            var orbitBucket = scene.SceneKind == PreviewSceneKind.ItemPlane
                ? PreviewSceneKind.ItemPlane
                : PreviewSceneKind.BlockCube;
            var reseedOrbit = _orbitSyncedKind is null || _orbitSyncedKind != orbitBucket;
            if (reseedOrbit)
            {
                SyncOrbitFromSceneLocked(scene);
                _orbitSyncedKind = orbitBucket;
            }

            _scene = scene;
            _meshDirty = true;
        }
    }

    public void SetMaterial(PreviewMaterial? material)
    {
        lock (_sync)
        {
            _blockModelSubject = null;
            _blockModelSlots = null;
            _material = material;
            _materialDirty = true;
        }
    }

    /// <summary>Multi-material Java block model preview. Cleared by <see cref="SetMaterial"/>.</summary>
    public void SetBlockModelPreview(PreviewModelSubject? subject, PreviewMaterial[]? slotMaterials)
    {
        lock (_sync)
        {
            _blockModelSubject = subject;
            _blockModelSlots = slotMaterials;
            _prevPauseEntityIdleAnimation = false;
            if (subject is not null && slotMaterials is { Length: > 0 })
            {
                var pi = Math.Clamp(subject.PrimaryMaterialIndex, 0, slotMaterials.Length - 1);
                _material = slotMaterials[pi];
            }

            _materialDirty = true;
            _meshDirty = true;
        }
    }

    private static bool IsSolidBlockGeometry(PreviewSceneKind k) =>
        k is PreviewSceneKind.BlockCube or PreviewSceneKind.BlockModel;

    private static bool IsEntityEmulatedPreview(PreviewModelSubject? blockModel) =>
        string.Equals(blockModel?.AnimationPreset, "entity_emulated", StringComparison.Ordinal);

    /// <summary>
    /// Entity rigs baked from clean-room Java cuboids can disagree with AutoPBR-generated tangent-space maps;
    /// disable solid back-face culling for those previews so occasional winding mismatches do not punch holes.
    /// </summary>
    private static bool ShouldCullSolidBackFaces(PreviewSceneKind sceneKind, PreviewModelSubject? blockModel) =>
        IsSolidBlockGeometry(sceneKind) && !IsEntityEmulatedPreview(blockModel);

    public void SetRenderSettings(PreviewRenderSettings settings)
    {
        lock (_sync)
        {
            var prev = _settings;
            _settings = CloneSettings(settings);
            if (_settings.DrawPreviewSubject != prev.DrawPreviewSubject ||
                _settings.EnableEntityAnimation != prev.EnableEntityAnimation ||
                _settings.PauseEntityIdleAnimation != prev.PauseEntityIdleAnimation ||
                Math.Abs(_settings.EntityAnimationSpeed - prev.EntityAnimationSpeed) > 1e-6f ||
                Math.Abs(_settings.EntityAnimationAmplitude - prev.EntityAnimationAmplitude) > 1e-6f)
            {
                _meshDirty = true;
            }

            if (_settings.EnableEntityAnimation != prev.EnableEntityAnimation ||
                _settings.PauseEntityIdleAnimation != prev.PauseEntityIdleAnimation)
            {
                _lastEmulatedEntityRebakeRenderTime = double.NegativeInfinity;
            }
        }
    }

    public void RenderFrame(TimeSpan elapsed)
    {
        lock (_sync)
        {
            _renderTimeAccum += elapsed.TotalSeconds;
            if (_settings.AutoRotate && !_userCameraDragging)
            {
                _rotationAccum += elapsed.TotalSeconds * 0.65;
            }

            if (_debugFlyRmbHeld && (_flyKeyW || _flyKeyA || _flyKeyS || _flyKeyD))
            {
                ComposeOrbitEye(_orbitBaseTarget, _orbitPan, _debugFlyWorldOffset, _orbitYaw, _orbitPitch,
                    _orbitDistance, out var eye0, out var look0);
                var forward = look0 - eye0;
                var fl = forward.Length();
                if (fl > 1e-5f)
                {
                    forward /= fl;
                    var worldUp = Vector3.UnitY;
                    var right = Vector3.Normalize(Vector3.Cross(forward, worldUp));
                    if (right.LengthSquared() < 1e-8f)
                    {
                        right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitZ));
                    }

                    var move = Vector3.Zero;
                    if (_flyKeyW)
                    {
                        move += forward;
                    }

                    if (_flyKeyS)
                    {
                        move -= forward;
                    }

                    if (_flyKeyD)
                    {
                        move += right;
                    }

                    if (_flyKeyA)
                    {
                        move -= right;
                    }

                    if (move.LengthSquared() > 1e-8f)
                    {
                        var speed = 5f * (float)elapsed.TotalSeconds *
                                    MathF.Max(0.35f, _orbitDistance * 0.15f);
                        _debugFlyWorldOffset += Vector3.Normalize(move) * speed;
                    }
                }
            }
        }
    }

    /// <inheritdoc />
    public bool TryGetCameraDebugPose(out Vector3 eye, out Vector3 lookTarget)
    {
        lock (_sync)
        {
            if (!_gpuAlive)
            {
                eye = default;
                lookTarget = default;
                return false;
            }

            ComposeOrbitEye(_orbitBaseTarget, _orbitPan, _debugFlyWorldOffset, _orbitYaw, _orbitPitch, _orbitDistance,
                out eye, out lookTarget);
            return true;
        }
    }

    /// <inheritdoc />
    public void SetDebugFlyInput(bool rightMouseHeld, bool keyW, bool keyA, bool keyS, bool keyD)
    {
        lock (_sync)
        {
            _debugFlyRmbHeld = rightMouseHeld;
            _flyKeyW = keyW;
            _flyKeyA = keyA;
            _flyKeyS = keyS;
            _flyKeyD = keyD;
        }
    }

    /// <summary>Orbit (rad/pixel), pan (world scale per pixel, scaled by distance in <see cref="ApplyCameraPanPixels"/>), zoom (step strength per wheel notch).</summary>
    public void SetCameraSensitivities(float orbitRadPerPx, float panPerPixel, float zoomPerWheelStep)
    {
        lock (_sync)
        {
            _camOrbitRadPerPx = Math.Clamp(orbitRadPerPx, 0.0008f, 0.04f);
            _camPanPerPx = Math.Clamp(panPerPixel, 0.0003f, 0.02f);
            _camZoomPerWheelStep = Math.Clamp(zoomPerWheelStep, 0.02f, 0.5f);
        }
    }

    /// <summary>World-space orbit boom length (pivot to eye). Updates current distance and reset baseline.</summary>
    public void SetOrbitBoomArmDistance(float boomWorldUnits)
    {
        lock (_sync)
        {
            _orbitBoomArmDistance = Math.Clamp(boomWorldUnits, 1.05f, 120f);
            _orbitDistance = _orbitBoomArmDistance;
            _orbitDistanceDefault = _orbitBoomArmDistance;
        }
    }

    public void SetUserCameraDragging(bool dragging)
    {
        lock (_sync)
        {
            _userCameraDragging = dragging;
        }
    }

    public void ResetPreviewCameraToDefaults()
    {
        lock (_sync)
        {
            _orbitYaw = _orbitYawDefault;
            _orbitPitch = _orbitPitchDefault;
            _orbitDistance = _orbitDistanceDefault;
            _orbitPan = _orbitPanDefault;
            _debugFlyWorldOffset = Vector3.Zero;
        }
    }

    public void ApplyCameraOrbitPixels(float dx, float dy)
    {
        lock (_sync)
        {
            _orbitYaw -= dx * _camOrbitRadPerPx;
            _orbitPitch = Math.Clamp(_orbitPitch - dy * _camOrbitRadPerPx, -1.55f, 1.55f);
        }
    }

    public void ApplyCameraPanPixels(float dx, float dy)
    {
        lock (_sync)
        {
            ComposeOrbitEye(_orbitBaseTarget, _orbitPan, _debugFlyWorldOffset, _orbitYaw, _orbitPitch, _orbitDistance,
                out var eye, out var look);
            var forward = look - eye;
            var fl = forward.Length();
            if (fl < 1e-5f)
            {
                return;
            }

            forward /= fl;
            var worldUp = Vector3.UnitY;
            var right = Vector3.Normalize(Vector3.Cross(forward, worldUp));
            if (right.LengthSquared() < 1e-8f)
            {
                right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitZ));
            }

            var up = Vector3.Normalize(Vector3.Cross(right, forward));
            var scale = _camPanPerPx * MathF.Max(0.35f, _orbitDistance * 0.22f);
            _orbitPan += right * (-dx * scale) + up * (dy * scale);
        }
    }

    /// <param name="wheelNotches">Typically ±1 per click (after normalizing platform delta).</param>
    public void ApplyCameraZoom(float wheelNotches)
    {
        lock (_sync)
        {
            var f = MathF.Exp(-wheelNotches * _camZoomPerWheelStep * 0.18f);
            // Stay outside the unit cube (~0.5 extent) with margin so near-plane clipping does not shear faces.
            _orbitDistance = Math.Clamp(_orbitDistance * f, 1.05f, 120f);
        }
    }

    private void SyncOrbitFromSceneLocked(IRenderPreviewScene scene)
    {
        var cam = scene.Camera;
        _orbitBaseTarget = cam.Target;
        var toEye = cam.Position - cam.Target;
        var dist = toEye.Length();
        if (dist < 1e-4f)
        {
            _orbitYaw = MathF.PI * 0.22f;
            _orbitPitch = 0.32f;
        }
        else
        {
            var dir = toEye / dist;
            _orbitPitch = MathF.Asin(Math.Clamp(dir.Y, -1f, 1f));
            _orbitYaw = MathF.Atan2(dir.X, dir.Z);
        }

        _orbitDistance = Math.Clamp(_orbitBoomArmDistance, 1.05f, 120f);
        _orbitPan = Vector3.Zero;
        _debugFlyWorldOffset = Vector3.Zero;
        _orbitYawDefault = _orbitYaw;
        _orbitPitchDefault = _orbitPitch;
        _orbitDistanceDefault = _orbitDistance;
        _orbitPanDefault = _orbitPan;
    }

    /// <summary>
    /// Places the eye on a sphere of radius <paramref name="distance"/> around pivot <paramref name="baseTarget"/> +
    /// <paramref name="pan"/> using yaw/pitch (orbit drag updates those angles). <paramref name="debugFlyWorld"/>
    /// shifts the eye only so WASD changes viewpoint relative to that orbit geometry (moving the pivot with the same
    /// delta preserved radius and kills parallax until clipping).
    /// </summary>
    private static void ComposeOrbitEye(Vector3 baseTarget, Vector3 pan, Vector3 debugFlyWorld, float yaw, float pitch,
        float distance, out Vector3 eye, out Vector3 lookTarget)
    {
        lookTarget = baseTarget + pan;
        var cp = MathF.Cos(pitch);
        var sp = MathF.Sin(pitch);
        var sy = MathF.Sin(yaw);
        var cy = MathF.Cos(yaw);
        var offset = new Vector3(cp * sy, sp, cp * cy) * distance;
        eye = lookTarget + offset + debugFlyWorld;
    }

    /// <summary>Called from <see cref="AutoPBR.App.Controls.GlPbrPreviewControl.OnOpenGlInit"/> only.</summary>
    internal void GlInit(GlInterface glInterface)
    {
        lock (_sync)
        {
            _lastError = null;
            try
            {
                _gl = GL.GetApi(glInterface.GetProcAddress);
            }
            catch (Exception ex)
            {
                _lastError = ex.ToString();
                EmitDiagnostic("[3D preview] " + _lastError);
                return;
            }

            var gl = _gl;
            string versionStr;
            unsafe
            {
                var p = gl.GetString(StringName.Version);
                versionStr = p is null ? "(unknown)" : Marshal.PtrToStringUTF8((nint)p) ?? "(unknown)";
            }

            var useOpenGlEs = versionStr.Contains("OpenGL ES", StringComparison.OrdinalIgnoreCase);
            _program = new GlShaderProgram(gl, "genesis.vert", "genesis.frag", useOpenGlEs, out var err);
            if (!_program.IsValid)
            {
                _lastError = err ?? "Shader link failed.";
                EmitDiagnostic("[3D preview] " + _lastError);
                _program.Dispose();
                _program = null;
                return;
            }

            EmitDiagnostic(useOpenGlEs
                ? $"[3D preview] Context: {versionStr} (Genesis shader path, GLSL ES 3.0)."
                : $"[3D preview] Context: {versionStr} (Genesis shader path, GLSL 330 core).");

            // Genesis Shadows Phase 2: depth-only program + FBO shadow target. If either fails we keep
            // the main path running and just disable shadow sampling at draw time.
            // PHASE3-CSM hook: when cascades arrive, allocate N targets here (or one array texture).
            _shadowProgram = new GlShaderProgram(gl, "genesis_shadow.vert", "genesis_shadow.frag", useOpenGlEs,
                out var shadowErr);
            if (!_shadowProgram.IsValid)
            {
                EmitDiagnostic("[3D preview] Shadow program: " + (shadowErr ?? "link failed"));
                _shadowProgram.Dispose();
                _shadowProgram = null;
            }

            var shadowResolution = Math.Clamp(_settings.ShadowMapResolution, 256, 4096);
            try
            {
                _shadowTarget = new GlShadowMapTarget(gl, shadowResolution, useOpenGlEs);
                EmitDiagnostic(
                    $"[3D preview] Shadow map: {shadowResolution}x{shadowResolution}");
            }
            catch (Exception ex)
            {
                _shadowTarget = null;
                EmitDiagnostic("[3D preview] Shadow target init failed: " + ex.Message);
            }

            var nearest = true;
            _albedo = new GlTexture2D(gl, nearest);
            _normal = new GlTexture2D(gl, nearest);
            _spec = new GlTexture2D(gl, nearest);
            _height = new GlTexture2D(gl, nearest);
            _mesh = new GlMeshBuffer(gl);
            _groundMesh = new GlMeshBuffer(gl);
            var groundGeom = PreviewMeshFactory.CreatePreviewGroundPlane();
            _groundMesh.Upload(groundGeom.InterleavedVertices, groundGeom.Indices);

            _neutralNormal = new GlTexture2D(gl, nearest);
            _neutralNormal.UploadRgba(1, 1, [128, 128, 255, 255], nearest);
            _neutralSpec = new GlTexture2D(gl, nearest);
            _neutralSpec.UploadRgba(1, 1, [120, 60, 40, 255], nearest);
            _neutralHeight = new GlTexture2D(gl, nearest);
            _neutralHeight.UploadRgba(1, 1, [128, 128, 128, 255], nearest);

            _grassGroundAlbedo = new GlTexture2D(gl, nearest);
            _grassGroundReady = TryUploadGrassGroundTexture(gl);

            TryInitLineOverlay(gl, useOpenGlEs);
            TryInitSunBillboard(gl, useOpenGlEs);
            TryInitAtmosphere(gl, useOpenGlEs);
            InitEntitySkinningBoneUbo(gl);
            _gpuAlive = true;
            _materialDirty = true;
            _meshDirty = true;

            // Context init can complete after SetScene; re-derive orbit from the current scene so updated
            // PreviewCamera defaults (or first scene push) always match the GPU path.
            if (_scene is not null)
            {
                SyncOrbitFromSceneLocked(_scene);
                _orbitSyncedKind = _scene.SceneKind == PreviewSceneKind.ItemPlane
                    ? PreviewSceneKind.ItemPlane
                    : PreviewSceneKind.BlockCube;
            }

            _loggedMeshReady = false;
            _loggedZeroIndex = false;
        }
    }

    /// <summary>Called from <see cref="AutoPBR.App.Controls.GlPbrPreviewControl.OnOpenGlDeinit"/> only.</summary>
    internal void GlDeinit(GlInterface glInterface)
    {
        _ = glInterface;
        lock (_sync)
        {
            _gpuAlive = false;
            _mesh?.Dispose();
            _mesh = null;
            _groundMesh?.Dispose();
            _groundMesh = null;
            _grassGroundAlbedo?.Dispose();
            _grassGroundAlbedo = null;
            _neutralNormal?.Dispose();
            _neutralNormal = null;
            _neutralSpec?.Dispose();
            _neutralSpec = null;
            _neutralHeight?.Dispose();
            _neutralHeight = null;
            _grassGroundReady = false;
            _albedo?.Dispose();
            _albedo = null;
            _normal?.Dispose();
            _normal = null;
            _spec?.Dispose();
            _spec = null;
            _height?.Dispose();
            _height = null;
            _program?.Dispose();
            _program = null;
            _shadowProgram?.Dispose();
            _shadowProgram = null;
            _shadowTarget?.Dispose();
            _shadowTarget = null;
            DestroyAtmosphereResources();
            DestroySunBillboard();
            DestroyLineOverlay();
            if (_entityBoneUbo != 0)
            {
                _gl?.DeleteBuffer(_entityBoneUbo);
                _entityBoneUbo = 0;
            }

            _gl = null;
        }
    }

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
        // Always render to the full control backing surface; relying on GL_VIEWPORT from BeginDraw can become stale
        // during splitter drags, leaving unrendered bands when the preview column is resized.
        var vpX = 0;
        var vpY = 0;
        var vw = Math.Max(1, pixelWidth);
        var vh = Math.Max(1, pixelHeight);

        // Bind backbuffer first; if there's no scene we still want a clean clear so the host control is not stale.
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

        // When idle animation is off, clear spacing so the next enable always rebakes immediately (avoids one throttled skip).
        if (!settings.EnableEntityAnimation)
        {
            _lastEmulatedEntityRebakeRenderTime = double.NegativeInfinity;
        }

        var entityEmulatedPreview = IsEntityEmulatedPreview(blockModel);
        var entityRebakeCtx = blockModel?.EmulatedRebake;
        var entityEmulatedMaterialsOk = entityEmulatedPreview &&
            entityRebakeCtx is not null &&
            blockSlots is { Length: > 0 } &&
            blockModel!.Materials.Length == entityRebakeCtx.OrderedTextureZipPaths.Length;

        float entityEmulatedAnimClock = 0f;
        var entityEmulatedPauseEdge = false;
        // Keep in sync with TryBuildStaticMesh / GPU bone fill: renderTime * speed * amp (see TryRebakeMesh / TryFillEmulatedEntityBoneMatrices).
        // Must not depend on materials being ready; otherwise modelMatrix wobble uses a different phase than bones when amp != 1.
        if (entityEmulatedPreview && blockModel is not null && entityRebakeCtx is not null)
        {
            var speed = Math.Clamp(settings.EntityAnimationSpeed, 0f, 4f);
            var amp = Math.Clamp(settings.EntityAnimationAmplitude, 0f, 2f);
            var paused = settings.PauseEntityIdleAnimation;
            if (paused)
            {
                if (!_prevPauseEntityIdleAnimation)
                {
                    _frozenEntityIdleAnimClock = (float)(renderTime * speed * amp);
                }

                entityEmulatedAnimClock = _frozenEntityIdleAnimClock;
            }
            else
            {
                entityEmulatedAnimClock = (float)(renderTime * speed * amp);
            }

            entityEmulatedPauseEdge = paused != _prevPauseEntityIdleAnimation;
            _prevPauseEntityIdleAnimation = paused;
        }

        var uploadedLiveEntityAnim = false;
        // When entity animation is off, rebake to lifted IR bind pose (no setupAnim overlay). Initial pack
        // conversion bakes with setupAnim enabled; this path restores static pose until animation is re-enabled.
        if (entityEmulatedMaterialsOk &&
            blockModel is not null &&
            entityRebakeCtx is not null &&
            !settings.EnableEntityAnimation &&
            blockModel.GpuEntityBoneSkinning &&
            EntityEmulatedPreviewRebaker.TryRebakeMesh(
                entityRebakeCtx,
                blockModel.Materials,
                entityEmulatedAnimClock,
                out var revertVerts,
                out var revertIdx,
                out var revertBatches,
                applyGeometryIrSetupAnimMotion: false) &&
            revertVerts is not null &&
            revertIdx is not null &&
            revertBatches is not null)
        {
            entityRebakeCtx.GpuPreparedBoneCount = null;
            entityRebakeCtx.GpuBindPoseInverseLocalToParent = null;
            var lifted = PreviewSubjectPlacement.LiftSubjectIfClipping(new PreviewModelSubject
            {
                InterleavedVertices = revertVerts,
                Indices = revertIdx,
                DrawBatches = revertBatches,
                Materials = blockModel.Materials,
                PrimaryMaterialIndex = blockModel.PrimaryMaterialIndex,
                Sprite2DFoliageTarget = blockModel.Sprite2DFoliageTarget,
                EnableRenderTimeAnimation = blockModel.EnableRenderTimeAnimation,
                AnimationPreset = blockModel.AnimationPreset,
                EmulatedRebake = blockModel.EmulatedRebake,
                GpuEntityBoneSkinning = false,
                EntityGpuMeshSpaceLiftY = 0f,
            });
            blockModel = lifted;
            _mesh!.Upload(lifted.InterleavedVertices, lifted.Indices);
            uploadedLiveEntityAnim = true;
            lock (_sync)
            {
                _blockModelSubject = lifted;
                if (meshDirty)
                {
                    _meshDirty = false;
                }
            }

            EmitDiagnostic(
                $"[3D preview] Emulated entity CPU mesh (animation off): verts={lifted.InterleavedVertices.Length / PreviewMesh.FloatsPerVertex}, indices={lifted.Indices.Length}.");
        }
        else if (entityEmulatedMaterialsOk &&
                 settings.EnableEntityAnimation &&
                 blockModel is not null &&
                 entityRebakeCtx is not null)
        {
            var rebakeKey = $"{entityRebakeCtx.PackZipPath}\u001f{entityRebakeCtx.AssetArchivePath}";
            if (!string.Equals(rebakeKey, _emulatedRebakeSubjectKey, StringComparison.Ordinal))
            {
                _emulatedRebakeSubjectKey = rebakeKey;
                _lastEmulatedEntityRebakeRenderTime = double.NegativeInfinity;
            }

            if (meshDirty)
            {
                _emulatedGpuSkinPrepFailedKey = null;
            }

            var shouldTryGpuLayout = meshDirty ||
                (!blockModel.GpuEntityBoneSkinning &&
                 !string.Equals(rebakeKey, _emulatedGpuSkinPrepFailedKey, StringComparison.Ordinal));

            if (shouldTryGpuLayout &&
                EntityEmulatedPreviewRebaker.TryPrepareGpuSkinnedEmulatedMesh(
                    entityRebakeCtx,
                    blockModel.Materials,
                    PreviewStageConstants.GridWorldY,
                    0.002f,
                    out var gpuVerts,
                    out var gpuIdx,
                    out var gpuBatches,
                    out var gpuBoneCount,
                    out var gpuLift))
            {
                _emulatedGpuSkinPrepFailedKey = null;
                entityRebakeCtx.GpuPreparedBoneCount = gpuBoneCount;
                var liftedGpu = new PreviewModelSubject
                {
                    InterleavedVertices = gpuVerts!,
                    Indices = gpuIdx!,
                    DrawBatches = gpuBatches!,
                    Materials = blockModel.Materials,
                    PrimaryMaterialIndex = blockModel.PrimaryMaterialIndex,
                    Sprite2DFoliageTarget = blockModel.Sprite2DFoliageTarget,
                    EnableRenderTimeAnimation = blockModel.EnableRenderTimeAnimation,
                    AnimationPreset = blockModel.AnimationPreset,
                    EmulatedRebake = blockModel.EmulatedRebake,
                    GpuEntityBoneSkinning = true,
                    VertexStrideFloats = EntityEmulatedPreviewMeshLayout.SkinnedFloatsPerVertex,
                    EntityGpuMeshSpaceLiftY = gpuLift,
                };
                blockModel = liftedGpu;
                _mesh!.Upload(gpuVerts!, gpuIdx!, EntityEmulatedPreviewMeshLayout.SkinnedFloatsPerVertex);
                uploadedLiveEntityAnim = true;
                lock (_sync)
                {
                    _blockModelSubject = liftedGpu;
                    if (meshDirty)
                    {
                        _meshDirty = false;
                    }
                }

                EmitDiagnostic(
                    $"[3D preview] Emulated entity GPU skinned mesh: bones={gpuBoneCount}, verts={gpuVerts!.Length / EntityEmulatedPreviewMeshLayout.SkinnedFloatsPerVertex}, indices={gpuIdx!.Length}.");
            }
            else
            {
                if (shouldTryGpuLayout)
                {
                    _emulatedGpuSkinPrepFailedKey = rebakeKey;
                    entityRebakeCtx.GpuPreparedBoneCount = null;
                    entityRebakeCtx.GpuBindPoseInverseLocalToParent = null;
                }

                var needsCpuRebake = meshDirty ||
                    entityEmulatedPauseEdge ||
                    (renderTime - _lastEmulatedEntityRebakeRenderTime >= MinEmulatedEntityRebakeIntervalSeconds);
                if (needsCpuRebake &&
                    !blockModel.GpuEntityBoneSkinning &&
                    EntityEmulatedPreviewRebaker.TryRebakeMesh(
                        entityRebakeCtx,
                        blockModel.Materials,
                        entityEmulatedAnimClock,
                        out var rbVerts,
                        out var rbIdx,
                        out var rbBatches) &&
                    rbVerts is not null &&
                    rbIdx is not null &&
                    rbBatches is not null)
                {
                    var lifted = PreviewSubjectPlacement.LiftSubjectIfClipping(new PreviewModelSubject
                    {
                        InterleavedVertices = rbVerts,
                        Indices = rbIdx,
                        DrawBatches = rbBatches,
                        Materials = blockModel.Materials,
                        PrimaryMaterialIndex = blockModel.PrimaryMaterialIndex,
                        Sprite2DFoliageTarget = blockModel.Sprite2DFoliageTarget,
                        EnableRenderTimeAnimation = blockModel.EnableRenderTimeAnimation,
                        AnimationPreset = blockModel.AnimationPreset,
                        EmulatedRebake = blockModel.EmulatedRebake,
                        GpuEntityBoneSkinning = false,
                        EntityGpuMeshSpaceLiftY = 0f,
                    });
                    blockModel = lifted;
                    _mesh!.Upload(lifted.InterleavedVertices, lifted.Indices);
                    uploadedLiveEntityAnim = true;
                    _lastEmulatedEntityRebakeRenderTime = renderTime;
                    lock (_sync)
                    {
                        _blockModelSubject = lifted;
                        if (meshDirty)
                        {
                            _meshDirty = false;
                        }
                    }

                    EmitDiagnostic(
                        $"[3D preview] Emulated entity mesh rebake: verts={lifted.InterleavedVertices.Length / PreviewMesh.FloatsPerVertex}, indices={lifted.Indices.Length}.");
                }
            }
        }

        // Mesh upload must happen before either pass so depth-only and main pass share the same VBO.
        if (meshDirty && !uploadedLiveEntityAnim)
        {
            // Keep mesh dirty until we actually have geometry to upload; render can start a few frames
            // before scene meshes are ready, and clearing this flag too early leaves the GPU buffer empty.
            if (!settings.DrawPreviewSubject)
            {
                var empty = PreviewMeshFactory.CreateEmptySubjectPlaceholder();
                _mesh!.Upload(empty.InterleavedVertices, empty.Indices);
            }
            else if (blockModel is
                     {
                         GpuEntityBoneSkinning: true,
                         VertexStrideFloats: > 0,
                         InterleavedVertices.Length: > 0,
                         Indices.Length: > 0
                     } gpuSkinned
                     && gpuSkinned.InterleavedVertices.Length % gpuSkinned.VertexStrideFloats == 0)
            {
                // Scene meshes are always 12-float PreviewMesh; GPU-skinned entities use a wider stride (bone index).
                // Never upload the scene copy here or the VAO stride would desync from genesis.vert skinning.
                _mesh!.Upload(
                    gpuSkinned.InterleavedVertices,
                    gpuSkinned.Indices,
                    gpuSkinned.VertexStrideFloats);
                EmitDiagnostic(
                    $"[3D preview] Mesh upload: scene={scene.SceneKind}, sourceCount={scene.Meshes.Count}, verts={gpuSkinned.InterleavedVertices.Length / gpuSkinned.VertexStrideFloats}, indices={gpuSkinned.Indices.Length}, strideFloats={gpuSkinned.VertexStrideFloats} (GPU-skinned subject).");
            }
            else if (scene.Meshes.Count > 0)
            {
                var uploadMesh = scene.Meshes[0];
                _mesh!.Upload(uploadMesh.InterleavedVertices, uploadMesh.Indices);
                EmitDiagnostic(
                    $"[3D preview] Mesh upload: scene={scene.SceneKind}, sourceCount={scene.Meshes.Count}, verts={uploadMesh.VertexCount}, indices={uploadMesh.Indices.Length}.");
            }
            else
            {
                // Defensive fallback: if scene mesh population races with the first render frame,
                // synthesize a canonical mesh so preview never goes blank.
                var uploadMesh = scene.SceneKind == PreviewSceneKind.ItemPlane
                    ? (settings.SpritePlaneCount <= 1
                        ? PreviewMeshFactory.CreateItemPlane()
                        : PreviewMeshFactory.CreateSpritePlanes(
                            planeCount: Math.Clamp(settings.SpritePlaneCount, 1, 8)))
                    : PreviewMeshFactory.CreateUnitCube();
                EmitDiagnostic($"[3D preview] Fallback mesh upload used ({scene.SceneKind}).");
                _mesh!.Upload(uploadMesh.InterleavedVertices, uploadMesh.Indices);
            }

            lock (_sync)
            {
                _meshDirty = false;
            }
        }

        if (materialDirty)
        {
            if (blockModel is null || blockSlots is null)
            {
                UploadMaterial(gl, material, settings.NearestTextureFilter);
            }

            lock (_sync)
            {
                _materialDirty = false;
            }
        }

        var entityBoneSnapshotValid = false;
        var entityBoneSnapshotCount = 0;
        if (blockModel is { GpuEntityBoneSkinning: true, EmulatedRebake: { } ebBone } &&
            entityEmulatedMaterialsOk &&
            EntityEmulatedPreviewRebaker.TryFillEmulatedEntityBoneMatrices(
                ebBone,
                entityEmulatedAnimClock,
                _entityBoneScratch.AsSpan(),
                out entityBoneSnapshotCount))
        {
            entityBoneSnapshotValid = entityBoneSnapshotCount > 0;
        }

        if (entityBoneSnapshotValid &&
            blockModel?.GpuEntityBoneSkinning == true &&
            _entityBoneUbo != 0)
        {
            UploadEntitySkinningBoneMatrices(gl, entityBoneSnapshotCount);
        }

        // Compute the world-space light direction once; both the shadow ortho and the main pass use it.
        var worldLightDir = PreviewLightMath.LightDirectionFromYawPitch(
            settings.LightYawDegrees, settings.LightPitchDegrees);
        if (settings.EnableAtmosphericSky)
        {
            EnsureAtmosphereLuts(gl, worldLightDir, settings);
        }

        // Build orthographic light view-projection (covers the unit cube + max POM displacement).
        // Half-extent 1.5 covers a unit cube's diagonal; near/far chosen so the boom (scene-extent + margin)
        // sits centered. Boom is intentionally larger than 2.5 so near > 0 (depth precision).
        const float shadowOrthoHalfExtent = 1.5f;
        const float shadowBoom = 4.0f;
        const float shadowNear = shadowBoom - 2.5f;
        const float shadowFar = shadowBoom + 2.5f;
        var shadowTargetPos = Vector3.Zero;
        var shadowEye = shadowTargetPos - worldLightDir * shadowBoom;
        var shadowUp = PreviewLightMath.PickShadowViewUp(worldLightDir);
        var shadowView = PreviewGlMatrices.CreateLookAtRhOpenGlRowStorage(shadowEye, shadowTargetPos, shadowUp);
        var shadowProj = PreviewGlMatrices.CreateOrthographicOpenGlRowStorage(
            -shadowOrthoHalfExtent, shadowOrthoHalfExtent,
            -shadowOrthoHalfExtent, shadowOrthoHalfExtent,
            shadowNear, shadowFar);
        var shadowVp = shadowProj * shadowView;

        var entityAlphaModeUniform = entityEmulatedPreview ? (int)settings.EntityAlphaMode : 0;
        var entityBlendDraw =
            entityEmulatedPreview &&
            scene.SceneKind == PreviewSceneKind.BlockModel &&
            settings.EntityAlphaMode == PreviewEntityAlphaMode.Blend;
        var enableParallaxEff = PreviewEntityEmulatedShaderGating.EffectiveParallax(
            settings.EnableParallax, entityEmulatedPreview, settings.EnableEntityParallax);
        var enableParallaxAoEff = PreviewEntityEmulatedShaderGating.EffectiveParallaxAo(
            settings.EnableParallaxAo, entityEmulatedPreview, settings.EnableEntityParallax);
        var enableNormalMapEff = PreviewEntityEmulatedShaderGating.EffectiveNormalMap(
            settings.EnableNormalMap, entityEmulatedPreview, settings.EnableEntityLabPbrShading);
        var enableSpecularMapEff = PreviewEntityEmulatedShaderGating.EffectiveSpecularMap(
            settings.EnableSpecularMap, entityEmulatedPreview, settings.EnableEntityLabPbrShading);
        var enableParallaxShadowEff = PreviewEntityEmulatedShaderGating.EffectiveParallaxShadow(
            settings.EnableParallaxShadow, entityEmulatedPreview, settings.EnableEntityParallax);

        var modelMatrix = Matrix4x4.CreateRotationY((float)rotation);
        if (scene.SceneKind == PreviewSceneKind.ItemPlane)
        {
            modelMatrix = Matrix4x4.Identity;
        }
        // Legacy whole-mesh wobble (pre–setupAnim IR); opt-in via Render settings.
        else if (blockModel?.EnableRenderTimeAnimation == true &&
                 settings.EnableEntityAnimation &&
                 settings.EnableLegacyEntityWobble &&
                 string.Equals(blockModel.AnimationPreset, "entity_emulated", StringComparison.Ordinal))
        {
            var animT = entityEmulatedAnimClock;
            var amp = Math.Clamp(settings.EntityAnimationAmplitude, 0f, 2f);
            var bob = Matrix4x4.CreateTranslation(0f, MathF.Sin(animT * 2.2f) * (0.035f * amp), 0f);
            var yaw = Matrix4x4.CreateRotationY(MathF.Sin(animT * 0.9f) * (0.22f * amp));
            var roll = Matrix4x4.CreateRotationZ(MathF.Sin(animT * 1.6f) * (0.03f * amp));
            modelMatrix = roll * yaw * bob * modelMatrix;
        }

        // Shadow depth pre-pass (Phase 2). Skips line overlays so debug grid/axes never cast shadows.
        var shadowAvailable = settings.EnableShadows && _shadowProgram?.IsValid == true && _shadowTarget is not null;
        if (shadowAvailable)
        {
            _shadowTarget!.BeginShadowPass();
            gl.Enable(EnableCap.DepthTest);
            gl.DepthFunc(GLEnum.Lequal);
            gl.DepthMask(true);
            // Cull front faces during the depth pass to reduce self-shadow acne on solid casters; for
            // alpha-cut planes (sprite mode) we leave culling off so both sides cast.
            if (ShouldCullSolidBackFaces(scene.SceneKind, blockModel))
            {
                gl.Enable(EnableCap.CullFace);
                gl.CullFace(GLEnum.Front);
                gl.FrontFace(GLEnum.Ccw);
            }
            else
            {
                gl.Disable(EnableCap.CullFace);
            }

            _shadowProgram!.Use();
            SetMatrixOnProgram(_shadowProgram, "uLightViewProj", shadowVp);
            SetMatrixOnProgram(_shadowProgram, "uModel", Matrix4x4.Identity);
            SetIntOnProgram(_shadowProgram, "uSceneKind", 0);
            SetIntOnProgram(_shadowProgram, "uEntityAlphaMode", 0);
            UploadEntitySkinningUboTail(gl, 0, 0, 0f);
            _groundMesh!.Draw();
            if (settings.DrawPreviewSubject && _mesh.IndexCount > 0)
            {
                SetMatrixOnProgram(_shadowProgram, "uModel", modelMatrix);
                if (scene.SceneKind == PreviewSceneKind.ItemPlane)
                {
                    UploadEntitySkinningUboTail(gl, 0, 0, 0f);
                    SetIntOnProgram(_shadowProgram, "uSceneKind", 1);
                    SetIntOnProgram(_shadowProgram, "uEntityAlphaMode", 0);
                    SetFloatOnProgram(_shadowProgram, "uAlphaCutoff", settings.AlphaCutoff);
                    SetIntOnProgram(_shadowProgram, "uItemAlphaBlend", settings.ItemUseAlphaBlend ? 1 : 0);
                    gl.ActiveTexture(TextureUnit.Texture0);
                    _albedo!.Bind(0);
                    SetIntOnProgram(_shadowProgram, "uAlbedo", 0);
                }
                else
                {
                    SetIntOnProgram(_shadowProgram, "uSceneKind", 0);
                }

                if (blockModel is not null && blockSlots is { Length: > 0 })
                {
                    if (entityAlphaModeUniform != 0)
                    {
                        SetFloatOnProgram(_shadowProgram, "uAlphaCutoff", settings.AlphaCutoff);
                    }

                    SetIntOnProgram(_shadowProgram, "uEntityAlphaMode", entityAlphaModeUniform);
                    ApplyEntityBoneSkinningUboTail(
                        gl,
                        blockModel,
                        blockModel.EntityGpuMeshSpaceLiftY,
                        entityBoneSnapshotValid,
                        entityBoneSnapshotCount);
                    foreach (var batch in blockModel.DrawBatches)
                    {
                        if ((uint)batch.MaterialIndex >= (uint)blockSlots.Length)
                        {
                            continue;
                        }

                        UploadMaterial(gl, blockSlots[batch.MaterialIndex], settings.NearestTextureFilter);
                        gl.ActiveTexture(TextureUnit.Texture0);
                        _albedo!.Bind(0);
                        SetIntOnProgram(_shadowProgram, "uAlbedo", 0);
                        _mesh.DrawRange(batch.FirstIndex, batch.IndexCount);
                    }
                }
                else
                {
                    SetIntOnProgram(_shadowProgram, "uEntityAlphaMode", 0);
                    UploadEntitySkinningUboTail(gl, 0, 0, 0f);
                    _mesh.Draw();
                }
            }

            _shadowTarget.EndShadowPass();
        }

        // Restore main-pass framebuffer + viewport (BeginShadowPass snapshots & EndShadowPass restores
        // the GL viewport, but binding our actual default FBO again is cheap and explicit).
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
        gl.Enable(EnableCap.DepthTest);
        gl.DepthFunc(GLEnum.Lequal);
        gl.DepthMask(true);
        if (ShouldCullSolidBackFaces(scene.SceneKind, blockModel))
        {
            gl.Enable(EnableCap.CullFace);
            gl.CullFace(GLEnum.Back);
            gl.FrontFace(GLEnum.Ccw);
        }
        else
        {
            gl.Disable(EnableCap.CullFace);
        }

        var drewAtmosphereSky = false;
        if (settings.EnableAtmosphericSky && _atmoLutsValid && _atmoSkyProgram?.IsValid == true && _atmoSkyViewTex != 0)
        {
            gl.Disable(EnableCap.DepthTest);
            gl.DepthMask(false);
            DrawAtmosphereSky(gl, worldLightDir, settings);
            gl.DepthMask(true);
            drewAtmosphereSky = true;
        }

        if (drewAtmosphereSky)
        {
            gl.Clear(ClearBufferMask.DepthBufferBit);
        }
        else
        {
            gl.ClearColor(0.12f, 0.12f, 0.14f, 1f);
            gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        }

        var cam = scene.Camera;
        ComposeOrbitEye(orbitBaseTarget, orbitPan, debugFlyWorldOffset, orbitYaw, orbitPitch, orbitDistance,
            out var eye, out var lookTarget);
        var aspect = vw / (float)vh;
        var proj = PreviewGlMatrices.CreatePerspectiveFieldOfViewOpenGl(
            cam.FieldOfViewDegrees * (MathF.PI / 180f),
            aspect,
            cam.NearPlane,
            cam.FarPlane);
        var view = PreviewGlMatrices.CreateLookAtRhOpenGlRowStorage(eye, lookTarget, Vector3.UnitY);

        // Genesis Shadows Phase 2 routes the user-controlled yaw/pitch through PreviewLightMath so the
        // shadow ortho frustum and the shaded direct lighting agree on direction. The scene.Light.Direction
        // is kept as a fallback when the helper produces a degenerate vector.
        var lightDir = worldLightDir;
        if (lightDir.LengthSquared() < 1e-8f)
        {
            lightDir = scene.Light.Direction.LengthSquared() < 1e-8f
                ? new Vector3(-0.35f, -0.85f, -0.4f)
                : Vector3.Normalize(scene.Light.Direction);
        }

        // Sun billboard: draw before opaque geometry so depth testing hides it behind the cube/grid while
        // the atmosphere sky (drawn earlier without depth) stays behind the sun.
        gl.Enable(EnableCap.DepthTest);
        gl.DepthFunc(GLEnum.Lequal);
        DrawSunBillboard(gl, proj, view, eye, lightDir, ShouldCullSolidBackFaces(scene.SceneKind, blockModel));

        _program.Use();
        SetMatrix("uView", view);
        SetMatrix("uProj", proj);
        SetMatrix("uLightViewProj", shadowVp);

        SetVec3("uCameraPos", eye);
        SetVec3("uLightDir", lightDir);
        SetVec3("uLightColor", scene.Light.Color);
        SetFloat("uAmbient", settings.AmbientStrength);
        SetFloat("uNormalStrength", settings.NormalStrength);
        SetFloat("uHeightStrength", settings.HeightStrength);
        SetFloat("uSpecularStrength", settings.SpecularStrength);
        SetFloat("uRoughnessScale", settings.RoughnessScale);
        SetFloat("uExposure", settings.Exposure);
        SetFloat("uParallaxAoStrength", settings.ParallaxAoStrength);
        SetInt("uEnableParallax", enableParallaxEff ? 1 : 0);
        SetInt("uEnableParallaxAo", enableParallaxAoEff ? 1 : 0);
        SetInt("uEnableNormalMap", enableNormalMapEff ? 1 : 0);
        SetInt("uEnableSpecularMap", enableSpecularMapEff ? 1 : 0);
        SetInt("uSceneKind", scene.SceneKind == PreviewSceneKind.ItemPlane ? 1 : 0);
        SetFloat("uAlphaCutoff", settings.AlphaCutoff);
        SetInt("uItemAlphaBlend", settings.ItemUseAlphaBlend ? 1 : 0);
        SetInt("uEntityAlphaMode", 0);

        // Genesis-specific uniforms.
        SetInt("uEnableSss", settings.EnableSss ? 1 : 0);
        SetInt("uEnableParallaxShadow", enableParallaxShadowEff ? 1 : 0);
        SetInt("uEnableIbl", settings.EnableIbl ? 1 : 0);
        SetFloat("uSssStrength", settings.SssStrength);
        SetFloat("uIblStrength", settings.IblStrength);
        SetFloat("uEmissionStrength", settings.EmissionStrength);
        SetInt("uEnableAtmosphericSky", settings.EnableAtmosphericSky ? 1 : 0);
        SetFloat("uAtmosphereTurbidity", settings.AtmosphereTurbidity);
        SetFloat("uAtmosphereSunIntensity", settings.AtmosphereSunIntensity);
        SetFloat("uAtmosphereHorizonFalloff", settings.AtmosphereHorizonFalloff);
        // Soft neutral sky/ground tints; future plan can expose these as user settings.
        SetVec3("uSkyTint", new Vector3(0.55f, 0.62f, 0.74f));
        SetVec3("uGroundTint", new Vector3(0.22f, 0.20f, 0.18f));

        // Directional shadow map (Genesis Shadows Phase 2). Bound to texture unit 4.
        var shadowEnabledForShader = shadowAvailable;
        SetInt("uEnableShadowMap", shadowEnabledForShader ? 1 : 0);
        SetFloat("uShadowMinBias", settings.ShadowMinBias);
        SetFloat("uShadowMaxBias", settings.ShadowMaxBias);
        var shadowRes = _shadowTarget?.Resolution ?? Math.Clamp(settings.ShadowMapResolution, 256, 4096);
        SetVec2("uShadowTexelSize", new Vector2(1f / shadowRes, 1f / shadowRes));
        if (_shadowTarget is not null)
        {
            gl.ActiveTexture(TextureUnit.Texture4);
            gl.BindTexture(TextureTarget.Texture2D, _shadowTarget.DepthTextureHandle);
            SetInt("uShadowMap", 4);
        }

        // Tinted vanilla grass plane sits under the grid; one texture tile per world unit (nearest + repeat).
        if (_grassGroundReady && _grassGroundAlbedo is not null && _groundMesh!.IndexCount > 0)
        {
            var restoreCull = gl.IsEnabled(EnableCap.CullFace);
            gl.Disable(EnableCap.CullFace);
            SetMatrix("uModel", Matrix4x4.Identity);
            SetInt("uEnableParallax", 0);
            SetInt("uEnableNormalMap", 0);
            SetInt("uEnableSpecularMap", 0);
            SetInt("uSceneKind", 0);
            SetInt("uEntityAlphaMode", 0);
            UploadEntitySkinningUboTail(gl, 0, 0, 0f);
            SetInt("uHasNormal", 0);
            SetInt("uHasSpecular", 0);
            SetInt("uHasHeight", 0);
            _grassGroundAlbedo.Bind(0);
            _neutralNormal!.Bind(1);
            _neutralSpec!.Bind(2);
            _neutralHeight!.Bind(3);
            SetInt("uAlbedo", 0);
            SetInt("uNormal", 1);
            SetInt("uSpecular", 2);
            SetInt("uHeight", 3);
            _groundMesh.Draw();
            if (restoreCull)
            {
                gl.Enable(EnableCap.CullFace);
            }
        }

        if (settings.ShowBackgroundGrid && _lineProgram?.IsValid == true &&
            _gridVertexCount > 0)
        {
            DrawBackgroundGrid(gl, proj, view);
            // DrawBackgroundGrid binds the line program; restore main material program before mesh uniforms.
            _program.Use();
        }

        SetMatrix("uModel", modelMatrix);
        SetInt("uEnableParallax", enableParallaxEff ? 1 : 0);
        SetInt("uEnableNormalMap", enableNormalMapEff ? 1 : 0);
        SetInt("uEnableSpecularMap", enableSpecularMapEff ? 1 : 0);
        SetInt("uSceneKind", scene.SceneKind == PreviewSceneKind.ItemPlane ? 1 : 0);
        SetInt("uEntityAlphaMode", 0);
        if (_atmoSkyViewTex != 0)
        {
            gl.ActiveTexture(TextureUnit.Texture5);
            gl.BindTexture(TextureTarget.Texture2D, _atmoSkyViewTex);
        }

        SetInt("uAtmoSkyViewLut", 5);

        if (!settings.DrawPreviewSubject || _mesh.IndexCount <= 0)
        {
            if (!_loggedZeroIndex && settings.DrawPreviewSubject && _mesh.IndexCount <= 0)
            {
                EmitDiagnostic(
                    $"[3D preview] Draw skipped: index buffer empty (scene={scene.SceneKind}, sceneMeshCount={scene.Meshes.Count}, meshDirty={meshDirty}).");
                _loggedZeroIndex = true;
            }
        }
        else if (blockModel is not null && blockSlots is { Length: > 0 })
        {
            if (!_loggedMeshReady)
            {
                EmitDiagnostic(
                    $"[3D preview] Draw ready: indexCount={_mesh.IndexCount}, scene={scene.SceneKind}, lightYaw={settings.LightYawDegrees:F1}, lightPitch={settings.LightPitchDegrees:F1}.");
                _loggedMeshReady = true;
            }

            SetInt("uEntityAlphaMode", entityAlphaModeUniform);
            var blendWasEnabled = false;
            if (entityBlendDraw)
            {
                blendWasEnabled = gl.IsEnabled(EnableCap.Blend);
                gl.Enable(EnableCap.Blend);
                gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            }

            ApplyEntityBoneSkinningUboTail(
                gl,
                blockModel,
                blockModel.EntityGpuMeshSpaceLiftY,
                entityBoneSnapshotValid,
                entityBoneSnapshotCount);
            foreach (var batch in blockModel.DrawBatches)
            {
                if ((uint)batch.MaterialIndex >= (uint)blockSlots.Length)
                {
                    continue;
                }

                var slot = blockSlots[batch.MaterialIndex];
                UploadMaterial(gl, slot, settings.NearestTextureFilter);
                var bHasN = slot.NormalRgba is { Length: > 0 };
                var bHasS = slot.SpecularRgba is { Length: > 0 };
                var bHasH = slot.HeightRgba is { Length: > 0 };
                SetInt("uHasNormal", bHasN ? 1 : 0);
                SetInt("uHasSpecular", bHasS ? 1 : 0);
                SetInt("uHasHeight", bHasH ? 1 : 0);
                _albedo.Bind(0);
                _normal.Bind(1);
                _spec.Bind(2);
                _height.Bind(3);
                SetInt("uAlbedo", 0);
                SetInt("uNormal", 1);
                SetInt("uSpecular", 2);
                SetInt("uHeight", 3);
                _mesh.DrawRange(batch.FirstIndex, batch.IndexCount);
            }

            if (entityBlendDraw && !blendWasEnabled)
            {
                gl.Disable(EnableCap.Blend);
            }
        }
        else
        {
            var hasN = material?.NormalRgba is { Length: > 0 };
            var hasS = material?.SpecularRgba is { Length: > 0 };
            var hasH = material?.HeightRgba is { Length: > 0 };
            SetInt("uHasNormal", hasN ? 1 : 0);
            SetInt("uHasSpecular", hasS ? 1 : 0);
            SetInt("uHasHeight", hasH ? 1 : 0);
            _albedo.Bind(0);
            _normal.Bind(1);
            _spec.Bind(2);
            _height.Bind(3);
            SetInt("uAlbedo", 0);
            SetInt("uNormal", 1);
            SetInt("uSpecular", 2);
            SetInt("uHeight", 3);
            if (!_loggedMeshReady)
            {
                EmitDiagnostic(
                    $"[3D preview] Draw ready: indexCount={_mesh.IndexCount}, scene={scene.SceneKind}, lightYaw={settings.LightYawDegrees:F1}, lightPitch={settings.LightPitchDegrees:F1}.");
                _loggedMeshReady = true;
            }

            UploadEntitySkinningUboTail(gl, 0, 0, 0f);
            _mesh.Draw();
        }

        if (settings.ShowCornerAxes && _lineProgram?.IsValid == true)
        {
            DrawCornerAxes(gl, vpX, vpY, vw, vh, proj, view);
        }
    }

    private void SetMatrix(string name, Matrix4x4 m)
    {
        var loc = _program!.GetUniformLocation(name);
        if (loc < 0)
        {
            return;
        }

        var mt = Matrix4x4.Transpose(m);
        _gl!.UniformMatrix4(loc, 1, false, in mt.M11);
    }

    private void SetVec2(string name, Vector2 v)
    {
        var loc = _program!.GetUniformLocation(name);
        if (loc >= 0)
        {
            _gl!.Uniform2(loc, v.X, v.Y);
        }
    }

    private void SetVec3(string name, Vector3 v)
    {
        var loc = _program!.GetUniformLocation(name);
        if (loc >= 0)
        {
            _gl!.Uniform3(loc, v.X, v.Y, v.Z);
        }
    }

    private void SetFloat(string name, float v)
    {
        var loc = _program!.GetUniformLocation(name);
        if (loc >= 0)
        {
            _gl!.Uniform1(loc, v);
        }
    }

    private void SetInt(string name, int v)
    {
        var loc = _program!.GetUniformLocation(name);
        if (loc >= 0)
        {
            _gl!.Uniform1(loc, v);
        }
    }

    private static PreviewRenderSettings CloneSettings(PreviewRenderSettings s) => new()
    {
        NormalStrength = s.NormalStrength,
        HeightStrength = s.HeightStrength,
        SpecularStrength = s.SpecularStrength,
        RoughnessScale = s.RoughnessScale,
        AmbientStrength = s.AmbientStrength,
        Exposure = s.Exposure,
        EnableParallax = s.EnableParallax,
        EnableNormalMap = s.EnableNormalMap,
        EnableSpecularMap = s.EnableSpecularMap,
        AutoRotate = s.AutoRotate,
        LightYawDegrees = s.LightYawDegrees,
        LightPitchDegrees = s.LightPitchDegrees,
        NearestTextureFilter = s.NearestTextureFilter,
        AlphaCutoff = s.AlphaCutoff,
        ItemUseAlphaBlend = s.ItemUseAlphaBlend,
        EntityAlphaMode = s.EntityAlphaMode,
        EnableEntityLabPbrShading = s.EnableEntityLabPbrShading,
        EnableEntityParallax = s.EnableEntityParallax,
        SpritePlaneCount = s.SpritePlaneCount,
        ShowBackgroundGrid = s.ShowBackgroundGrid,
        ShowCornerAxes = s.ShowCornerAxes,
        DrawPreviewSubject = s.DrawPreviewSubject,
        EnableSss = s.EnableSss,
        EnableParallaxShadow = s.EnableParallaxShadow,
        EnableParallaxAo = s.EnableParallaxAo,
        ParallaxAoStrength = s.ParallaxAoStrength,
        EnableIbl = s.EnableIbl,
        EnableAtmosphericSky = s.EnableAtmosphericSky,
        AtmosphereTurbidity = s.AtmosphereTurbidity,
        AtmosphereSunIntensity = s.AtmosphereSunIntensity,
        AtmosphereHorizonFalloff = s.AtmosphereHorizonFalloff,
        SssStrength = s.SssStrength,
        IblStrength = s.IblStrength,
        EmissionStrength = s.EmissionStrength,
        EntityAnimationSpeed = s.EntityAnimationSpeed,
        EntityAnimationAmplitude = s.EntityAnimationAmplitude,
        EnableEntityAnimation = s.EnableEntityAnimation,
        PauseEntityIdleAnimation = s.PauseEntityIdleAnimation,
        EnableLegacyEntityWobble = s.EnableLegacyEntityWobble,
        EnableShadows = s.EnableShadows,
        ShadowMapResolution = s.ShadowMapResolution,
        ShadowMinBias = s.ShadowMinBias,
        ShadowMaxBias = s.ShadowMaxBias,
        EnableShadowCascades = s.EnableShadowCascades
    };

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        lock (_sync)
        {
            _gpuAlive = false;
        }
    }
}
