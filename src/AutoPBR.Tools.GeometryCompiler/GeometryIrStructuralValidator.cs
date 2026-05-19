using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AutoPBR.Tools.GeometryCompiler;

/// <summary>
/// Validates geometry IR shards for structural integrity and optional strict parity gates.
/// </summary>
public static class GeometryIrStructuralValidator
{
    private const double AdjacencyEpsilon = 1e-6;
    private const double AdjacencyTouchEpsilon = 0.02;

    private static readonly HashSet<string> StrictForbiddenCuboidWarnings = new(StringComparer.Ordinal)
    {
        "direction_mask_unparsed_set",
        "cube_deformation_obf_inferred"
    };

    private static readonly HashSet<string> StrictForbiddenPoseWarnings = new(StringComparer.Ordinal)
    {
        "pose_loop_unsupported"
    };

    private static readonly HashSet<string> NonExactLiftKinds = new(StringComparer.Ordinal)
    {
        "direction_mask_full_box",
        "unknown"
    };

    public readonly record struct Options(
        bool Strict = false,
        bool CheckAdjacency = false,
        bool RequireOkStatus = false);

    public readonly record struct Issue(string Path, string Code, string Message);

    public readonly record struct Result(bool IsValid, IReadOnlyList<Issue> Issues);

    public static Result ValidateFile(string shardPath, in Options options) =>
        ValidateJson(File.ReadAllText(shardPath), shardPath, options);

    public static Result ValidateJson(string json, string? contextPath, in Options options)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(json)!.AsObject();
        }
        catch (Exception ex)
        {
            return new Result(false, [new Issue(contextPath ?? "<json>", "parse_error", ex.Message)]);
        }

        return ValidateShard(root, contextPath, options);
    }

    public static Result ValidateShard(JsonObject root, string? contextPath, in Options options)
    {
        var issues = new List<Issue>();
        var ctx = contextPath ?? (string?)root["officialJvmName"] ?? "<shard>";

        if (root["schemaVersion"]?.GetValue<int>() is not 2)
        {
            issues.Add(new Issue(ctx, "schema_version", "schemaVersion must be 2"));
        }

        var status = (string?)root["extractionStatus"];
        if (options.RequireOkStatus && !string.Equals(status, "ok", StringComparison.Ordinal))
        {
            issues.Add(new Issue(ctx, "extraction_status", $"extractionStatus must be ok (was '{status ?? "<missing>"}')"));
        }

        if (options.Strict && string.Equals(status, "ok", StringComparison.Ordinal))
        {
            ValidateLiftSummaryStrict(root, ctx, issues);
        }

        if (root["roots"] is not JsonArray roots)
        {
            issues.Add(new Issue(ctx, "missing_roots", "roots array is required"));
            return new Result(issues.Count == 0, issues);
        }

        WalkPartTree(roots, $"{ctx}/roots", options, issues);

        if (options.CheckAdjacency)
        {
            CheckAdjacencyAllParts(roots, ctx, issues);
        }

        return new Result(issues.Count == 0, issues);
    }

    private static void ValidateLiftSummaryStrict(JsonObject root, string ctx, List<Issue> issues)
    {
        if (root["liftSummary"] is not JsonObject summary)
        {
            issues.Add(new Issue(ctx, "lift_summary_missing", "ok shard requires liftSummary"));
            return;
        }

        var cuboidApprox = summary["cuboidApproxCount"]?.GetValue<int>() ?? -1;
        var poseApprox = summary["poseApproxCount"]?.GetValue<int>() ?? -1;
        if (cuboidApprox != 0)
        {
            issues.Add(new Issue(ctx, "cuboid_approx", $"liftSummary.cuboidApproxCount must be 0 (was {cuboidApprox})"));
        }

        if (poseApprox != 0)
        {
            issues.Add(new Issue(ctx, "pose_approx", $"liftSummary.poseApproxCount must be 0 (was {poseApprox})"));
        }
    }

    private static void WalkPartTree(JsonArray parts, string path, in Options options, List<Issue> issues)
    {
        foreach (var node in parts)
        {
            if (node is not JsonObject part)
            {
                continue;
            }

            var partId = (string?)part["id"] ?? "<no-id>";
            var partPath = $"{path}/{partId}";

            if (part["pose"] is JsonObject pose)
            {
                ValidatePose(pose, partPath, options, issues);
            }

            if (part["cuboids"] is JsonArray cuboids)
            {
                foreach (var c in cuboids)
                {
                    if (c is JsonObject cuboid)
                    {
                        ValidateCuboid(cuboid, partPath, options, issues);
                    }
                }
            }

            if (part["children"] is JsonArray children)
            {
                WalkPartTree(children, partPath, options, issues);
            }
        }
    }

    private static void ValidatePose(JsonObject pose, string path, in Options options, List<Issue> issues)
    {
        if (options.Strict && pose["liftWarnings"] is JsonArray pw)
        {
            foreach (var w in pw)
            {
                var code = w?.GetValue<string>();
                if (code is not null && StrictForbiddenPoseWarnings.Contains(code))
                {
                    issues.Add(new Issue(path, "pose_lift_warning", $"forbidden pose liftWarning '{code}'"));
                }
            }
        }

        if (!TryReadVec3(pose["translation"], out _))
        {
            issues.Add(new Issue(path, "pose_translation", "pose.translation must be [x,y,z] numbers"));
        }

        if (!TryReadVec3(pose["rotationEulerRad"], out _))
        {
            issues.Add(new Issue(path, "pose_rotation", "pose.rotationEulerRad must be [x,y,z] numbers"));
        }
    }

    private static void ValidateCuboid(JsonObject cuboid, string path, in Options options, List<Issue> issues)
    {
        if (!TryReadVec3(cuboid["from"], out var from) || !TryReadVec3(cuboid["to"], out var to))
        {
            issues.Add(new Issue(path, "cuboid_bounds", "cuboid requires numeric from/to vec3"));
            return;
        }

        for (var i = 0; i < 3; i++)
        {
            var lo = Math.Min(from[i], to[i]);
            var hi = Math.Max(from[i], to[i]);
            if (hi - lo < -AdjacencyEpsilon)
            {
                issues.Add(new Issue(path, "cuboid_inverted_axis",
                    $"cuboid axis {i}: invalid span from[{from[i]}] to[{to[i]}]"));
            }
        }

        if (cuboid["uvOrigin"] is not JsonArray uv || uv.Count < 2 ||
            !HasInt(uv[0]) || !HasInt(uv[1]))
        {
            issues.Add(new Issue(path, "cuboid_uv", "cuboid.uvOrigin must be [int,int]"));
        }

        var liftKind = (string?)cuboid["liftKind"] ?? "exact";
        if (options.Strict)
        {
            if (NonExactLiftKinds.Contains(liftKind))
            {
                issues.Add(new Issue(path, "lift_kind", $"strict mode rejects liftKind '{liftKind}'"));
            }

            if (liftKind is "exact" &&
                cuboid["faceMask"] is JsonArray { Count: 0 })
            {
                issues.Add(new Issue(path, "empty_face_mask", "exact cuboid with empty faceMask is invalid for parity"));
            }

            if (cuboid["liftWarnings"] is JsonArray cw)
            {
                foreach (var w in cw)
                {
                    var code = w?.GetValue<string>();
                    if (code is not null && StrictForbiddenCuboidWarnings.Contains(code))
                    {
                        issues.Add(new Issue(path, "cuboid_lift_warning", $"forbidden cuboid liftWarning '{code}'"));
                    }
                }
            }
        }
    }

    private static bool TryReadVec3(JsonNode? node, out double[] v)
    {
        v = [0, 0, 0];
        if (node is not JsonArray a || a.Count < 3)
        {
            return false;
        }

        for (var i = 0; i < 3; i++)
        {
            if (!TryGetNumber(a[i], out v[i]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasInt(JsonNode? n) =>
        TryGetNumber(n, out var d) && d is >= int.MinValue and <= int.MaxValue && IsWholeNumber(d);

    private static bool IsWholeNumber(double d) =>
        Math.Abs(d - Math.Round(d)) < 1e-9;

    private static bool TryGetNumber(JsonNode? n, out double value)
    {
        value = 0;
        if (n is null)
        {
            return false;
        }

        if (n is JsonValue jv)
        {
            if (jv.TryGetValue<double>(out value))
            {
                return true;
            }

            if (jv.TryGetValue<int>(out var iv))
            {
                value = iv;
                return true;
            }
        }

        return false;
    }

    private static void CheckAdjacencyAllParts(JsonArray parts, string ctx, List<Issue> issues)
    {
        foreach (var node in parts)
        {
            if (node is not JsonObject part || part["cuboids"] is not JsonArray cuboids || cuboids.Count < 2)
            {
                if (node is JsonObject p && p["children"] is JsonArray ch)
                {
                    CheckAdjacencyAllParts(ch, ctx, issues);
                }

                continue;
            }

            var boxes = new List<(double x0, double y0, double z0, double x1, double y1, double z1)>();
            foreach (var c in cuboids)
            {
                if (c is not JsonObject co ||
                    !TryReadVec3(co["from"], out var from) ||
                    !TryReadVec3(co["to"], out var to))
                {
                    continue;
                }

                boxes.Add((from[0], from[1], from[2], to[0], to[1], to[2]));
            }

            var partId = (string?)part["id"] ?? "?";
            CheckCuboidAdjacency(boxes, $"{ctx}/roots/{partId}", issues);

            if (part["children"] is JsonArray children)
            {
                CheckAdjacencyAllParts(children, ctx, issues);
            }
        }
    }

    private static void CheckCuboidAdjacency(
        List<(double x0, double y0, double z0, double x1, double y1, double z1)> boxes,
        string path,
        List<Issue> issues)
    {
        for (var i = 0; i < boxes.Count; i++)
        {
            for (var j = i + 1; j < boxes.Count; j++)
            {
                var a = boxes[i];
                var b = boxes[j];
                if (!TryGetSharedFacePlane(a, b, out var axis, out var plane))
                {
                    continue;
                }

                if (!RangesOverlapOnOtherAxes(a, b, axis))
                {
                    continue;
                }

                var aFace = GetFaceCoord(a, axis, positive: plane > (axis switch
                {
                    0 => (a.x0 + a.x1) * 0.5,
                    1 => (a.y0 + a.y1) * 0.5,
                    _ => (a.z0 + a.z1) * 0.5
                }));
                var bFace = GetFaceCoord(b, axis, positive: plane < (axis switch
                {
                    0 => (b.x0 + b.x1) * 0.5,
                    1 => (b.y0 + b.y1) * 0.5,
                    _ => (b.z0 + b.z1) * 0.5
                }));

                if (Math.Abs(aFace - bFace) > AdjacencyEpsilon)
                {
                    issues.Add(new Issue(path, "adjacency_gap",
                        $"cuboids {i} and {j} share axis {axis} but faces differ by {Math.Abs(aFace - bFace):G4}"));
                }
            }
        }
    }

    private static double GetFaceCoord(
        (double x0, double y0, double z0, double x1, double y1, double z1) b,
        int axis,
        bool positive) =>
        axis switch
        {
            0 => positive ? b.x1 : b.x0,
            1 => positive ? b.y1 : b.y0,
            _ => positive ? b.z1 : b.z0
        };

    private static bool TryGetSharedFacePlane(
        (double x0, double y0, double z0, double x1, double y1, double z1) a,
        (double x0, double y0, double z0, double x1, double y1, double z1) b,
        out int axis,
        out double plane)
    {
        axis = -1;
        plane = 0;
        if (PlanesEqual(a.x1, b.x0))
        {
            axis = 0;
            plane = a.x1;
            return true;
        }

        if (PlanesEqual(b.x1, a.x0))
        {
            axis = 0;
            plane = b.x1;
            return true;
        }

        if (PlanesEqual(a.y1, b.y0))
        {
            axis = 1;
            plane = a.y1;
            return true;
        }

        if (PlanesEqual(b.y1, a.y0))
        {
            axis = 1;
            plane = b.y1;
            return true;
        }

        if (PlanesEqual(a.z1, b.z0))
        {
            axis = 2;
            plane = a.z1;
            return true;
        }

        if (PlanesEqual(b.z1, a.z0))
        {
            axis = 2;
            plane = b.z1;
            return true;
        }

        return false;
    }

    private static bool PlanesEqual(double a, double b) => Math.Abs(a - b) <= AdjacencyTouchEpsilon;

    private static bool RangesOverlapOnOtherAxes(
        (double x0, double y0, double z0, double x1, double y1, double z1) a,
        (double x0, double y0, double z0, double x1, double y1, double z1) b,
        int sharedAxis)
    {
        return sharedAxis switch
        {
            0 => RangesOverlap(a.y0, a.y1, b.y0, b.y1) && RangesOverlap(a.z0, a.z1, b.z0, b.z1),
            1 => RangesOverlap(a.x0, a.x1, b.x0, b.x1) && RangesOverlap(a.z0, a.z1, b.z0, b.z1),
            _ => RangesOverlap(a.x0, a.x1, b.x0, b.x1) && RangesOverlap(a.y0, a.y1, b.y0, b.y1)
        };
    }

    private static bool RangesOverlap(double a0, double a1, double b0, double b1) =>
        Math.Max(a0, b0) < Math.Min(a1, b1) - AdjacencyEpsilon;

    public static int RunValidateGeometryCommand(string[] args)
    {
        var strict = false;
        var adjacency = false;
        var requireOk = false;
        string? versionLabel = null;
        string? outDir = null;

        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (string.Equals(a, "--strict", StringComparison.OrdinalIgnoreCase))
            {
                strict = true;
                requireOk = true;
            }
            else if (string.Equals(a, "--adjacency", StringComparison.OrdinalIgnoreCase))
            {
                adjacency = true;
            }
            else if (string.Equals(a, "--require-ok", StringComparison.OrdinalIgnoreCase))
            {
                requireOk = true;
            }
            else if (string.Equals(a, "--out-dir", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                outDir = Path.GetFullPath(args[++i]);
            }
            else if (!a.StartsWith('-'))
            {
                versionLabel = a;
            }
        }

        var root = Program.FindRepoRoot();
        outDir ??= Path.Combine(root, "docs", "generated");
        versionLabel ??= "26.1.2";

        var geoDir = Path.Combine(outDir, "geometry", versionLabel);
        if (!Directory.Exists(geoDir))
        {
            Console.Error.WriteLine($"Geometry directory not found: {geoDir}");
            return 2;
        }

        var options = new Options(strict, adjacency, requireOk);
        var failCount = 0;
        var okCount = 0;
        foreach (var file in Directory.EnumerateFiles(geoDir, "*.json").OrderBy(static f => f, StringComparer.Ordinal))
        {
            if (options.Strict && !ShardClaimsOk(file))
            {
                continue;
            }

            var result = ValidateFile(file, options);
            if (result.IsValid)
            {
                okCount++;
                continue;
            }

            failCount++;
            Console.Error.WriteLine(Path.GetFileName(file));
            foreach (var issue in result.Issues)
            {
                Console.Error.WriteLine($"  [{issue.Code}] {issue.Path}: {issue.Message}");
            }
        }

        Console.WriteLine(
            string.Create(CultureInfo.InvariantCulture,
                $"validate-geometry {versionLabel}: {okCount} passed, {failCount} failed (strict={strict}, adjacency={adjacency})"));

        return failCount > 0 ? 1 : 0;
    }

    private static bool ShardClaimsOk(string filePath)
    {
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(filePath));
            return string.Equals(doc.RootElement.GetProperty("extractionStatus").GetString(), "ok",
                StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }
}
