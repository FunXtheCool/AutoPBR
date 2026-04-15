# AutoPBR

AutoPBR generates a **PBR overlay** for a Minecraft resource pack (input `.zip` / `.jar`) by creating:

- **LabPBR specular** (`*_s.png`)
- **Normal maps** (`*_n.png`, with height packed in alpha for POM-style workflows)

Output is a **separate** `.zip` containing only generated textures + `pack.mcmeta` / `pack.png` (when present). Stack it above the base pack in-game.

## Projects

- `src/AutoPBR.App`: desktop UI (Avalonia)
- `src/AutoPBR.Cli`: command-line tool
- `src/AutoPBR.Core`: conversion engine

## Requirements

- **.NET 8** (SDK to build, runtime to run)
- **Optional GPU normals (DeepBump) and GPU specular (my own model)**: uses ONNX Runtime CUDA on Windows when CUDA/cuDNN DLLs are available. See `src/AutoPBR.Core/Data/native/README.md`.

## CLI (quick start)

```bash
dotnet run --project src/AutoPBR.Cli -- "in_pack.zip" "out_pack_PBR.zip" --fast --normal 1.5 --height 0.12
```

Common flags:
- `--fast`
- `--normal <float>`
- `--height <float>`
- `--ignore-plants`
- `--tag-rules <file.json>`

## App (quick start)

```bash
dotnet run --project src/AutoPBR.App
```

## Build

```bash
dotnet build AutoPBR.sln
```

## ML specular (optional)

AutoPBR can use an ONNX **specular predictor** (`diffuse -> _s RGBA`) when enabled in the app/CLI.

Trainer + sample dataset live in `tools/MlSpecularTrainer`. Channel semantics and ONNX ↔ LabPBR alignment are documented in [`docs/ml-specular-labpbr-contract.md`](docs/ml-specular-labpbr-contract.md).

## Tags & semantic material (Explore)

- [Tag / keyword system](docs/tag-keyword-system.md) — rules, keywords, MiniLM, UI.
- [Weighted vs Unweighted (heuristic vs ML)](docs/semantic-material-weighted-unweighted.md) — when MiniLM runs, flag meanings, and `MaterialTagSemanticResolution`.

