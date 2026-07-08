namespace AutoPBR.Contracts.GeometryIr;

// ReSharper disable UnusedMember.Global — shared vocabulary for lift tooling and future parity checks.

/// <summary>Geometry IR v2 <c>liftKind</c> values on cuboids.</summary>
public static class GeometryIrLiftKinds
{
    public const string Exact = "exact";
    public const string DirectionMaskFullBox = "direction_mask_full_box";
    public const string TexCropStatic = "tex_crop_static";
    public const string Unknown = "unknown";
}

/// <summary>Machine-readable lift warning codes on cuboids and poses.</summary>
public static class GeometryIrLiftWarningCodes
{
    public const string DirectionMaskUnparsedSet = "direction_mask_unparsed_set";
    public const string CubeDeformationObfInferred = "cube_deformation_obf_inferred";
    public const string UnknownFloadZeroed = "unknown_fload_zeroed";
    public const string MathNonConstant = "math_non_constant";
    public const string StaticFieldInferred = "static_field_inferred";
    public const string ObfFactoryInferred = "obf_factory_inferred";
    public const string UnsupportedEulerOrder = "unsupported_euler_order";
}
