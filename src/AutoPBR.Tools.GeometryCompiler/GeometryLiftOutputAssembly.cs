using System.Text.Json.Nodes;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Assembles the final single-root geometry IR tree returned by mesh lift pipelines.
/// </summary>
internal static class GeometryLiftOutputAssembly
{
    /// <summary>
    /// Wraps lifted top-level body parts under a synthetic <c>root</c> node with an empty cuboid list.
    /// </summary>
    /// <param name="rootChildren">Parts lifted from mesh factory bytecode (body, head, legs, …).</param>
    /// <returns>A one-element array whose sole entry is the synthetic root.</returns>
    public static JsonArray WrapSyntheticRoot(JsonArray rootChildren) =>
        new()
        {
            new JsonObject
            {
                ["id"] = "root",
                ["pose"] = ZeroPose(),
                ["cuboids"] = new JsonArray(),
                ["children"] = rootChildren
            }
        };

    private static JsonObject ZeroPose() =>
        new()
        {
            ["translation"] = new JsonArray { 0d, 0d, 0d },
            ["rotationEulerRad"] = new JsonArray { 0d, 0d, 0d },
            ["eulerOrder"] = "XYZ"
        };
}
