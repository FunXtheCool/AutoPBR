using System.Buffers;

namespace AutoPBR.Training.Ort;

/// <summary>Entry point for specular ORT training (mirrors Python <c>train_spec --trainer-backend ort</c>).</summary>
public static class SpecularOrtTrainingCli
{
    public static int Run(string[] args)
    {
        var opts = ArgParser.Parse(args);
        if (opts.DataRoot is null)
        {
            Console.Error.WriteLine("--data-root is required.");
            return 1;
        }

        var artifactsDir = Path.GetFullPath(opts.OrtArtifactsDir);
        var trainOnnx = Path.Combine(artifactsDir, "training_model.onnx");
        if (!File.Exists(trainOnnx))
        {
            trainOnnx = Path.Combine(artifactsDir, "train_model.onnx");
        }

        var evalOnnx = Path.Combine(artifactsDir, "eval_model.onnx");
        var optOnnx = Path.Combine(artifactsDir, "optimizer_model.onnx");
        foreach (var (label, path) in new[] { ("training_model.onnx / train_model.onnx", trainOnnx), ("eval_model.onnx", evalOnnx), ("optimizer_model.onnx", optOnnx) })
        {
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"Missing ORT artifact ({label}): {path}");
                Console.Error.WriteLine(
                    "Generate with Python, e.g. ml_specular.generate_ort_training_artifacts (see repo tools/MlSpecularTrainer).");
                return 1;
            }
        }

        string ckptLoad;
        try
        {
            ckptLoad = SpecularOrtCheckpointPaths.ResolveLoadPath(artifactsDir, opts.OrtCheckpoint);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }

        var ckptSave = SpecularOrtCheckpointPaths.SessionSavePath(artifactsDir, opts.OrtCheckpoint);
        Console.WriteLine($"[ORT.NET] Artifacts: {artifactsDir}");
        Console.WriteLine($"[ORT.NET] Load checkpoint: {ckptLoad}");
        Console.WriteLine($"[ORT.NET] Save checkpoint: {ckptSave}");

        using var trainer = new OrtSpecularTrainer(
            trainOnnx,
            evalOnnx,
            optOnnx,
            ckptLoad,
            sessionOptions: null,
            preferCudaSessionOptions: !opts.Cpu);

        trainer.SetLearningRate(opts.LearningRate);

        var trainDs = new SpecularManifestDataset(
            opts.DataRoot,
            "train",
            trainRes: opts.TrainRes,
            inChannels: opts.InChannels,
            augment: true,
            alphaIgnoreBelow: 128,
            randomSeed: opts.Seed);

        var valDs = new SpecularManifestDataset(
            opts.DataRoot,
            "val",
            trainRes: opts.TrainRes,
            inChannels: opts.InChannels,
            augment: false,
            alphaIgnoreBelow: 128,
            randomSeed: opts.Seed);

        if (trainDs.Count == 0)
        {
            Console.Error.WriteLine("No training samples.");
            return 1;
        }

        var w = opts.TrainRes;
        var h = opts.TrainRes;
        var c = opts.InChannels;
        var order = new int[trainDs.Count];
        for (var i = 0; i < order.Length; i++)
        {
            order[i] = i;
        }

        var rng = opts.Seed is { } s ? new Random(s) : new Random();

        var sampleIn = new float[c * h * w];
        var sampleT = new float[4 * h * w];
        var sampleV = new float[h * w];
        var scratchRgb = new byte[h * w * 3];

        var maxBatch = Math.Max(1, opts.Batch);
        var pooledIn = ArrayPool<float>.Shared.Rent(maxBatch * c * h * w);
        var pooledT = ArrayPool<float>.Shared.Rent(maxBatch * 4 * h * w);
        var pooledV = ArrayPool<float>.Shared.Rent(maxBatch * h * w);
        try
        {
            for (var epoch = 0; epoch < opts.Epochs; epoch++)
            {
                Shuffle(order, rng);
                var trLoss = 0.0;
                var nTr = 0;
                for (var start = 0; start < order.Length; start += opts.Batch)
                {
                    var n = Math.Min(opts.Batch, order.Length - start);
                    FillBatch(
                        trainDs,
                        order,
                        start,
                        n,
                        c,
                        h,
                        w,
                        pooledIn,
                        pooledT,
                        pooledV,
                        sampleIn,
                        sampleT,
                        sampleV,
                        scratchRgb);
                    using var batch = SpecularOrtBatchTensors.Create(
                        n,
                        c,
                        h,
                        w,
                        pooledIn.AsSpan(0, n * c * h * w),
                        pooledT.AsSpan(0, n * 4 * h * w),
                        pooledV.AsSpan(0, n * h * w),
                        opts.TransparentZeroWeight);
                    var loss = trainer.TrainOneStep(batch.Feeds);
                    trLoss += loss * n;
                    nTr += n;
                }

                trLoss /= Math.Max(nTr, 1);

                var vaLoss = 0.0;
                var nVa = 0;
                for (var start = 0; start < valDs.Count; start += opts.Batch)
                {
                    var n = Math.Min(opts.Batch, valDs.Count - start);
                    FillBatchSequential(
                        valDs,
                        start,
                        n,
                        c,
                        h,
                        w,
                        pooledIn,
                        pooledT,
                        pooledV,
                        sampleIn,
                        sampleT,
                        sampleV,
                        scratchRgb);
                    using var batch = SpecularOrtBatchTensors.Create(
                        n,
                        c,
                        h,
                        w,
                        pooledIn.AsSpan(0, n * c * h * w),
                        pooledT.AsSpan(0, n * 4 * h * w),
                        pooledV.AsSpan(0, n * h * w),
                        opts.TransparentZeroWeight);
                    var loss = trainer.EvalOneStep(batch.Feeds);
                    vaLoss += loss * n;
                    nVa += n;
                }

                vaLoss /= Math.Max(nVa, 1);
                Console.WriteLine(
                    $"[ORT.NET] epoch {epoch + 1}/{opts.Epochs}  train_loss={trLoss:F4}  val_loss={vaLoss:F4}");
            }
        }
        finally
        {
            ArrayPool<float>.Shared.Return(pooledIn);
            ArrayPool<float>.Shared.Return(pooledT);
            ArrayPool<float>.Shared.Return(pooledV);
        }

        try
        {
            trainer.SaveCheckpoint(ckptSave, includeOptimizerState: true);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Warning: save with optimizer state failed ({ex.Message}); retrying without.");
            try
            {
                trainer.SaveCheckpoint(ckptSave, includeOptimizerState: false);
            }
            catch (Exception ex2)
            {
                Console.Error.WriteLine($"Failed to save ORT checkpoint: {ex2.Message}");
                return 1;
            }
        }

        Console.WriteLine($"[ORT.NET] Saved CheckpointState: {ckptSave}");
        return 0;
    }

    private static void Shuffle(int[] order, Random rng)
    {
        for (var i = order.Length - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (order[i], order[j]) = (order[j], order[i]);
        }
    }

    private static void FillBatch(
        SpecularManifestDataset ds,
        int[] indices,
        int start,
        int count,
        int c,
        int h,
        int w,
        float[] batchIn,
        float[] batchT,
        float[] batchV,
        float[] sampleIn,
        float[] sampleT,
        float[] sampleV,
        byte[] scratchRgb)
    {
        for (var b = 0; b < count; b++)
        {
            var idx = indices[start + b];
            var inOff = b * (c * h * w);
            var tOff = b * (4 * h * w);
            var vOff = b * (h * w);
            ds.GetSample(
                idx,
                sampleIn.AsSpan(),
                sampleT.AsSpan(),
                sampleV.AsSpan(),
                scratchRgb);
            sampleIn.AsSpan().CopyTo(batchIn.AsSpan(inOff, c * h * w));
            sampleT.AsSpan().CopyTo(batchT.AsSpan(tOff, 4 * h * w));
            sampleV.AsSpan().CopyTo(batchV.AsSpan(vOff, h * w));
        }
    }

    private static void FillBatchSequential(
        SpecularManifestDataset ds,
        int start,
        int count,
        int c,
        int h,
        int w,
        float[] batchIn,
        float[] batchT,
        float[] batchV,
        float[] sampleIn,
        float[] sampleT,
        float[] sampleV,
        byte[] scratchRgb)
    {
        for (var b = 0; b < count; b++)
        {
            var idx = start + b;
            var inOff = b * (c * h * w);
            var tOff = b * (4 * h * w);
            var vOff = b * (h * w);
            ds.GetSample(
                idx,
                sampleIn.AsSpan(),
                sampleT.AsSpan(),
                sampleV.AsSpan(),
                scratchRgb);
            sampleIn.AsSpan().CopyTo(batchIn.AsSpan(inOff, c * h * w));
            sampleT.AsSpan().CopyTo(batchT.AsSpan(tOff, 4 * h * w));
            sampleV.AsSpan().CopyTo(batchV.AsSpan(vOff, h * w));
        }
    }

    private sealed record TrainingOptions(
        string? DataRoot,
        string OrtArtifactsDir,
        string? OrtCheckpoint,
        int Epochs,
        int Batch,
        float LearningRate,
        int TrainRes,
        int InChannels,
        float TransparentZeroWeight,
        int? Seed,
        bool Cpu);

    private static class ArgParser
    {
        public static TrainingOptions Parse(string[] args)
        {
            string? dataRoot = null;
            var artifacts = "artifacts/ort";
            string? ckpt = null;
            var epochs = 40;
            var batch = 4;
            var lr = 1e-3f;
            var trainRes = 128;
            var inCh = 4;
            var tw = 0.5f;
            int? seed = null;
            var cpu = false;

            for (var i = 0; i < args.Length; i++)
            {
                string? Next() => i + 1 < args.Length ? args[++i] : null;

                switch (args[i])
                {
                    case "--data-root":
                        dataRoot = Next() is { } d ? Path.GetFullPath(d) : null;
                        break;
                    case "--ort-artifacts-dir":
                        if (Next() is { } a)
                        {
                            artifacts = a;
                        }

                        break;
                    case "--ort-checkpoint":
                        ckpt = Next();
                        break;
                    case "--epochs":
                        if (int.TryParse(Next(), out var e))
                        {
                            epochs = e;
                        }

                        break;
                    case "--batch":
                        if (int.TryParse(Next(), out var b))
                        {
                            batch = b;
                        }

                        break;
                    case "--lr":
                        if (float.TryParse(Next(), out var l))
                        {
                            lr = l;
                        }

                        break;
                    case "--train-res":
                        if (int.TryParse(Next(), out var r))
                        {
                            trainRes = r;
                        }

                        break;
                    case "--in-channels":
                        if (int.TryParse(Next(), out var ic) && ic is 3 or 4)
                        {
                            inCh = ic;
                        }

                        break;
                    case "--transparent-zero-weight":
                        if (float.TryParse(Next(), out var tz))
                        {
                            tw = tz;
                        }

                        break;
                    case "--seed":
                        if (int.TryParse(Next(), out var sd))
                        {
                            seed = sd;
                        }

                        break;
                    case "--cpu":
                        cpu = true;
                        break;
                    case "--help":
                    case "-h":
                        PrintHelp();
                        Environment.Exit(0);
                        break;
                }
            }

            return new TrainingOptions(
                dataRoot,
                artifacts,
                ckpt,
                epochs,
                batch,
                lr,
                trainRes,
                inCh,
                tw,
                seed,
                cpu);
        }

        private static void PrintHelp()
        {
            Console.WriteLine(
                """
                AutoPBR ORT specular training (.NET)

                  --data-root <path>           Dataset root (manifest.jsonl, splits/train.txt, splits/val.txt) [required]
                  --ort-artifacts-dir <path>   Default: artifacts/ort
                  --ort-checkpoint <path>      Session checkpoint load/save; default: <ort-artifacts-dir>/ort_training_state
                  --epochs <n>                 Default: 40
                  --batch <n>                  Default: 4
                  --lr <f>                     Default: 1e-3
                  --train-res <n>              Default: 128
                  --in-channels <3|4>          Default: 4
                  --transparent-zero-weight <f> Default: 0.5
                  --seed <n>                   RNG seed (shuffle + augment)
                  --cpu                        Force CPU session options (no CUDA EP)
                """);
        }
    }
}
