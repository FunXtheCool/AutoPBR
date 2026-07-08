#!/usr/bin/env python3
"""Transform MainWindow.axaml slider rows to OverlaySlider controls."""

from __future__ import annotations

import re
import sys
from pathlib import Path


def parse_attrs(tag_body: str) -> dict[str, str]:
    attrs: dict[str, str] = {}
    for match in re.finditer(r'(\w+(?:\.\w+)?)="([^"]*)"', tag_body):
        attrs[match.group(1)] = match.group(2)
    return attrs


def format_overlay_slider(
    grid_attrs: dict[str, str],
    slider_attrs: dict[str, str],
    *,
    format_string: str | None = None,
    increment: str | None = None,
    value_binding: str | None = None,
) -> str:
    attrs = dict(grid_attrs)
    attrs.pop("Grid.Column", None)
    attrs["Grid.Column"] = "1"
    if "Grid.ColumnSpan" in attrs:
        del attrs["Grid.ColumnSpan"]

    parts = ['<ctrls:OverlaySlider']
    for key in ("Grid.Row", "Grid.Column", "HorizontalAlignment", "VerticalAlignment", "IsEnabled", "IsVisible"):
        if key in attrs:
            parts.append(f'{key}="{attrs[key]}"')

    for key in ("Minimum", "Maximum", "SmallChange", "LargeChange", "TickFrequency", "IsSnapToTickEnabled"):
        if key in slider_attrs:
            parts.append(f'{key}="{slider_attrs[key]}"')

    value = value_binding or slider_attrs.get("Value", "")
    if value:
        parts.append(f'Value="{{Binding {value}}}"' if not value.startswith("{") else f'Value="{value}"')

    if increment:
        parts.append(f'Increment="{increment}"')
    elif "SmallChange" in slider_attrs:
        parts.append(f'Increment="{slider_attrs["SmallChange"]}"')

    if format_string:
        parts.append(f'FormatString="{format_string}"')

    if "ToolTip.Tip" in slider_attrs:
        parts.append(f'ToolTip.Tip="{slider_attrs["ToolTip.Tip"]}"')
    if "IsEnabled" in slider_attrs and "IsEnabled" not in attrs:
        parts.append(f'IsEnabled="{slider_attrs["IsEnabled"]}"')

    parts.append('HorizontalAlignment="Left"')
    return " ".join(parts) + " />"


def string_format_to_format_string(sf: str | None) -> str:
    if not sf:
        return "0.##"
    m = re.search(r"\{0:([^}]+)\}", sf)
    if not m:
        return "0.##"
    fmt = m.group(1)
    if fmt in ("0", "0.##", "0.###", "0.0000"):
        return fmt
    if fmt.startswith("F"):
        return fmt
    return fmt


def extract_binding(text: str) -> tuple[str | None, str | None]:
    m = re.search(r'Text="\{Binding ([^,}]+)(?:,([^"]*))?\}"', text)
    if not m:
        m = re.search(r'Value="\{Binding ([^,}]+)(?:,([^"]*))?\}"', text)
    if not m:
        return None, None
    prop = m.group(1).strip()
    rest = m.group(2) or ""
    sf = None
    sf_match = re.search(r"StringFormat=\{\}\{0:([^}]+)\}", rest)
    if sf_match:
        sf = sf_match.group(1)
    return prop, sf


def transform_stackpanel_slider_numeric(content: str) -> str:
    pattern = re.compile(
        r'<StackPanel(?P<sp_attrs>[^>]*Grid\.Column="1"[^>]*Orientation="Horizontal"[^>]*)>\s*'
        r'<Slider(?P<slider>[^/]*)/>\s*'
        r'<NumericUpDown(?P<nud>[^/]*)/>\s*'
        r'</StackPanel>',
        re.DOTALL,
    )

    def repl(match: re.Match[str]) -> str:
        sp_attrs = parse_attrs(match.group("sp_attrs"))
        slider_attrs = parse_attrs(match.group("slider"))
        nud_attrs = parse_attrs(match.group("nud"))
        grid_attrs = {k: v for k, v in sp_attrs.items() if k.startswith("Grid.")}
        fmt = nud_attrs.get("FormatString", "F2")
        inc = nud_attrs.get("Increment")
        value = nud_attrs.get("Value", slider_attrs.get("Value", ""))
        if value.startswith("{Binding"):
            value_binding = value[len("{Binding ") : -1]
        else:
            value_binding = value
        line = format_overlay_slider(
            grid_attrs,
            slider_attrs,
            format_string=fmt,
            increment=inc,
            value_binding=value_binding,
        )
        return line

    return pattern.sub(repl, content)


def transform_slider_with_value_control(content: str) -> str:
    lines = content.splitlines(keepends=True)
    out: list[str] = []
    i = 0
    while i < len(lines):
        line = lines[i]
        if "<Slider " not in line and not line.strip().startswith("<Slider"):
            out.append(line)
            i += 1
            continue

        slider_lines = [line]
        i += 1
        while i < len(lines) and "/>" not in slider_lines[-1]:
            slider_lines.append(lines[i])
            i += 1
        slider_block = "".join(slider_lines)
        slider_body = re.sub(r"^.*<Slider", "<Slider", slider_block, count=1)
        slider_body = slider_body.replace("/>", "").replace("<Slider", "", 1)
        slider_attrs = parse_attrs(slider_body)

        grid_attrs: dict[str, str] = {}
        for key in ("Grid.Row", "Grid.Column", "IsEnabled", "IsVisible"):
            if key in slider_attrs:
                grid_attrs[key] = slider_attrs[key]

        if i < len(lines):
            next_block = lines[i]
            j = i
            value_lines = [next_block]
            while j + 1 < len(lines) and "/>" not in value_lines[-1] and "</" not in value_lines[-1]:
                j += 1
                value_lines.append(lines[j])
            value_block = "".join(value_lines)

            if re.match(r"\s*<(TextBox|TextBlock|NumericUpDown)\b", value_block):
                prop, sf = extract_binding(value_block)
                value_binding = prop or slider_attrs.get("Value", "").replace("{Binding ", "").replace(", Mode=TwoWay}", "").replace("}", "")
                if "Mode=TwoWay" in slider_attrs.get("Value", ""):
                    value_binding = f"{value_binding}, Mode=TwoWay"
                fmt = string_format_to_format_string(sf)
                if "NumericUpDown" in value_block:
                    nud_attrs = parse_attrs(value_block)
                    fmt = nud_attrs.get("FormatString", fmt)
                    inc = nud_attrs.get("Increment")
                else:
                    inc = slider_attrs.get("SmallChange")
                indent = re.match(r"(\s*)", line).group(1)
                overlay = indent + format_overlay_slider(
                    grid_attrs,
                    slider_attrs,
                    format_string=fmt,
                    increment=inc,
                    value_binding=value_binding,
                ) + "\n"
                out.append(overlay)
                i = j + 1
                continue

        out.extend(slider_lines)
    return "".join(out)


def transform_standalone_sliders(content: str) -> str:
    pattern = re.compile(
        r'(?P<indent>\s*)<Slider (?P<body>[^/]*Grid\.Column="1"[^/]*)/>\s*\n',
        re.MULTILINE,
    )

    def repl(match: re.Match[str]) -> str:
        indent = match.group("indent")
        slider_attrs = parse_attrs(match.group("body"))
        grid_attrs = {k: v for k, v in slider_attrs.items() if k.startswith("Grid.") or k in ("IsEnabled", "IsVisible")}
        inc = slider_attrs.get("SmallChange", "0.01")
        value = slider_attrs.get("Value", "")
        if value.startswith("{Binding "):
            value_binding = value[len("{Binding ") : -1]
        else:
            value_binding = value
        return indent + format_overlay_slider(
            grid_attrs,
            slider_attrs,
            format_string="0.##",
            increment=inc,
            value_binding=value_binding,
        ) + "\n"

    return pattern.sub(repl, content)


def update_grid_column_definitions(content: str) -> str:
    def repl_grid(match: re.Match[str]) -> str:
        tag = match.group(0)
        if "Auto,*,Auto" in tag:
            return tag.replace("Auto,*,Auto", "Auto,Auto")
        if 'Width="*"' in tag and "Auto" in tag and "ColumnDefinitions" in tag:
            # Auto,* patterns for preview rows -> Auto,Auto
            return re.sub(
                r'ColumnDefinitions="Auto,\*"',
                'ColumnDefinitions="Auto,Auto"',
                tag,
            )
        return tag

    content = re.sub(r'ColumnDefinitions="Auto,\*,Auto"', 'ColumnDefinitions="Auto,Auto"', content)
    # Preview label rows were Auto,* — keep that change.
    content = re.sub(r'ColumnDefinitions="Auto,\*"', 'ColumnDefinitions="Auto,Auto"', content)

    # ComboBoxes that spanned removed middle column
    content = re.sub(
        r'Grid\.Column="1" Grid\.ColumnSpan="2"',
        'Grid.Column="1"',
        content,
    )
    return content


def main() -> int:
    path = Path(sys.argv[1] if len(sys.argv) > 1 else "src/AutoPBR.App/Views/MainWindow.axaml")
    text = path.read_text(encoding="utf-8")
    original_slider_count = text.count("<Slider")

    text = transform_stackpanel_slider_numeric(text)
    text = transform_slider_with_value_control(text)
    text = transform_standalone_sliders(text)
    text = update_grid_column_definitions(text)

    remaining = text.count("<Slider")
    path.write_text(text, encoding="utf-8")
    print(f"Transformed {path}")
    print(f"Sliders before: {original_slider_count}, remaining: {remaining}")
    return 0 if remaining == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
