from __future__ import annotations

import csv
import json
import re
from pathlib import Path


def main() -> None:
    root = Path(__file__).resolve().parents[1]

    inventory = json.loads(
        (root / "src/AutoPBR.Core/Data/minecraft-native/minecraft_26.1.2_entity_textures.json").read_text(
            encoding="utf-8"
        )
    )
    manifest = json.loads(
        (
            root
            / "src/AutoPBR.Core/Data/minecraft-native/minecraft_26.1.2_entity_texture_model_manifest.json"
        ).read_text(encoding="utf-8")
    )

    dispatch_text = (
        root / "src/AutoPBR.Core/Preview/Entities/CleanRoomEntityModelRuntime.ParityCatalogDispatch.cs"
    ).read_text(encoding="utf-8")
    dispatch_cases = set(re.findall(r'case "([^"]+)"', dispatch_text))

    fallback_builders = {
        "HumanoidGeneric",
        "QuadrupedFamily",
        "FlyingFamily",
        "AquaticFamily",
        "Unknown",
        "HumanoidZombie",
        "HumanoidSkeleton",
        "PlayerHumanoid",
        "EquipmentLayer",
    }

    test_files = [
        root / "tests/AutoPBR.Core.Tests/EntityTextureParityCatalogTests.cs",
        root / "tests/AutoPBR.Core.Tests/EntityTextureParityJsonCatalogTests.cs",
        root / "tests/AutoPBR.Core.Tests/EntityTextureRoutingInventoryTests.cs",
        root / "tests/AutoPBR.Core.Tests/MinecraftJavaModelPreviewTests.cs",
    ]
    test_text = "\n".join(path.read_text(encoding="utf-8").lower() for path in test_files)

    files_by_folder: dict[str, list[str]] = {}
    for f in inventory["files"]:
        files_by_folder.setdefault(f["folder"], []).append(f["path"])

    folders = sorted(inventory["entity_folders"])
    output_dir = root / "artifacts" / "entity_parity_rollout"
    output_dir.mkdir(parents=True, exist_ok=True)
    output_csv = output_dir / "entity_coverage_matrix_26_1_2.csv"

    with output_csv.open("w", encoding="utf-8", newline="") as fh:
        writer = csv.writer(fh)
        writer.writerow(
            [
                "folder",
                "inventory_file_count",
                "catalogued",
                "manifest_mapped",
                "dispatch_implemented",
                "specific_mesh_built",
                "test_covered",
                "classification",
                "builders",
            ]
        )

        for folder in folders:
            direct_files = files_by_folder.get(folder, [])
            descendant_files = [
                f["path"] for f in inventory["files"] if f["folder"].startswith(f"{folder}/")
            ]
            inv_count = len(direct_files) + len(descendant_files)
            folder_prefix = f"assets/minecraft/textures/entity/{folder}/"
            folder_rules = [
                rule
                for rule in manifest["rules"]
                if rule["path_prefix"].startswith(folder_prefix)
                or rule["path_prefix"] == folder_prefix.removesuffix("/")
            ]
            builders = sorted({rule["builder_method"] for rule in folder_rules})

            catalogued = inv_count > 0
            manifest_mapped = catalogued and bool(folder_rules)
            dispatch_implemented = manifest_mapped and all(builder in dispatch_cases for builder in builders)
            specific_mesh_built = dispatch_implemented and all(
                builder not in fallback_builders for builder in builders
            )
            test_covered = (
                folder.lower() in test_text
                or any(builder.lower() in test_text for builder in builders)
            )

            if catalogued and manifest_mapped and dispatch_implemented and specific_mesh_built and test_covered:
                classification = "already_green"
            elif catalogued and (not manifest_mapped or not dispatch_implemented):
                classification = "dispatch_missing"
            elif catalogued and dispatch_implemented and not specific_mesh_built:
                classification = "builder_quality_gap"
            elif catalogued and manifest_mapped and dispatch_implemented and specific_mesh_built and not test_covered:
                classification = "test_gap_only"
            else:
                classification = "out_of_scope"

            writer.writerow(
                [
                    folder,
                    inv_count,
                    str(catalogued).lower(),
                    str(manifest_mapped).lower(),
                    str(dispatch_implemented).lower(),
                    str(specific_mesh_built).lower(),
                    str(test_covered).lower(),
                    classification,
                    ";".join(builders),
                ]
            )

    print(output_csv)


if __name__ == "__main__":
    main()
