# Build performance

Guidance for faster local builds and how we measure compile time. See also [project quality standards](project-quality-standards.md).

## Baseline (2026-07-08, Windows, .NET 8 SDK)

Measured on this repo after the Preview/Ml assembly split. Binlogs are written under `.tmpbuild/` (gitignored).

| Scenario | Command | Time |
|----------|---------|------|
| App clean build | `dotnet build src/AutoPBR.App/AutoPBR.App.csproj -c Debug` | ~35 s |
| App incremental rebuild (after icon fix) | same | ~7 s |
| App incremental (touch `Rendering/OpenGL/*.cs`) | same | ~15 s |
| App incremental (touch `AutoPBR.Core/*.cs`) | same | ~10 s |
| Full solution Debug | `dotnet build AutoPBR.sln -c Debug` | ~17 s |

**Hot spots identified from verbose logs:**

- `GenerateAppIcon` ran nested `dotnet run` on **every** App build when `Assets/AutoPBR-logo.png` exists (**fixed:** MSBuild `Inputs`/`Outputs` on the target; routine rebuilds skip with “all output files are up-to-date”).
- Full solution builds compile all test and tool projects even when only running the desktop app.
- `AutoPBR.Preview` copies ~578 JSON shards from `docs/generated/**` via MSBuild content globs (see [Preview data during UI work](#preview-data-during-ui-work)).

### Reproducing timings

```powershell
# Clean App build + binlog
dotnet clean src/AutoPBR.App/AutoPBR.App.csproj -c Debug
dotnet build src/AutoPBR.App/AutoPBR.App.csproj -c Debug /bl:.tmpbuild/app-clean.binlog

# Incremental (touch one render file, rebuild)
(Get-Item src/AutoPBR.App/Rendering/OpenGL/OpenGlPreviewBackend.cs).LastWriteTime = Get-Date
dotnet build src/AutoPBR.App/AutoPBR.App.csproj -c Debug /bl:.tmpbuild/app-incr.binlog
```

Open `.binlog` files in [MSBuild Structured Log Viewer](https://msbuildlog.com/).

## Which build to use when

| Work | Build target |
|------|----------------|
| **App UI / OpenGL preview (daily)** | `AutoPBR.App.slnf` or `dotnet build src/AutoPBR.App/AutoPBR.App.csproj` |
| **Core conversion / CLI** | `dotnet build src/AutoPBR.Core/AutoPBR.Core.csproj` + `src/AutoPBR.Cli` |
| **Preview / parity tests** | `dotnet build tests/AutoPBR.Preview.Tests/AutoPBR.Preview.Tests.csproj` or `AutoPBR.Core.slnf` |
| **Full CI parity** | `dotnet build AutoPBR.sln -c Release` |

**Core.Tests** now compiles only Core/ML conversion tests (~19 files). Preview/parity suites live in **Preview.Tests** (~165 files).

Use **F5 / launch** with the VS Code/Cursor **Build App (fast path)** task — not a full solution build.

## Preview data during UI work

Regenerating `docs/generated/**` (geometry indexes, animation shards, etc.) changes hundreds of files and forces `AutoPBR.Preview` content copy work on the next build. During App UI sessions:

- Avoid running index/geometry generator scripts unless you need fresh IR data.
- If you must regenerate, expect the next Preview/App build to take longer; subsequent builds are incremental (`PreserveNewest`).

A future `AutoPBR.PreviewData` content-only project could isolate data rebuilds from Preview `.cs` changes if binlogs show glob evaluation as a top cost.

## Analyzers

Debug builds keep .NET analyzers enabled (`Directory.Build.props`, `AnalysisLevel` 5.0). CI Release uses `latest-recommended`. Do not disable analyzers locally without team agreement.

## Watch mode (optional)

For rapid App iteration without rebuilding manually:

```bash
dotnet watch run --project src/AutoPBR.App/AutoPBR.App.csproj
```
