using AutoPBR.Core.Preview;

namespace AutoPBR.Core.Tests;

public sealed class BlockModelRescaleRotationTests
{
    [Fact]
    public void Merge_parses_rescale_true_on_element_rotation()
    {
        using var fixture = new BlockModelMergerTests.BlockModelFixture();
        fixture.Write(
            "assets/minecraft/models/block/rotated_panel.json",
            """
            {
              "elements": [
                {
                  "from": [0, 0, 0],
                  "to": [16, 16, 16],
                  "rotation": { "angle": 45, "axis": "y", "rescale": true },
                  "faces": {
                    "north": { "texture": "#tex", "uv": [0, 0, 16, 16] }
                  }
                }
              ],
              "textures": { "tex": "minecraft:block/stone" }
            }
            """);

        var source = new DirectoryAssetSource(fixture.Root);
        Assert.True(MinecraftModelMerger.TryMerge(
            source,
            "assets/minecraft/models/block/rotated_panel.json",
            out var merged));
        Assert.True(merged.Elements[0].RescaleRotation);
    }
}
