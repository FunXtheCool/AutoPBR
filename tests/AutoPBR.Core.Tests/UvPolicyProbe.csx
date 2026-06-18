using AutoPBR.Core.Preview;
using AutoPBR.Tests.TestSupport;

const ulong OldCreeper = 0x5d24fe3716be89a5UL;
var profile = new MinecraftNativeProfile("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));
var runtime = EntityModelRuntimeFactory.Create();
runtime.TryBuildStaticMesh("assets/minecraft/textures/entity/creeper/creeper.png", profile, 0f, 0f, out var merged, out _);
var ordered = JavaModelPreviewPipeline.CollectOrderedTextureZipPaths(merged, "minecraft");
var pathToIdx = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
var texSizes = new Dictionary<string, (int w, int h)>(StringComparer.OrdinalIgnoreCase);
for (var i = 0; i < ordered.Count; i++) { pathToIdx[ordered[i]] = i; texSizes[ordered[i]] = (64, 32); }

ulong Fp(float[] verts) {
  unchecked { ulong h=14695981039346656037UL; int s=12; for(int i=6;i<verts.Length;i+=s){h^=BitConverter.SingleToUInt32Bits(verts[i]);h*=1099511628211UL;h^=BitConverter.SingleToUInt32Bits(verts[i+1]);h*=1099511628211UL;} return h; }

foreach (var flipV in new[]{false,true})
foreach (var useBl in new[]{false,true})
foreach (var swap in new[]{false,true})
{
  var p = new PreviewUvBakePolicy { FlipV=flipV, UseBottomLeftUvOrigin=useBl, SwapFaceUpDown=swap, PreserveDirectionalBounds=true };
  MinecraftModelBaker.TryBakeWithUvPolicy(merged,"minecraft",pathToIdx,texSizes,in p,out var v,out _,out _);
  var fp = Fp(v);
  if (fp==OldCreeper) Console.WriteLine($"MATCH flipV={flipV} useBL={useBl} swap={swap} fp={fp:x16}");
  else Console.WriteLine($"flipV={flipV} useBL={useBl} swap={swap} fp={fp:x16}");
}
