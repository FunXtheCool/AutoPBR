"""Extract partial class files from ObjectEntityBlockStateParityTests.cs by method name."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
MAIN = ROOT / "tests/AutoPBR.Core.Tests/ObjectEntityBlockStateParityTests.cs"


def find_test_start(lines: list[str], method_marker: str) -> int:
    for i, line in enumerate(lines):
        if method_marker in line:
            j = i
            while j > 0 and lines[j - 1].strip().startswith("["):
                j -= 1
            return j
    raise SystemExit(f"marker not found: {method_marker}")


def extract_partial(name: str, header: str, start_marker: str, end_marker: str) -> None:
    lines = MAIN.read_text(encoding="utf-8").splitlines(keepends=True)
    start = find_test_start(lines, start_marker)
    end = find_test_start(lines, end_marker)
    body = "".join(lines[start:end])
    out = ROOT / f"tests/AutoPBR.Core.Tests/ObjectEntityBlockStateParityTests.{name}.cs"
    out.write_text(header + body + "}\n", encoding="utf-8")
    trimmed = lines[:start] + lines[end:]
    MAIN.write_text("".join(trimmed), encoding="utf-8")
    print(f"wrote {out.name}; main now {len(trimmed)} lines")
