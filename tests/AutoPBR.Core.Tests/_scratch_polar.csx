using System.Numerics;
using System.Text.Json;
using AutoPBR.Core.Preview;
using AutoPBR.Core.Models;
using AutoPBR.Tests.TestSupport;

GeometryIrParityPolicy.ResetForTests();
var runtime = new CleanRoomEntityModelRuntime();
var profile = new MinecraftNativeProfile("26.1.2", TestEnvironmentPaths.AbsentNativeRoot, new Version(26, 1, 2));
const string path = "assets/minecraft/textures/entity/bear/polarbear.png";
const string jvm = "net.minecraft.client.model.animal.polarbear.PolarBearModel";
runtime.TryBuildStaticMesh(path, profile, 0f, 0f, out var mesh);
var repo = GeometryIrTestTierSupport.FindRepoRoot();
using var shard = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "docs/generated/geometry/26.1.2", jvm + ".json")));
var geometryRoot = GeometryIrPartTreeRepair.ApplyForParityCatalog(jvm, shard.RootElement);
var options = GeometryIrMeshEmitOptions.ForParity(128, 64) with { OfficialJvmName = jvm };
var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(geometryRoot, options);
float bodyZ=0, headZ=0, bodyX=0, headX=0; int bn=0, hn=0;
for (int i=0;i<mesh.Elements.Count;i++) {
  var c = mesh.Elements[i].From; var t = mesh.Elements[i].To;
  var cz = (c[2]+t[2])*0.5f; var cx = (c[0]+t[0])*0.5f;
  var id = partIds[i];
  if (id.Contains("head")) { headZ+=cz; headX+=cx; hn++; }
  else if (id=="body") { bodyZ+=cz; bodyX+=cx; bn++; }
}
bodyZ/=bn; headZ/=hn; bodyX/=bn; headX/=hn;
Console.WriteLine($"bodyZ={bodyZ:F3} headZ={headZ:F3} gapZ={MathF.Abs(bodyZ-headZ):F3}");
Console.WriteLine($"bodyX={bodyX:F3} headX={headX:F3} gapX={MathF.Abs(bodyX-headX):F3}");
