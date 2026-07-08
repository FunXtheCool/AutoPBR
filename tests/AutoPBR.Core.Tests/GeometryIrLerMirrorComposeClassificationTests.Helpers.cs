using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Models;
using AutoPBR.Preview;

namespace AutoPBR.Core.Tests;

public sealed partial class GeometryIrLerMirrorComposeClassificationTests
{
    private static (float HeadY, float LegY) MeasureHeadLegCentroidYPair(
        MergedJavaBlockModel mesh,
        JsonElement geometryRoot,
        int atlasW,
        int atlasH,
        string officialJvmName)
    {
        var options = new GeometryIrMeshEmitOptions
        {
            RootTransform = Matrix4x4.Identity,
            DefaultPartScale = 1f,
            AtlasWidth = atlasW,
            AtlasHeight = atlasH,
            Fidelity = GeometryIrEmitFidelity.Parity,
            OfficialJvmName = officialJvmName,
        };
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(geometryRoot, options);
        float headSum = 0f;
        var headCount = 0;
        float legSum = 0f;
        var legCount = 0;
        for (var i = 0; i < mesh.Elements.Count; i++)
        {
            var partId = partIds[i];
            TransformWorldCorners(mesh.Elements[i], out var wMin, out var wMax);
            var cy = (wMin.Y + wMax.Y) * 0.5f;
            if (partId.Contains("head", StringComparison.OrdinalIgnoreCase) &&
                !partId.Contains("leg", StringComparison.OrdinalIgnoreCase))
            {
                headSum += cy;
                headCount++;
            }

            if (partId.Contains("leg", StringComparison.OrdinalIgnoreCase))
            {
                legSum += cy;
                legCount++;
            }
        }

        Assert.True(headCount > 0 && legCount > 0);
        return (headSum / headCount, legSum / legCount);
    }

    private static void AssertQuadrupedBodyBetweenLegsAndHead(
        MergedJavaBlockModel mesh,
        JsonElement geometryRoot,
        int atlasW,
        int atlasH,
        string officialJvmName)
    {
        var (headY, bodyY, legY) = MeasureHeadBodyLegCentroidY(mesh, geometryRoot, atlasW, atlasH, officialJvmName);
        Assert.True(legY < headY, $"{officialJvmName}: legY={legY:F3} headY={headY:F3}");
        var rotatedTorsoBand = legY < headY && headY < bodyY;
        var nestedFelineBand = legY < bodyY && bodyY < headY;
        Assert.True(
            rotatedTorsoBand || nestedFelineBand,
            $"{officialJvmName}: bodyY={bodyY:F3} outside preview bands; leg={legY:F3} head={headY:F3}");
        var spanLimit = officialJvmName.Contains("polarbear", StringComparison.OrdinalIgnoreCase) ? 32f : 24f;
        var span = MathF.Max(bodyY, headY) - legY;
        Assert.True(span < spanLimit, $"{officialJvmName}: vertical span={span:F3}");
    }

    private static (float HeadY, float BodyY, float LegY) MeasureHeadBodyLegCentroidY(
        MergedJavaBlockModel mesh,
        JsonElement geometryRoot,
        int atlasW,
        int atlasH,
        string officialJvmName)
    {
        var options = new GeometryIrMeshEmitOptions
        {
            RootTransform = Matrix4x4.Identity,
            DefaultPartScale = 1f,
            AtlasWidth = atlasW,
            AtlasHeight = atlasH,
            Fidelity = GeometryIrEmitFidelity.Parity,
            OfficialJvmName = officialJvmName,
        };
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(geometryRoot, options);
        float headSum = 0f;
        var headCount = 0;
        float bodySum = 0f;
        var bodyCount = 0;
        float legSum = 0f;
        var legCount = 0;
        for (var i = 0; i < mesh.Elements.Count; i++)
        {
            var partId = partIds[i];
            TransformWorldCorners(mesh.Elements[i], out var wMin, out var wMax);
            var cy = (wMin.Y + wMax.Y) * 0.5f;
            if (partId.Contains("head", StringComparison.OrdinalIgnoreCase) &&
                !partId.Contains("leg", StringComparison.OrdinalIgnoreCase))
            {
                headSum += cy;
                headCount++;
            }
            else if (partId.Contains("body", StringComparison.OrdinalIgnoreCase) &&
                     !partId.Contains("leg", StringComparison.OrdinalIgnoreCase))
            {
                bodySum += cy;
                bodyCount++;
            }
            else if (partId.Contains("leg", StringComparison.OrdinalIgnoreCase))
            {
                legSum += cy;
                legCount++;
            }
        }

        Assert.True(headCount > 0 && bodyCount > 0 && legCount > 0);
        return (headSum / headCount, bodySum / bodyCount, legSum / legCount);
    }

    private static void AssertLegsBelowHead(
        MergedJavaBlockModel mesh,
        JsonElement geometryRoot,
        int atlasW,
        int atlasH,
        string officialJvmName)
    {
        var options = new GeometryIrMeshEmitOptions
        {
            RootTransform = Matrix4x4.Identity,
            DefaultPartScale = 1f,
            AtlasWidth = atlasW,
            AtlasHeight = atlasH,
            Fidelity = GeometryIrEmitFidelity.Parity,
            OfficialJvmName = officialJvmName,
        };
        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(geometryRoot, options);
        float headSum = 0f;
        var headCount = 0;
        float legSum = 0f;
        var legCount = 0;
        for (var i = 0; i < mesh.Elements.Count; i++)
        {
            var partId = partIds[i];
            TransformWorldCorners(mesh.Elements[i], out var wMin, out var wMax);
            var cy = (wMin.Y + wMax.Y) * 0.5f;
            if (partId.Contains("head", StringComparison.OrdinalIgnoreCase) &&
                !partId.Contains("leg", StringComparison.OrdinalIgnoreCase))
            {
                headSum += cy;
                headCount++;
            }

            if (partId.Contains("leg", StringComparison.OrdinalIgnoreCase))
            {
                legSum += cy;
                legCount++;
            }
        }

        Assert.True(headCount > 0 && legCount > 0);
        Assert.True(legSum / legCount < headSum / headCount);
    }

    private static void TransformWorldCorners(
        ModelElement el,
        out Vector3 wMin,
        out Vector3 wMax)
    {
        wMin = new Vector3(float.PositiveInfinity);
        wMax = new Vector3(float.NegativeInfinity);
        ReadOnlySpan<(float x, float y, float z)> corners =
        [
            (el.From[0], el.From[1], el.From[2]),
            (el.To[0], el.From[1], el.From[2]),
            (el.From[0], el.To[1], el.From[2]),
            (el.To[0], el.To[1], el.From[2]),
            (el.From[0], el.From[1], el.To[2]),
            (el.To[0], el.From[1], el.To[2]),
            (el.From[0], el.To[1], el.To[2]),
            (el.To[0], el.To[1], el.To[2]),
        ];
        foreach (var (x, y, z) in corners)
        {
            var w = Vector3.Transform(new Vector3(x, y, z), el.LocalToParent);
            wMin = Vector3.Min(wMin, w);
            wMax = Vector3.Max(wMax, w);
        }
    }
}
