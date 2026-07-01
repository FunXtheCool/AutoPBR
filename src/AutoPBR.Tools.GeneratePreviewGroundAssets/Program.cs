using AutoPBR.Core;
using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: GeneratePreviewGroundAssets <diffuse-png> <output-directory>");
    return 1;
}

var diffusePath = Path.GetFullPath(args[0]);
var outDir = Path.GetFullPath(args[1]);
if (!File.Exists(diffusePath))
{
    Console.Error.WriteLine($"Diffuse not found: {diffusePath}");
    return 1;
}

Directory.CreateDirectory(outDir);

var dataPath = Path.Combine(AppContext.BaseDirectory, "Data", "textures_data.json");
if (!File.Exists(dataPath))
{
    var devData = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "AutoPBR.Core", "Data", "textures_data.json"));
    dataPath = File.Exists(devData) ? devData : dataPath;
}

var specularData = SpecularData.LoadFromFile(dataPath);
var options = new AutoPbrOptions
{
    SpecularData = specularData,
    FastSpecular = true,
    FoliageMode = "No Height",
};

var maps = await PreviewGroundMapsResolver.TryResolveFromDiffuseFileAsync(diffusePath, options);
if (maps?.NormalRgba is null || maps.SpecularRgba is null)
{
    Console.Error.WriteLine("Failed to generate LabPBR maps for preview ground.");
    return 1;
}

WriteRgbaPng(Path.Combine(outDir, "grass_block_top_n.png"), maps.NormalRgba, maps.Width, maps.Height);
WriteRgbaPng(Path.Combine(outDir, "grass_block_top_s.png"), maps.SpecularRgba, maps.Width, maps.Height);
Console.WriteLine($"Wrote preview ground LabPBR assets to {outDir}");
return 0;

static void WriteRgbaPng(string path, byte[] rgba, int width, int height)
{
    using var image = Image.LoadPixelData<Rgba32>(rgba, width, height);
    image.SaveAsPng(path);
    Console.WriteLine($"  {Path.GetFileName(path)} ({width}x{height})");
}
