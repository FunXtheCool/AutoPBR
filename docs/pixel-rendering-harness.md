# Live GL pixel rendering harness

The pixel rendering harness is the post-roadmap correctness gate for the desktop GL acceleration lanes. It renders deterministic fixtures into a private RGBA8/depth framebuffer, normalizes OpenGL readback to a top-left origin, and compares complete images rather than treating successful GL calls as proof of visual parity.

## Runtime policy

- The harness is opt-in and is never constructed by the production preview frame loop.
- Normal application rendering pays no readback, synchronization, comparison, image encoding, or artifact-writing cost.
- The ordinary test run covers the CPU-side snapshot, row normalization, tolerance, mismatch-bound, fingerprint, and diff-image logic. A real WGL context runs only when explicitly enabled.
- Same-context lane comparisons are the primary oracle. Driver-specific hashes are recorded for diagnostics, not used as portable golden values.

## Live matrix

The fixed 160x120 WGL fixture currently verifies:

1. Direct `DrawRange` submission as the reference image.
2. One `glDrawElementsIndirect` call per command.
3. Grouped `glMultiDrawElementsIndirect` submission.
4. Compute-compacted commands consumed by `glMultiDrawElementsIndirectCount` without CPU count readback.
5. Four legacy `sampler2D` bindings against one four-layer `sampler2DArray` binding.

Unavailable capability lanes are reported and skipped. Every available lane must match its reference exactly. The comparison API also supports per-channel tolerance, a maximum changed-pixel ratio, mean absolute error, RMSE, and top-left mismatch bounds for future shader paths where a small driver rounding allowance is justified.

## Run it

PowerShell from the repository root:

```powershell
$env:AUTOPBR_RUN_PIXEL_GL_HARNESS = '1'
$env:AUTOPBR_PIXEL_HARNESS_ARTIFACT_DIR = 'artifacts/pixel-rendering-harness'
dotnet test tests/AutoPBR.App.Tests/AutoPBR.App.Tests.csproj --filter "FullyQualifiedName=AutoPBR.App.Tests.PreviewPixelRenderingHarnessTests.HiddenWglContext_AccelerationLanesMatchDirectPixelBaseline"
```

`AUTOPBR_RUN_LIVE_GL_SMOKE=1` also enables the pixel harness so the broader desktop GL smoke run includes pixel parity. `AUTOPBR_PIXEL_HARNESS_ARTIFACT_DIR` is optional; relative paths resolve from the repository root. When set, the run writes one PNG per lane plus `pixel-harness-report.json`. A failed comparison writes the same evidence to a temporary directory even when no artifact directory was configured, including an amplified RGB difference image.

The JSON report contains the GL capability diagnostic, dimensions, FNV-1a fingerprints, exact comparison metrics, mismatch bounds, and execution diagnostics. It is intended as durable CI or local evidence without making one GPU driver's byte hash the cross-vendor contract.
