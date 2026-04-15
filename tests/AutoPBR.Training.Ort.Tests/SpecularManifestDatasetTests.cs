using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace AutoPBR.Training.Ort.Tests;

public class SpecularManifestDatasetTests
{
    [Fact]
    public void GetSample_channel_layout_matches_python_nchw_and_planar_rgba_target()
    {
        var root = Path.Combine(Path.GetTempPath(), "autopbr_ort_ds_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "splits"));

        try
        {
            var diffusePath = Path.Combine(root, "d.png");
            var specPath = Path.Combine(root, "s.png");
            CreateRgbaImage(diffusePath, 8, 8, 255, 0, 0, 255);
            CreateRgbaImage(specPath, 8, 8, 10, 20, 30, 128);

            File.WriteAllText(
                Path.Combine(root, "manifest.jsonl"),
                """
                {"id":"a","image":"d.png","label_spec":"s.png"}

                """.Trim());

            File.WriteAllText(Path.Combine(root, "splits", "train.txt"), "a\n");

            var ds = new SpecularManifestDataset(root, "train", trainRes: 4, inChannels: 4, augment: false, randomSeed: 42);
            var c = 4;
            var h = 4;
            var w = 4;
            var input = new float[c * h * w];
            var target = new float[4 * h * w];
            var valid = new float[h * w];
            var scratch = new byte[h * w * 3];
            ds.GetSample(0, input.AsSpan(), target.AsSpan(), valid.AsSpan(), scratch);

            Assert.True(input[0] > 0.99f && input[h * w] < 0.02f && input[2 * h * w] < 0.02f);
            Assert.InRange(input[3 * h * w], 0f, 1f);
            Assert.Equal(10f / 255f, target[0], precision: 5);
            Assert.Equal(128f / 255f, target[3 * h * w], precision: 5);
            Assert.All(valid, v => Assert.Equal(1f, v));
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
            }
        }
    }

    private static void CreateRgbaImage(string path, int width, int height, byte r, byte g, byte b, byte a)
    {
        using var img = new Image<Rgba32>(width, height, new Rgba32(r, g, b, a));
        img.SaveAsPng(path);
    }
}
