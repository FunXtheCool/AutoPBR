using AutoPBR.App.Rendering.Abstractions;

namespace AutoPBR.App.Rendering.OpenGL;

internal sealed record GenesisMaterialTextureArrayPlan(
    int Width,
    int Height,
    int Layers,
    PreviewMaterialContentKey.Value[] SlotKeys)
{
    public const int MaxLayers = 2048;

    public static bool TryCreate(
        IReadOnlyList<PreviewMaterial> slots,
        int maxTextureArrayLayers,
        out GenesisMaterialTextureArrayPlan? plan,
        out string reason)
    {
        plan = null;
        reason = string.Empty;
        if (slots.Count <= 0)
        {
            reason = "no material slots";
            return false;
        }

        var layerLimit = Math.Min(MaxLayers, Math.Max(1, maxTextureArrayLayers));
        if (slots.Count > layerLimit)
        {
            reason = $"slot count {slots.Count} exceeds texture-array layer limit {layerLimit}";
            return false;
        }

        var first = slots[0];
        var width = Math.Max(1, first.Width);
        var height = Math.Max(1, first.Height);
        if (!HasValidAlbedo(first, width, height))
        {
            reason = "slot 0 has invalid albedo dimensions";
            return false;
        }

        var keys = new PreviewMaterialContentKey.Value[slots.Count];
        for (var i = 0; i < slots.Count; i++)
        {
            var slot = slots[i];
            if (Math.Max(1, slot.Width) != width || Math.Max(1, slot.Height) != height)
            {
                reason = $"slot {i} dimensions {slot.Width}x{slot.Height} differ from {width}x{height}";
                return false;
            }

            if (!HasValidAlbedo(slot, width, height))
            {
                reason = $"slot {i} has invalid albedo dimensions";
                return false;
            }

            if (!OptionalMapIsCompatible(slot.NormalRgba, width, height) ||
                !OptionalMapIsCompatible(slot.SpecularRgba, width, height) ||
                !OptionalMapIsCompatible(slot.HeightRgba, width, height))
            {
                reason = $"slot {i} has mixed map dimensions";
                return false;
            }

            keys[i] = PreviewMaterialContentKey.Compute(slot);
        }

        plan = new GenesisMaterialTextureArrayPlan(width, height, slots.Count, keys);
        return true;
    }

    public bool ContentEquals(GenesisMaterialTextureArrayPlan other)
    {
        if (Width != other.Width || Height != other.Height || Layers != other.Layers)
        {
            return false;
        }

        for (var i = 0; i < SlotKeys.Length; i++)
        {
            if (!PreviewMaterialContentKey.Equals(SlotKeys[i], other.SlotKeys[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasValidAlbedo(PreviewMaterial material, int width, int height) =>
        material.AlbedoRgba.Length >= width * height * 4;

    private static bool OptionalMapIsCompatible(ReadOnlyMemory<byte>? rgba, int width, int height) =>
        rgba is not { Length: > 0 } map || map.Length >= width * height * 4;
}
