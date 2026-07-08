namespace AutoPBR.Preview;

/// <summary>Caps for emulated-entity GPU bone skinning (vertex shader uniform array size).</summary>
public static class EntityGpuSkinningLimits
{
    /// <summary>Conservative cap for GLES3-class <c>MAX_VERTEX_UNIFORM_COMPONENTS</c> (mat4 = 16 components each).</summary>
    public const int MaxBones = 64;
}
