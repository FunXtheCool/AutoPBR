using System.Numerics;
using AutoPBR.Core.Models;
using AutoPBR.Core.Preview;

EntityPreviewDebugSettings.UseLegacyTranslationTimesRotationPartPose = false;
var erT = ComposeHornWorld(false);
EntityPreviewDebugSettings.UseLegacyTranslationTimesRotationPartPose = true;
var txEr = ComposeHornWorld(true);

var cuboidCenter = new Vector3(-0.5f, -1.5f, 0.5f);
var head = EntityParityTemplate.T(0f, 4f, -8f);
Console.WriteLine($"Er×T horn pivot: {Vector3.Transform(Vector3.Zero, erT)} cuboid: {Vector3.Transform(cuboidCenter, erT)}");
Console.WriteLine($"T×Er horn pivot: {Vector3.Transform(Vector3.Zero, txEr)} cuboid: {Vector3.Transform(cuboidCenter, txEr)}");
Console.WriteLine($"head pivot: {Vector3.Transform(Vector3.Zero, head)}");

Matrix4x4 ComposeHornWorld(bool legacy) {
  EntityPreviewDebugSettings.UseLegacyTranslationTimesRotationPartPose = legacy;
  CleanRoomEntityModelRuntime.TryComposePartPosePublic(System.Text.Json.JsonDocument.Parse("{\"translation\":[-4.5,-2.5,-3.5],\"rotationEulerRad\":[1.5708,0,0],\"eulerOrder\":\"XYZ\"}").RootElement, out var hornLocal);
  return Matrix4x4.Multiply(head, hornLocal);
}
