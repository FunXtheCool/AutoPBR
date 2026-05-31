using System.Globalization;
using System.Numerics;
using System.Text.Json;


namespace AutoPBR.Core.Preview;

/// <summary>
/// Compares Java <c>ModelPart</c> reference bakes to C# geometry IR parity meshes or IR shard cuboid fingerprints.
/// </summary>
internal static class GeometryIrReferenceComparer
{
    public readonly record struct CompareResult(
        bool IsMatch,
        string? Message,
        int ReferenceCuboids,
        int ComparedCuboids,
        int ReferencePoses = 0,
        int ComparedPoses = 0);

    public static CompareResult CompareReferenceToIrShard(JsonElement referenceRoot, JsonElement irShardRoot, double tolerance = 1e-3) =>
        CompareReferenceToIrShard(referenceRoot, irShardRoot, tolerance, includePoses: false);

    /// <summary>Phase 6: compare cuboid fingerprints per normalized part id (humanoid overlay parts).</summary>
    public static CompareResult CompareReferenceToIrShardCuboidsByPartId(JsonElement referenceRoot,
        JsonElement irShardRoot, double tolerance = 1e-3)
    {
        if (referenceRoot.TryGetProperty("extractionStatus", out var st) &&
            string.Equals(st.GetString(), "reference_stub", StringComparison.Ordinal))
        {
            return new CompareResult(false, "reference is stub", 0, 0);
        }

        if (!irShardRoot.TryGetProperty("extractionStatus", out var irSt) ||
            !string.Equals(irSt.GetString(), "ok", StringComparison.Ordinal))
        {
            return new CompareResult(false, "IR shard not ok", 0, 0);
        }

        var refById = CollectCuboidFingerprintsByPartId(referenceRoot);
        var irById = CollectCuboidFingerprintsByPartId(irShardRoot);
        if (refById.Count != irById.Count)
        {
            return new CompareResult(false, $"part count reference={refById.Count} ir={irById.Count}", refById.Count,
                irById.Count);
        }

        var totalCuboids = 0;
        foreach (var (id, refFp) in refById)
        {
            if (!irById.TryGetValue(id, out var irFp))
            {
                return new CompareResult(false, $"missing IR part '{id}'", refById.Count, irById.Count);
            }

            var cmp = CompareFingerprintLists(refFp, irFp, tolerance);
            if (!cmp.IsMatch)
            {
                return new CompareResult(false, $"part '{id}': {cmp.Message}", cmp.ReferenceCuboids, cmp.ComparedCuboids);
            }

            totalCuboids += refFp.Count;
        }

        return new CompareResult(true, null, totalCuboids, totalCuboids);
    }

    private static Dictionary<string, List<string>> CollectCuboidFingerprintsByPartId(JsonElement doc)
    {
        var map = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        if (!doc.TryGetProperty("roots", out var roots) || roots.ValueKind != JsonValueKind.Array)
        {
            return map;
        }

        foreach (var root in roots.EnumerateArray())
        {
            WalkPartCuboidsById(root, map);
        }

        return map;
    }

    private static void WalkPartCuboidsById(JsonElement part, Dictionary<string, List<string>> map)
    {
        if (part.TryGetProperty("id", out var idEl) &&
            part.TryGetProperty("cuboids", out var cuboids) && cuboids.ValueKind == JsonValueKind.Array)
        {
            var id = NormalizePartId(idEl.GetString() ?? "");
            if (!string.IsNullOrEmpty(id))
            {
                if (!map.TryGetValue(id, out var list))
                {
                    list = [];
                    map[id] = list;
                }

                foreach (var c in cuboids.EnumerateArray())
                {
                    list.Add(CuboidFingerprint(c));
                }
            }
        }

        if (part.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var ch in children.EnumerateArray())
            {
                WalkPartCuboidsById(ch, map);
            }
        }
    }

    /// <summary>
    /// Composed part-origin world translation per part id (parent chain × local pose via <see cref="GeometryIrMeshWalk"/>).
    /// Catches flat-nested IR where local poses match reference but parity-repaired hierarchy diverges.
    /// </summary>
    public static CompareResult CompareReferenceWorldPartOrigins(JsonElement referenceRoot, JsonElement irShardRoot,
        double tolerance = 0.05)
    {
        if (referenceRoot.TryGetProperty("extractionStatus", out var st) &&
            string.Equals(st.GetString(), "reference_stub", StringComparison.Ordinal))
        {
            return new CompareResult(false, "reference is stub", 0, 0);
        }

        if (!irShardRoot.TryGetProperty("extractionStatus", out var irSt) ||
            !string.Equals(irSt.GetString(), "ok", StringComparison.Ordinal))
        {
            return new CompareResult(false, "IR shard not ok", 0, 0);
        }

        if (!GeometryIrMeshWalk.TryCollectPartWorldTranslations(
                referenceRoot, Matrix4x4.Identity, out var refWorld, out var refFail))
        {
            return new CompareResult(false, $"reference world walk: {refFail}", 0, 0);
        }

        if (!GeometryIrMeshWalk.TryCollectPartWorldTranslations(
                irShardRoot, Matrix4x4.Identity, out var irWorld, out var irFail))
        {
            return new CompareResult(false, $"IR world walk: {irFail}", 0, 0);
        }

        if (refWorld.Count != irWorld.Count)
        {
            return new CompareResult(false,
                $"world pose part count reference={refWorld.Count} ir={irWorld.Count}",
                refWorld.Count,
                irWorld.Count);
        }

        foreach (var (partId, refOrigin) in refWorld)
        {
            if (!irWorld.TryGetValue(partId, out var irOrigin))
            {
                return new CompareResult(false, $"missing IR world pose for part '{partId}'", refWorld.Count, irWorld.Count);
            }

            if (!TranslationNear(refOrigin, irOrigin, tolerance))
            {
                return new CompareResult(false,
                    $"part '{partId}' world origin reference=({refOrigin.X:R},{refOrigin.Y:R},{refOrigin.Z:R}) " +
                    $"ir=({irOrigin.X:R},{irOrigin.Y:R},{irOrigin.Z:R})",
                    refWorld.Count,
                    irWorld.Count);
            }
        }

        return new CompareResult(true, null, refWorld.Count, irWorld.Count);
    }

    private static bool TranslationNear(Vector3 a, Vector3 b, double tolerance) =>
        Math.Abs(a.X - b.X) <= tolerance &&
        Math.Abs(a.Y - b.Y) <= tolerance &&
        Math.Abs(a.Z - b.Z) <= tolerance;

    /// <summary>Phase 5: cuboid multiset plus pose multiset (translation + rotation per part, ids ignored).</summary>
    public static CompareResult CompareReferenceToIrShardWithPoses(JsonElement referenceRoot, JsonElement irShardRoot,
        double cuboidTolerance = 1e-3, double poseTolerance = 0.05)
    {
        var cuboids = CompareReferenceToIrShard(referenceRoot, irShardRoot, cuboidTolerance, includePoses: false);
        if (!cuboids.IsMatch)
        {
            return cuboids;
        }

        var poses = ComparePosesByNormalizedId(referenceRoot, irShardRoot, poseTolerance);
        if (!poses.IsMatch)
        {
            return new CompareResult(
                false,
                $"pose: {poses.Message}",
                cuboids.ReferenceCuboids,
                cuboids.ComparedCuboids,
                poses.ReferencePoses,
                poses.ComparedPoses);
        }

        return new CompareResult(true, null, cuboids.ReferenceCuboids, cuboids.ComparedCuboids, poses.ReferencePoses, poses.ComparedPoses);
    }

    private readonly record struct PoseCompareResult(bool IsMatch, string? Message, int ReferencePoses, int ComparedPoses);

    private static CompareResult CompareReferenceToIrShard(JsonElement referenceRoot, JsonElement irShardRoot,
        double tolerance, bool includePoses)
    {
        if (referenceRoot.TryGetProperty("extractionStatus", out var st) &&
            string.Equals(st.GetString(), "reference_stub", StringComparison.Ordinal))
        {
            return new CompareResult(false, "reference is stub", 0, 0);
        }

        if (!irShardRoot.TryGetProperty("extractionStatus", out var irSt) ||
            !string.Equals(irSt.GetString(), "ok", StringComparison.Ordinal))
        {
            return new CompareResult(false, "IR shard not ok", 0, 0);
        }

        var refFp = CollectCuboidFingerprints(referenceRoot, "roots");
        var irFp = CollectCuboidFingerprints(irShardRoot, "roots");
        var cuboids = CompareFingerprintLists(refFp, irFp, tolerance);
        if (!cuboids.IsMatch || !includePoses)
        {
            return cuboids;
        }

        var refPoses = CollectPoseFingerprints(referenceRoot, "roots");
        var irPoses = CollectPoseFingerprints(irShardRoot, "roots");
        var poses = CompareFingerprintLists(refPoses, irPoses, tolerance);
        if (!poses.IsMatch)
        {
            return new CompareResult(
                false,
                $"pose: {poses.Message}",
                cuboids.ReferenceCuboids,
                cuboids.ComparedCuboids,
                poses.ReferenceCuboids,
                poses.ComparedCuboids);
        }

        return new CompareResult(true, null, cuboids.ReferenceCuboids, cuboids.ComparedCuboids, refPoses.Count, irPoses.Count);
    }

    public static CompareResult CompareReferenceToParityMesh(JsonElement referenceRoot, MergedJavaBlockModel parityMesh,
        double tolerance = 1e-3)
    {
        if (referenceRoot.TryGetProperty("extractionStatus", out var st) &&
            string.Equals(st.GetString(), "reference_stub", StringComparison.Ordinal))
        {
            return new CompareResult(false, "reference is stub", 0, 0);
        }

        var refFp = CollectCuboidFingerprints(referenceRoot, "roots");
        var meshFp = CollectMeshElementFingerprints(parityMesh);
        return CompareFingerprintLists(refFp, meshFp, tolerance);
    }

    /// <summary>
    /// Compares per-part model-space origins: mesh <c>LocalToParent</c> translation vs reference <c>worldPose</c>
    /// (pre–<c>LivingEntityRenderer</c> mirror).
    /// </summary>
    public static CompareResult CompareReferenceJavaModelWorldToParityMesh(
        JsonElement referenceRoot,
        JsonElement geometryRoot,
        MergedJavaBlockModel parityMesh,
        GeometryIrMeshEmitOptions emitOptions,
        double tolerance = 0.35)
        => CompareReferenceJavaWorldToParityMeshCore(
            referenceRoot,
            geometryRoot,
            parityMesh,
            emitOptions,
            applyLivingEntityRendererMirror: false,
            tolerance);

    /// <summary>
    /// Compares per-part preview world origins: mesh after <c>S * LocalToParent</c> vs
    /// <c>S *</c> reference <c>worldPose</c> (model layer + vanilla LER mirror).
    /// </summary>
    public static CompareResult CompareReferenceJavaPreviewWorldToParityMesh(
        JsonElement referenceRoot,
        JsonElement geometryRoot,
        MergedJavaBlockModel parityMesh,
        GeometryIrMeshEmitOptions emitOptions,
        double tolerance = 0.35)
        => CompareReferenceJavaWorldToParityMeshCore(
            referenceRoot,
            geometryRoot,
            parityMesh,
            emitOptions,
            applyLivingEntityRendererMirror: true,
            tolerance);

    private static CompareResult CompareReferenceJavaWorldToParityMeshCore(
        JsonElement referenceRoot,
        JsonElement geometryRoot,
        MergedJavaBlockModel parityMesh,
        GeometryIrMeshEmitOptions emitOptions,
        bool applyLivingEntityRendererMirror,
        double tolerance)
    {
        if (referenceRoot.TryGetProperty("extractionStatus", out var st) &&
            string.Equals(st.GetString(), "reference_stub", StringComparison.Ordinal))
        {
            return new CompareResult(false, "reference is stub", 0, 0);
        }

        if (!GeometryIrMeshWalk.TryCollectBakedWorldTranslations(
                referenceRoot, out var refWorld, out var refFail))
        {
            return new CompareResult(false, $"reference worldPose: {refFail}", 0, 0);
        }

        var partIds = GeometryIrMeshWalk.CollectCuboidOwnerPartIds(geometryRoot, emitOptions);
        if (partIds.Count != parityMesh.Elements.Count)
        {
            return new CompareResult(
                false,
                $"cuboid/part order reference parts={refWorld.Count} mesh elements={parityMesh.Elements.Count}",
                refWorld.Count,
                parityMesh.Elements.Count);
        }

        var ler = CleanRoomEntityModelRuntime.LivingEntityRendererPreviewRootScale;
        var compared = 0;
        var skipped = 0;
        foreach (var (partId, refOrigin) in refWorld)
        {
            if (!TryMeanPartOriginInMesh(parityMesh, partIds, partId, out var meshOrigin))
            {
                skipped++;
                continue;
            }

            var expected = applyLivingEntityRendererMirror
                ? Vector3.Transform(refOrigin, ler)
                : refOrigin;
            if (!TranslationNear(expected, meshOrigin, tolerance))
            {
                var space = applyLivingEntityRendererMirror ? "preview" : "model";
                return new CompareResult(
                    false,
                    $"part '{partId}' {space} origin expected=({expected.X:R},{expected.Y:R},{expected.Z:R}) " +
                    $"mesh=({meshOrigin.X:R},{meshOrigin.Y:R},{meshOrigin.Z:R}) refModel=({refOrigin.X:R},{refOrigin.Y:R},{refOrigin.Z:R})",
                    refWorld.Count,
                    compared);
            }

            compared++;
        }

        if (compared == 0)
        {
            return new CompareResult(
                false,
                $"no comparable parts (reference parts={refWorld.Count} skipped={skipped})",
                refWorld.Count,
                0);
        }

        return new CompareResult(true, null, refWorld.Count, compared);
    }

    private static bool TryMeanPartOriginInMesh(
        MergedJavaBlockModel mesh,
        IReadOnlyList<string> partIds,
        string partId,
        out Vector3 origin)
    {
        origin = default;
        var sum = Vector3.Zero;
        var count = 0;
        for (var i = 0; i < partIds.Count && i < mesh.Elements.Count; i++)
        {
            if (!string.Equals(partIds[i], partId, StringComparison.Ordinal))
            {
                continue;
            }

            sum += Vector3.Transform(Vector3.Zero, mesh.Elements[i].LocalToParent);
            count++;
        }

        if (count == 0)
        {
            return false;
        }

        origin = sum / count;
        return true;
    }

    private static CompareResult CompareFingerprintLists(List<string> reference, List<string> compared, double tolerance)
    {
        if (reference.Count != compared.Count)
        {
            return new CompareResult(
                false,
                $"cuboid count reference={reference.Count} compared={compared.Count}",
                reference.Count,
                compared.Count);
        }

        reference.Sort(StringComparer.Ordinal);
        compared.Sort(StringComparer.Ordinal);
        for (var i = 0; i < reference.Count; i++)
        {
            if (reference[i] == compared[i])
            {
                continue;
            }

            if (TryParseFingerprint(reference[i], out var ra) && TryParseFingerprint(compared[i], out var cb) &&
                FingerprintNear(ra, cb, tolerance))
            {
                continue;
            }

            if (PoseFingerprintNear(reference[i], compared[i], tolerance))
            {
                continue;
            }

            return new CompareResult(
                false,
                $"fingerprint mismatch at {i}: reference={reference[i]} compared={compared[i]}",
                reference.Count,
                compared.Count);
        }

        return new CompareResult(true, null, reference.Count, compared.Count);
    }

    private static List<string> CollectCuboidFingerprints(JsonElement doc, string rootsProperty)
    {
        var list = new List<string>();
        if (!doc.TryGetProperty(rootsProperty, out var roots) || roots.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var root in roots.EnumerateArray())
        {
            WalkPartCuboids(root, list);
        }

        return list;
    }

    private static void WalkPartCuboids(JsonElement part, List<string> fingerprints)
    {
        if (part.TryGetProperty("cuboids", out var cuboids) && cuboids.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in cuboids.EnumerateArray())
            {
                fingerprints.Add(CuboidFingerprint(c));
            }
        }

        if (part.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var ch in children.EnumerateArray())
            {
                WalkPartCuboids(ch, fingerprints);
            }
        }
    }

    private static PoseCompareResult ComparePosesByNormalizedId(JsonElement referenceRoot, JsonElement irShardRoot,
        double tolerance)
    {
        var refById = new Dictionary<string, string>(StringComparer.Ordinal);
        var irById = new Dictionary<string, string>(StringComparer.Ordinal);
        if (referenceRoot.TryGetProperty("roots", out var refRoots) && refRoots.ValueKind == JsonValueKind.Array)
        {
            foreach (var root in refRoots.EnumerateArray())
            {
                WalkPartPosesById(root, refById);
            }
        }

        if (irShardRoot.TryGetProperty("roots", out var irRoots) && irRoots.ValueKind == JsonValueKind.Array)
        {
            foreach (var root in irRoots.EnumerateArray())
            {
                WalkPartPosesById(root, irById);
            }
        }

        if (refById.Count != irById.Count)
        {
            return new PoseCompareResult(false,
                $"pose part count reference={refById.Count} ir={irById.Count}",
                refById.Count,
                irById.Count);
        }

        foreach (var (id, refFp) in refById)
        {
            if (!irById.TryGetValue(id, out var irFp))
            {
                return new PoseCompareResult(false, $"missing IR pose for part '{id}'", refById.Count, irById.Count);
            }

            if (!PoseFingerprintNear(refFp, irFp, tolerance))
            {
                return new PoseCompareResult(false, $"part '{id}': ref={refFp} ir={irFp}", refById.Count, irById.Count);
            }
        }

        return new PoseCompareResult(true, null, refById.Count, irById.Count);
    }

    private static void WalkPartPosesById(JsonElement part, Dictionary<string, string> byId)
    {
        if (part.TryGetProperty("pose", out var pose) && pose.ValueKind == JsonValueKind.Object &&
            part.TryGetProperty("id", out var idEl))
        {
            var id = NormalizePartId(idEl.GetString() ?? "");
            if (!string.IsNullOrEmpty(id))
            {
                byId[id] = PoseFingerprint(pose);
            }
        }

        if (part.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var ch in children.EnumerateArray())
            {
                WalkPartPosesById(ch, byId);
            }
        }
    }

    private static string NormalizePartId(string id) =>
        id.Replace("_", "", StringComparison.Ordinal);

    private static List<string> CollectPoseFingerprints(JsonElement doc, string rootsProperty)
    {
        var list = new List<string>();
        if (!doc.TryGetProperty(rootsProperty, out var roots) || roots.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var root in roots.EnumerateArray())
        {
            WalkPartPoses(root, list);
        }

        return list;
    }

    private static void WalkPartPoses(JsonElement part, List<string> fingerprints)
    {
        if (part.TryGetProperty("pose", out var pose) && pose.ValueKind == JsonValueKind.Object)
        {
            fingerprints.Add(PoseFingerprint(pose));
        }

        if (part.TryGetProperty("children", out var children) && children.ValueKind == JsonValueKind.Array)
        {
            foreach (var ch in children.EnumerateArray())
            {
                WalkPartPoses(ch, fingerprints);
            }
        }
    }

    private static string PoseFingerprint(JsonElement pose)
    {
        static string At(JsonElement arr, int index) =>
            arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > index
                ? arr[index].GetDouble().ToString("R", CultureInfo.InvariantCulture)
                : "0";

        var t = pose.GetProperty("translation");
        var r = pose.TryGetProperty("rotationEulerRad", out var rot) ? rot : default;
        return string.Create(CultureInfo.InvariantCulture,
            $"{At(t, 0)},{At(t, 1)},{At(t, 2)}|{At(r, 0)},{At(r, 1)},{At(r, 2)}");
    }

    private static List<string> CollectMeshElementFingerprints(MergedJavaBlockModel mesh)
    {
        var list = new List<string>();
        foreach (var e in mesh.Elements)
        {
            list.Add(string.Create(CultureInfo.InvariantCulture,
                $"{e.From[0]:R},{e.From[1]:R},{e.From[2]:R}|{e.To[0]:R},{e.To[1]:R},{e.To[2]:R}"));
        }

        return list;
    }

    private static string CuboidFingerprint(JsonElement cuboid)
    {
        static string At(JsonElement arr, int index) =>
            arr.ValueKind == JsonValueKind.Array && arr.GetArrayLength() > index
                ? arr[index].GetDouble().ToString("R", CultureInfo.InvariantCulture)
                : "0";

        var from = cuboid.GetProperty("from");
        var to = cuboid.GetProperty("to");
        return string.Create(CultureInfo.InvariantCulture,
            $"{At(from, 0)},{At(from, 1)},{At(from, 2)}|{At(to, 0)},{At(to, 1)},{At(to, 2)}");
    }

    private static bool TryParseFingerprint(string fp, out (double fx, double fy, double fz, double tx, double ty, double tz) box)
    {
        box = default;
        var parts = fp.Split('|');
        if (parts.Length != 2)
        {
            return false;
        }

        if (!TryParseTriple(parts[0], out box.fx, out box.fy, out box.fz) ||
            !TryParseTriple(parts[1], out box.tx, out box.ty, out box.tz))
        {
            return false;
        }

        return true;
    }

    private static bool TryParseTriple(string s, out double a, out double b, out double c)
    {
        a = b = c = 0;
        var p = s.Split(',');
        if (p.Length != 3)
        {
            return false;
        }

        return double.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out a) &&
               double.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out b) &&
               double.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out c);
    }

    private static bool FingerprintNear(
        (double fx, double fy, double fz, double tx, double ty, double tz) a,
        (double fx, double fy, double fz, double tx, double ty, double tz) b,
        double tol) =>
        Math.Abs(a.fx - b.fx) <= tol && Math.Abs(a.fy - b.fy) <= tol && Math.Abs(a.fz - b.fz) <= tol &&
        Math.Abs(a.tx - b.tx) <= tol && Math.Abs(a.ty - b.ty) <= tol && Math.Abs(a.tz - b.tz) <= tol;

    private static bool PoseFingerprintNear(string a, string b, double tol)
    {
        if (!TryParsePoseFingerprint(a, out var pa) || !TryParsePoseFingerprint(b, out var pb))
        {
            return false;
        }

        return Math.Abs(pa.tx - pb.tx) <= tol && Math.Abs(pa.ty - pb.ty) <= tol && Math.Abs(pa.tz - pb.tz) <= tol &&
               Math.Abs(pa.rx - pb.rx) <= tol && Math.Abs(pa.ry - pb.ry) <= tol && Math.Abs(pa.rz - pb.rz) <= tol;
    }

    private static bool TryParsePoseFingerprint(string fp, out (double tx, double ty, double tz, double rx, double ry, double rz) pose)
    {
        pose = default;
        var parts = fp.Split('|');
        if (parts.Length != 2)
        {
            return false;
        }

        return TryParseTriple(parts[0], out pose.tx, out pose.ty, out pose.tz) &&
               TryParseTriple(parts[1], out pose.rx, out pose.ry, out pose.rz);
    }
}
