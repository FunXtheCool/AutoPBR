

namespace AutoPBR.GeometryCompiler.Tests;

public sealed class ObfuscatedDeformationLiftTests
{
    private const string ObfuscatedInflateAddBoxSlice = """
    Code:
      14: ldc           #42                 // String body
      16: invokestatic  #48                 // Method hdl.c:()Lhdl;
      19: iconst_0
      20: iconst_0
      21: invokevirtual #51                 // Method hdl.a:(II)Lhdl;
      24: ldc           #52                 // float -2.0f
      26: ldc           #52                 // float -2.0f
      28: ldc           #53                 // float -2.0f
      30: ldc           #54                 // float 4.0f
      32: ldc           #54                 // float 4.0f
      34: ldc           #55                 // float 4.0f
      36: new           #88                 // class hdk
      39: dup
      40: ldc           #89                 // float 0.5f
      42: invokespecial #95                 // Method hdk."<init>":(F)V
      45: invokevirtual #97                 // Method hdl.a:(FFFFFFLhdk;)Lhdl;
      48: invokestatic  #76                 // Method hdi.a:(FFF)Lhdi;
      51: invokevirtual #81                 // Method hdq.a:(Ljava/lang/String;Lhdl;Lhdi;)Lhdq;
    """;

    private static MojangMappingsParser? TryLoadTestMappings()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "tools", "minecraft-parity", "1.21.11", "client_mappings.txt");
            if (File.Exists(candidate))
            {
                return MojangMappingsParser.Load(candidate);
            }

            if (File.Exists(Path.Combine(dir, "AutoPBR.sln")))
            {
                return null;
            }

            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }

    [Fact]
    public void TryLift_obfuscated_cube_deformation_emits_inflate()
    {
        var maps = TryLoadTestMappings();
        Assert.NotNull(maps);

        var lifted = JavapFloatGeometryMeshLift.TryLift(ObfuscatedInflateAddBoxSlice, out var roots, out var notes, maps);
        Assert.True(lifted, $"TryLift failed: {string.Join("; ", notes)}");
        Assert.True(roots.Count > 0 && roots[0]!["children"]!.AsArray().Count > 0,
            "expected at least one lifted part");
        var cub = roots[0]!["children"]!.AsArray()[0]!["cuboids"]!.AsArray()[0]!.AsObject();
        Assert.True(cub.ContainsKey("inflate"), $"cuboid keys: {string.Join(", ", cub.Select(static kv => kv.Key))}");
        Assert.Equal(0.5, cub["inflate"]!.GetValue<double>(), 3);
        Assert.Equal("exact", (string?)cub["liftKind"]);
    }
}
