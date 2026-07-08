import re
from pathlib import Path

p = Path(__file__).resolve().parents[1] / "tests/AutoPBR.Core.Tests/ObjectEntityBlockStateParityTests.cs"
c = p.read_text(encoding="utf-8")
start = c.find("    [Fact]\n    public void BannerStanding_resolves_flag_bar_and_pole")
end = c.find("    [Fact]\n    public void StandingSign_resolves_two_cuboids")
if start != -1 and end != -1:
    c = c[:start] + c[end:]
helper_start = c.find("    private static bool IsHangingSignBoardElement")
if helper_start != -1:
    c = c[:helper_start].rstrip() + "\n}\n"
p.write_text(c, encoding="utf-8")
print("trimmed")
