# Minecraft Java 26.1.2 parity artifacts

- `version.json` — Mojang launcher package metadata for 26.1.2 (includes `downloads.client` for `client.jar`).
- `client.jar` — optional local download (gitignored). Used by `EntityTextureRoutingInventoryTests` and javap workflows.
- `client_mappings.txt` — **not** shipped in Mojang’s `version.json` `downloads` for 26.1.2. The **client.jar** instead contains **named** bytecode under `net/minecraft/client/model/**` (no separate mapping file). Regenerate the class index with `tools/Generate-MinecraftClientModelIndex.ps1 -ClientJar …/26.1.2/client.jar -VersionLabel 26.1.2` and **omit** `-Mappings` (see [`docs/generated/README.md`](../../docs/generated/README.md)).

## Ghast-family lift note

`GhastModel`, `HappyGhastModel`, and `HappyGhastHarnessModel` are direct-lift regression cases for computed tentacle names, receiver-local parent recovery, and terminal `LayerDefinition.apply(MeshTransformer.scaling(...))`. See [`docs/ghast-family-parity.md`](../../../docs/ghast-family-parity.md) before changing their lift or preview behavior.
