import pathlib

p = pathlib.Path("src/AutoPBR.Core/Preview/VanillaAnimationIrPreviewSampler.cs")
lines = p.read_text(encoding="utf-8").splitlines()
keep = []
skip = False
for i, line in enumerate(lines):
    if i == 14 and line.strip() == "/// <summary>":
        # Start skipping per-entity TrySample* until ResolveAnimationVersionLabel
        skip = True
    if skip and line.strip().startswith("private static string? ResolveAnimationVersionLabel"):
        skip = False
    if skip:
        continue
    # Skip duplicate definition-channel wrappers before TrySampleDefinitionChannelDegrees
    if line.strip().startswith("/// <summary><c>CamelAnimation.CAMEL_WALK</c>"):
        skip = True
    if skip and line.strip().startswith("private static bool TrySampleDefinitionChannelDegrees"):
        skip = False
    if skip:
        continue
    keep.append(line)

p.write_text("\n".join(keep) + "\n", encoding="utf-8")
print(f"trimmed to {len(keep)} lines")
