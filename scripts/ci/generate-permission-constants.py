#!/usr/bin/env python3
"""Generate Management Web permission constants from Permissions.cs."""

import argparse
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
SOURCE = REPO / "src/OpenIdentityStack.Application/Authorization/Permissions.cs"
OUTPUT = REPO / "src/OpenIdentityStack.ManagementWeb/src/lib/generated-permissions.ts"
CLASS_PATTERN = re.compile(r"\bpublic static class (\w+)")
CONST_PATTERN = re.compile(r'\bpublic const string (\w+) = "([^"]*)";')


def export_name(owner: str | None, member: str) -> str:
    if owner is None or owner == "Permissions":
        return member[:1].lower() + member[1:]
    return owner[:1].lower() + owner[1:] + member


def parse_constants(source: str) -> list[tuple[str, str]]:
    constants: list[tuple[str, str]] = []
    class_stack: list[str] = []
    depth = 0
    pending_class: str | None = None
    for line in source.splitlines():
        class_match = CLASS_PATTERN.search(line)
        const_match = CONST_PATTERN.search(line)
        if const_match is not None and pending_class is None:
            owner = class_stack[-1] if class_stack else None
            constants.append((export_name(owner, const_match.group(1)), const_match.group(2)))
        if class_match is not None:
            pending_class = class_match.group(1)
        opens = line.count("{")
        closes = line.count("}")
        if pending_class is not None and opens:
            class_stack.append(pending_class)
            pending_class = None
        depth += opens - closes
        while len(class_stack) > depth:
            class_stack.pop()
    names = [name for name, _ in constants]
    if len(names) != len(set(names)):
        raise SystemExit("Permission constant export names are not unique.")
    if not constants:
        raise SystemExit(f"No permission constants found in {SOURCE}.")
    return constants


def render(constants: list[tuple[str, str]]) -> str:
    lines = [
        "// Generated from src/OpenIdentityStack.Application/Authorization/Permissions.cs. Do not edit.",
    ]
    lines.extend(f'export const {name} = "{value}" as const;' for name, value in constants)
    return "\n".join(lines) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="Fail when the generated module is stale.")
    args = parser.parse_args()
    expected = render(parse_constants(SOURCE.read_text(encoding="utf-8")))
    if args.check:
        actual = OUTPUT.read_text(encoding="utf-8") if OUTPUT.exists() else ""
        if actual != expected:
            print(f"{OUTPUT.relative_to(REPO)} is stale. Run scripts/ci/generate-permission-constants.py.", file=sys.stderr)
            return 1
        return 0
    OUTPUT.write_text(expected, encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
