# Ghast family geometry and preview parity

This is the regression contract for Minecraft Java 26.1.2:

- `net.minecraft.client.model.monster.ghast.GhastModel`
- `net.minecraft.client.model.animal.ghast.HappyGhastModel`
- `net.minecraft.client.model.animal.ghast.HappyGhastHarnessModel`

The completed implementation is a direct bytecode lift. Do not restore the old ghast-only cuboid reflection, UV remapping, LER skip, or placement compensation.

## Javap ground truth

Use the exact pinned client jar at `tools/minecraft-parity/26.1.2/client.jar` or a byte-identical launcher copy.

### Monster ghast

- Body is a root child at `PartPose.offset(0, 17.6, 0)`.
- `tentacle0` through `tentacle8` are root children.
- Each tentacle uses `addBox(-1, 0, -1, 2, height, 2)` and starts at local `Y=0`.
- Tentacle pivots use root-space `Y=24.6`.
- The returned layer applies `MeshTransformer.scaling(4.5f)`.

### Happy ghast

- Body is a root child at `PartPose.offset(0, 16, 0)`.
- The nine tentacles are children of `body`, not root siblings.
- Tentacle pivots use body-local `Y=7`.
- The returned layer applies `MeshTransformer.scaling(4.0f)`.

### Root scale semantics

`MeshTransformer.scaling(scale)` transforms the mesh root with:

```text
uniformScale = scale
translationY = 24.016 * (1 - scale)
```

`LayerDefinitionMeshTransformerScaleStamp` lifts this terminal `LayerDefinition.apply(MeshTransformer.scaling(...))` pattern into the root pose. `GeometryIrMeshEmitter` composes `uniformScale` as a part-pose transform so descendants inherit it. Do not convert it into per-cuboid dimensions.

## Preview contract

Ghast family uses the normal runtime geometry path:

- Standard `GeometryIrLerBasisKind.StandardWorldRoot`.
- Standard LivingEntityRenderer column-root fold.
- Java `addBox` local extents remain `+Y`; do not reflect tentacle cuboids.
- Java cuboid face slots remain direct; do not swap up/down or invert face V specifically for ghasts.
- Animation changes tentacle `xRot` while preserving the lifted hierarchy and attachment pivots.

Placement assertions must compare composed runtime element matrices with JVM `renderPartAffines`. A green local-pose or shard test alone is insufficient because hierarchy, scale inheritance, LER composition, GPU bind preparation, or preview baking can still be wrong.

## Logical atlas versus physical PNG

The 26.1.2 jar stores padded ghast textures:

| Texture family | Geometry IR atlas | Physical PNG commonly loaded |
|---|---:|---:|
| Monster ghast | `64x32` | `128x64` |
| Happy ghast / harness | `64x64` | `128x128` |

Geometry UV texels are authored against the logical atlas. `EntityGeometryIrTextureAtlas.ResolveForBake` must therefore prefer the lifted geometry-IR shard `textureWidth` / `textureHeight` (same source as emit via `TryResolveGeometryIrAtlas`), then fall back to manifest `geometry_ir_texture_width` / `geometry_ir_texture_height` when no shard is available:

- initial `ResourcePackConverter` bake;
- `EntityEmulatedPreviewRebaker.TryRebakeMesh`;
- GPU bind-pose preparation.

The physical dimensions remain correct for texture upload. Baking UVs against the padded PNG dimensions produces partial faces and washed/misaligned previews while many mesh tests still pass.

**Baby-entity caveat:** manifest rows often still declare placeholder `64×64` (or other adult-sized atlases) while the lifted shard carries the true sheet size (`BabyAxolotl` `32×32`, `BabyChicken` `16×16`, etc.). `BabyCow` happens to match manifest and shard (`64×64`, no `faceMask` sheets), so it stayed correct when rebake used manifest-only sizing. Rebake must follow the shard so emit and bake normalize UVs identically.

## Regression coverage

Primary tests:

- `GhastFamilyLiftTests`: direct jar lift, root scale/translation, hierarchy, cuboid heights.
- `GhastPreviewAttachmentTests`: runtime affines, LER, local `+Y` cuboids, animation attachment, direct Java face slots, padded-atlas rebake.
- `EntityUvBakeGoldenTests`: baked ghast UV fingerprint.
- `GeometryIrLerMirrorComposeClassificationTests`: standard LER classification.
- `PreviewRenderingTests.GhastParityCatalogCpuBind`: renderer CPU/GPU commit and stale-cache behavior.

Useful focused commands:

```powershell
dotnet test tests/AutoPBR.GeometryCompiler.Tests --filter "GhastFamilyLiftTests|ChickenNestedPartLiftTests|MeshIslandMergeNestedChildrenTests"
dotnet test tests/AutoPBR.Core.Tests --filter "GhastPreviewAttachmentTests|EntityUvBakeGoldenTests|GeometryIrLerMirrorComposeClassificationTests|EntityTextureParityAssemblyCohesionTests"
dotnet test tests/AutoPBR.App.Tests --filter "FullyQualifiedName~ghast"
```

Tests are necessary but not sufficient. For final preview sign-off, inspect monster, shooting, happy, baby, and ropes/harness textures in Explore 3D. Confirm the full face is visible and all nine tentacles hang from the expected 3x3 positions.

## Files that carry the contract

- `src/AutoPBR.Tools.GeometryCompiler/LayerDefinitionMeshTransformerScaleStamp.cs`
- `src/AutoPBR.Tools.GeometryCompiler/JavapFloatGeometryMeshLift.PartTreeCollection.*.cs`
- `src/AutoPBR.Core/Preview/GeometryIrMeshEmitter.cs`
- `src/AutoPBR.Core/Preview/EntityGeometryIrTextureAtlas.cs`
- `src/AutoPBR.Core/Preview/EntityEmulatedPreviewRebaker.cs`
- `src/AutoPBR.Core/ResourcePackConverter.cs`
- `docs/generated/geometry/26.1.2/*Ghast*.json`

