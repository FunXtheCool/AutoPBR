using System.Text.Json.Nodes;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Builds lifted part-tree forests from mesh-factory bytecode: segment collection, receiver-slot hierarchy, attach.
/// Delegates segment cuboid/pose parsing to <see cref="JavapFloatGeometryMeshLift.TryLiftSegment"/>.
/// Forest merge/reparent after multi-island lift uses <see cref="GeometryLiftForestMerge"/> and <see cref="GeometryLiftJsonMerge"/>.
/// </summary>
internal static class JavapLiftPartTreeBuilder
{
    /// <inheritdoc cref="JavapFloatGeometryMeshLift.IslandUsesMeshWideConstantScope"/>
    internal static bool IslandUsesMeshWideConstantScope(string islandBytecode) =>
        JavapFloatGeometryMeshLift.IslandUsesMeshWideConstantScope(islandBytecode);

    /// <inheritdoc cref="JavapFloatGeometryMeshLift.TryCollectLiftedRootChildren"/>
    internal static bool TryCollectLiftedRootChildren(string meshFactoryJavap, List<string> notes,
        out JsonArray rootChildren, IReadOnlyList<string>? meshWideLines = null) =>
        JavapFloatGeometryMeshLift.TryCollectLiftedRootChildren(meshFactoryJavap, notes, out rootChildren, meshWideLines);
}
