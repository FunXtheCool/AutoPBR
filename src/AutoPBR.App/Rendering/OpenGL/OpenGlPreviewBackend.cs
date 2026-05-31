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
    /// <summary>Last emulated subject that received a successful animation-off IR bind-pose CPU rebake.</summary>
    private string? _entityBindPoseCommittedKey;

    private readonly Matrix4x4[] _entityBoneScratch = new Matrix4x4[EntityGpuSkinningLimits.MaxBones];
    /// <summary>std140: 64 mat4 (row-major source floats interpreted as column-major in GLSL) + 16 B tail scalars.</summary>
    private const int EntitySkinningUboMatrixBytes = EntityGpuSkinningLimits.MaxBones * 64;

    private const int EntitySkinningUboTotalBytes = EntitySkinningUboMatrixBytes;

    private readonly byte[] _entitySkinningUboScratch = new byte[EntitySkinningUboTotalBytes];
    private uint _entityBoneUbo;

    private const uint EntitySkinningUboBindingPoint = 2;

    private readonly record struct EntitySkinningUniformLocs(
        int PreviewSpaceVerts,
        int BindMesh,
        int GpuSkinning,
        int BoneCount,
        int MeshLiftY)
    {
        public bool IsComplete =>
            PreviewSpaceVerts >= 0 && BindMesh >= 0 && GpuSkinning >= 0 && BoneCount >= 0 && MeshLiftY >= 0;
    }

    private EntitySkinningUniformLocs _mainEntityUniformLocs;
    private EntitySkinningUniformLocs _shadowEntityUniformLocs;
    private bool _loggedEntityShaderInit;
    private bool _entityBoneUboBlockBoundMain;
    private bool _entityBoneUboBlockBoundShadow;

    private bool _disposed;
    private string? _lastError;
    private bool _gpuAlive;
    private Action<string>? _diagnosticLog;
    private bool _loggedMeshReady;
    private bool _loggedZeroIndex;
    private int _lastMeshUploadStride = PreviewMesh.FloatsPerVertex;
    private string? _entityBindPosePrepDiagKey;

    /// <summary>Orbit camera is re-synced from the scene only when this changes (block vs item), so texture swaps keep user framing.</summary>
    private string? _orbitSyncedKey;

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
            var orbitKey = ResolveOrbitSyncKey(scene, _blockModelSubject);
            var reseedOrbit = _orbitSyncedKey is null || !string.Equals(_orbitSyncedKey, orbitKey, StringComparison.Ordinal);
            if (reseedOrbit)
            {
                SyncOrbitFromSceneLocked(scene);
                _orbitSyncedKey = orbitKey;
            }

            _scene = scene;
            _meshDirty = true;
        }
    }

    private static string ResolveOrbitSyncKey(IRenderPreviewScene scene, PreviewModelSubject? blockModel)
    {
        if (scene.SceneKind == PreviewSceneKind.ItemPlane)
        {
            return "item_plane";
        }

        if (blockModel?.EmulatedRebake?.AssetArchivePath is { Length: > 0 } entityPath)
        {
            return "entity:" + entityPath.Replace('\\', '/').TrimStart('/');
        }

        return scene.SceneKind == PreviewSceneKind.BlockModel ? "block_model" : "block_cube";
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

    private static bool IsGpuSkinnedEntitySubject(PreviewModelSubject? subject) =>
        subject is
        {
            GpuEntityBoneSkinning: true,
            VertexStrideFloats: EntityEmulatedPreviewMeshLayout.SkinnedFloatsPerVertex,
            EmulatedRebake: not null
        };

    private static bool SameEntityPreviewAsset(PreviewModelSubject? a, PreviewModelSubject? b)
    {
        var pa = a?.EmulatedRebake?.AssetArchivePath;
        var pb = b?.EmulatedRebake?.AssetArchivePath;
        return pa is not null && pb is not null &&
               string.Equals(
                   pa.Replace('\\', '/').TrimStart('/'),
                   pb.Replace('\\', '/').TrimStart('/'),
                   StringComparison.OrdinalIgnoreCase);
    }

    private static bool BlockModelGeometryChanged(PreviewModelSubject? prev, PreviewModelSubject? next)
    {
        if (ReferenceEquals(prev, next))
        {
            return false;
        }

        if (prev is null || next is null)
        {
            return true;
        }

        return !ReferenceEquals(prev.InterleavedVertices, next.InterleavedVertices) ||
               !ReferenceEquals(prev.Indices, next.Indices) ||
               prev.VertexStrideFloats != next.VertexStrideFloats ||
               prev.GpuEntityBoneSkinning != next.GpuEntityBoneSkinning ||
               !SameEntityPreviewAsset(prev, next);
    }

    private void RecordMeshUpload(int strideFloats)
    {
        _lastMeshUploadStride = strideFloats;
    }

    private void UploadPreviewMesh(float[] interleavedVertices, uint[] indices, int strideFloats = PreviewMesh.FloatsPerVertex)
    {
        _mesh!.Upload(interleavedVertices, indices, strideFloats);
        RecordMeshUpload(strideFloats);
    }

    /// <summary>Multi-material Java block model preview. Cleared by <see cref="SetMaterial"/>.</summary>
    public void SetBlockModelPreview(PreviewModelSubject? subject, PreviewMaterial[]? slotMaterials)
    {
        lock (_sync)
        {
            var prev = _blockModelSubject;
            var prevPath = prev?.EmulatedRebake?.AssetArchivePath;
            var nextPath = subject?.EmulatedRebake?.AssetArchivePath;
            if (!string.Equals(prevPath, nextPath, StringComparison.OrdinalIgnoreCase) &&
                subject?.EmulatedRebake is { } rebake)
            {
                rebake.GpuPreparedBoneCount = null;
                rebake.GpuBindPoseInverseLocalToParent = null;
                rebake.GpuBindPoseBonePalette = null;
                rebake.GpuBindPoseInterleavedVertices = null;
                rebake.GpuBoneDispatchRoute = null;
                _emulatedGpuSkinPrepFailedKey = null;
                _emulatedRebakeSubjectKey = null;
                _entityBindPoseCommittedKey = null;
                _entityBindPosePrepDiagKey = null;
                _parityPlacementDiagKey = null;
                _entityCpuRebakeDiagKey = null;
                ResetEntityGpuRuntimeDiagState();
            }

            // UI re-pushes pack-converter CPU mesh every settings tick; keep the GPU bind VBO subject instead.
            if (subject is not null &&
                prev is not null &&
                IsGpuSkinnedEntitySubject(prev) &&
                SameEntityPreviewAsset(prev, subject) &&
                !IsGpuSkinnedEntitySubject(subject))
            {
                subject = prev;
            }

            _blockModelSubject = subject;
            _blockModelSlots = slotMaterials;
            _prevPauseEntityIdleAnimation = false;
            if (subject is not null && slotMaterials is { Length: > 0 })
            {
                var pi = Math.Clamp(subject.PrimaryMaterialIndex, 0, slotMaterials.Length - 1);
                _material = slotMaterials[pi];
            }

            _materialDirty = true;
            if (BlockModelGeometryChanged(prev, subject))
            {
                _meshDirty = true;
            }

            if (_scene is not null)
            {
                var orbitKey = ResolveOrbitSyncKey(_scene, subject);
                if (_orbitSyncedKey is null || !string.Equals(_orbitSyncedKey, orbitKey, StringComparison.Ordinal))
                {
                    SyncOrbitFromSceneLocked(_scene);
                    _orbitSyncedKey = orbitKey;
                }
            }
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
                _settings.ForceEntityCpuSkinning != prev.ForceEntityCpuSkinning ||
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

            if (!_settings.EnableEntityAnimation && prev.EnableEntityAnimation)
            {
                _entityBindPoseCommittedKey = null;
            }

            if (!_settings.ForceEntityCpuSkinning && prev.ForceEntityCpuSkinning)
            {
                _entityBindPoseCommittedKey = null;
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
        ForceEntityCpuSkinning = s.ForceEntityCpuSkinning,
        EnableShadows = s.EnableShadows,
        ShadowMapResolution = s.ShadowMapResolution,
        ShadowMinBias = s.ShadowMinBias,
        ShadowMaxBias = s.ShadowMaxBias,
        EnableShadowCascades = s.EnableShadowCascades
    };
}
