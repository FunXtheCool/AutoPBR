import re
import pathlib

for rel in [
    "src/AutoPBR.Core/Preview/Entities/CleanRoomEntityModelRuntime.ParityCatalogDispatch.cs",
    "src/AutoPBR.Core/Preview/Entities/CleanRoomEntityDispatch.cs",
]:
    p = pathlib.Path(rel)
    text = p.read_text(encoding="utf-8")
    text = text.replace("VanillaAnimationIrPreviewSampler.", "DefinitionAnimationPreviewSampling.")
    lines = text.splitlines()
    out = []
    current_case = None
    for line in lines:
        m = re.match(r'\s*case "([^"]+)":', line)
        if m:
            current_case = m.group(1)
        if (
            "ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave)" in line
            and current_case
        ):
            line = line.replace(
                "ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave)",
                f'ComputePreviewStandardQuadrupedLegPitches(animationTimeSeconds, idlePhase01, wave, "{current_case}")',
            )
        out.append(line)
    p.write_text("\n".join(out) + ("\n" if text.endswith("\n") else ""), encoding="utf-8")
print("done")
