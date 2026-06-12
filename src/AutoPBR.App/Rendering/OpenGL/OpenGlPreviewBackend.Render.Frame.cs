using System.Numerics;

using AutoPBR.App.Rendering.Abstractions;
using AutoPBR.Core.Models;

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
        public bool FlyCamActive;
        public Vector3 FlyPosition;
        public float FlyYaw;
        public float FlyPitch;
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
        public bool EntityBonePaletteUploaded;
        public Vector3 WorldLightDir;
        public Matrix4x4 ShadowVp;
        public Matrix4x4 ShadowVpNear;
        public bool ShadowCascadesActive;
        public float CascadeSplitWorldDistance;
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
        public bool GodRayCaptureActive;
        public bool VolumeFroxelsReady;
        public double LastVolumeInjectMs;
        public double LastVolumeIntegrateMs;
    }
}
