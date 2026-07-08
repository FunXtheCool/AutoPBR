import re
from pathlib import Path

path = Path(__file__).resolve().parents[1] / "src/AutoPBR.App/Views/MainWindow.axaml"
text = path.read_text(encoding="utf-8")


def find_matching_close(s: str, start: int) -> int:
    depth = 0
    i = start
    while i < len(s):
        if s.startswith("<Expander", i):
            depth += 1
            i += len("<Expander")
            continue
        if s.startswith("</Expander>", i):
            depth -= 1
            i += len("</Expander>")
            if depth == 0:
                return i
            continue
        i += 1
    raise ValueError("unclosed expander")


def convert_block(block: str) -> str:
    open_end = block.index(">")
    opening = block[: open_end + 1]
    inner = block[open_end + 1 : -len("</Expander>")]
    if "<Expander.Header>" in opening:
        raise ValueError("custom header: " + opening[:160])

    attrs: list[str] = []
    header_binding = re.search(r'Header="\{Binding[^"]+\}"', opening)
    header_literal = re.search(r'Header="([^"]+)"', opening)
    if header_binding:
        attrs.append(header_binding.group(0))
    elif header_literal:
        attrs.append(header_literal.group(0))

    grid_col = re.search(r'Grid\.Column="(\d+)"', opening)
    if grid_col:
        attrs.append(f'Grid.Column="{grid_col.group(1)}"')

    tooltip = re.search(r'ToolTip\.Tip="\{Binding[^"]+\}"', opening)
    if tooltip:
        attrs.append(tooltip.group(0))

    is_enabled = re.search(r'IsEnabled="\{Binding[^"]+\}"', opening)
    if is_enabled:
        attrs.append(is_enabled.group(0))

    if "Preview3D" in opening or "AiSpecular" in opening or "MlSpecular" in inner:
        attrs.append('FlyoutMaxWidth="720"')
    elif "Dictionary" in inner or "DeepBump" in inner or "MaterialTag" in inner:
        attrs.append('FlyoutMaxWidth="400"')

    inner = re.sub(r'\s*Margin="0,8,0,0"', "", inner, count=1)
    inner = re.sub(r'\s*Margin="0,6,0,0"', "", inner, count=1)

    attr_str = (" " + " ".join(attrs)) if attrs else ""
    return f"<ctrls:FlyoutSection{attr_str}>{inner}</ctrls:FlyoutSection>"


out: list[str] = []
i = 0
count = 0
while True:
    start = text.find("<Expander", i)
    if start == -1:
        out.append(text[i:])
        break
    out.append(text[i:start])
    end = find_matching_close(text, start)
    block = text[start:end]
    out.append(convert_block(block))
    count += 1
    i = end

path.write_text("".join(out), encoding="utf-8")
print(f"converted {count} expanders")
