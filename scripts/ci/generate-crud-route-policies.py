#!/usr/bin/env python3
"""Generate the single-permission CRUD route policy map from RequireAuthorization."""

import argparse
import re
import sys
from pathlib import Path

REPO = Path(__file__).resolve().parents[2]
PERMISSIONS = REPO / "src/OpenIdentityStack.Application/Authorization/Permissions.cs"
OUTPUT = REPO / "src/OpenIdentityStack.ManagementWeb/src/lib/generated-crud-route-policies.ts"
CLASS_PATTERN = re.compile(r"\bpublic static class (\w+)")
CONST_PATTERN = re.compile(r'\bpublic const string (\w+) = "([^"]*)";')
MAP_CALL = re.compile(r"\bMap(?:Get|Post|Put|Delete|Patch|Group|Methods)\(")
MAP_GET = re.compile(r'\bMapGet\(\s*(string\.Empty|"([^"]*)")')
AUTH = re.compile(r"RequireAuthorization\(\s*(Permissions(?:\.\w+)+)")

# Screens whose route has one permission. Combined-permission screens stay explicit.
SCREENS = [
    ("users", "src/OpenIdentityStack.Api/Users/UsersApi.cs", "api/admin/users", ""),
    ("userDetail", "src/OpenIdentityStack.Api/Users/UsersApi.cs", "api/admin/users", "{id:guid}"),
    ("groups", "src/OpenIdentityStack.Api/Groups/GroupsApi.cs", "api/admin/groups", ""),
    ("groupDetail", "src/OpenIdentityStack.Api/Groups/GroupsApi.cs", "api/admin/groups", "{id:guid}"),
    ("roles", "src/OpenIdentityStack.Api/Admin/RolesApi.cs", "api/admin/roles", ""),
    ("roleDetail", "src/OpenIdentityStack.Api/Admin/RolesApi.cs", "api/admin/roles", "{id:guid}"),
    ("applicationPermissions", "src/OpenIdentityStack.Api/Admin/ApplicationPermissionsApi.cs", "api/admin/application-permissions", "applications"),
    ("applicationPermissionDetail", "src/OpenIdentityStack.Api/Admin/ApplicationPermissionsApi.cs", "api/admin/application-permissions", "applications/{id:guid}"),
    ("applications", "src/OpenIdentityStack.Api/Applications/ApplicationsApi.cs", "api/admin/applications", ""),
    ("applicationDetail", "src/OpenIdentityStack.Api/Applications/ApplicationsApi.cs", "api/admin/applications", "{id:guid}"),
    ("sessions", "src/OpenIdentityStack.Api/Sessions/SessionsApi.cs", "api/admin/sessions", ""),
    ("sessionDetail", "src/OpenIdentityStack.Api/Sessions/SessionsApi.cs", "api/admin/sessions", "{id:guid}"),
    ("providers", "src/OpenIdentityStack.Api/Federation/ProvidersApi.cs", "api/admin/providers", ""),
    ("providerDetail", "src/OpenIdentityStack.Api/Federation/ProvidersApi.cs", "api/admin/providers", "{id:guid}"),
    ("authenticationSettings", "src/OpenIdentityStack.Api/Settings/AuthenticationSettingsApi.cs", "api/admin/authentication-settings", ""),
    ("auditEntries", "src/OpenIdentityStack.Api/Audit/AuditEntriesApi.cs", "api/admin/audit-entries", ""),
]


def export_name(owner: str | None, member: str) -> str:
    if owner is None or owner == "Permissions":
        return member[:1].lower() + member[1:]
    return owner[:1].lower() + owner[1:] + member


def dotted_name(owner: str | None, member: str) -> str:
    if owner is None or owner == "Permissions":
        return f"Permissions.{member}"
    return f"Permissions.{owner}.{member}"


def parse_constants(source: str) -> dict[str, str]:
    exports: dict[str, str] = {}
    class_stack: list[str] = []
    depth = 0
    pending_class: str | None = None
    for line in source.splitlines():
        class_match = CLASS_PATTERN.search(line)
        const_match = CONST_PATTERN.search(line)
        if const_match is not None and pending_class is None:
            owner = class_stack[-1] if class_stack else None
            exports[dotted_name(owner, const_match.group(1))] = export_name(owner, const_match.group(1))
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
    if not exports:
        raise SystemExit(f"No permission constants found in {PERMISSIONS}.")
    return exports


def endpoint_policy(api_file: str, group: str, template: str, exports: dict[str, str]) -> str:
    source = (REPO / api_file).read_text(encoding="utf-8")
    if f'MapGroup("{group}")' not in source:
        raise SystemExit(f"{api_file} does not map {group}.")
    found: list[str] = []
    for match in MAP_GET.finditer(source):
        argument = "" if match.group(1) == "string.Empty" else match.group(2)
        if argument != template:
            continue
        next_call = MAP_CALL.search(source, match.end())
        chain_end = next_call.start() if next_call is not None else len(source)
        found = AUTH.findall(source, match.start(), chain_end)
        break
    if len(found) != 1:
        raise SystemExit(f"{api_file} GET {group}/{template} is not a single-permission endpoint.")
    export = exports.get(found[0])
    if export is None:
        raise SystemExit(f"{found[0]} is not a Permissions.cs constant.")
    return export


def render(entries: list[tuple[str, str]]) -> str:
    imports = sorted({export for _, export in entries})
    lines = [
        "// Generated from endpoint RequireAuthorization for single-permission CRUD screens. Do not edit.",
        "// Screens that combine several permissions stay explicit.",
        "import {",
        *[f"  {name}," for name in imports],
        '} from "./generated-permissions";',
        "",
        "export const crudRoutePolicies = {",
        *[f"  {key}: {export}," for key, export in entries],
        "} as const;",
        "",
    ]
    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="Fail when the generated map is stale.")
    args = parser.parse_args()
    exports = parse_constants(PERMISSIONS.read_text(encoding="utf-8"))
    entries = [
        (key, endpoint_policy(api_file, group, template, exports))
        for key, api_file, group, template in SCREENS
    ]
    entries.sort(key=lambda item: item[0])
    expected = render(entries)
    if args.check:
        actual = OUTPUT.read_text(encoding="utf-8") if OUTPUT.exists() else ""
        if actual != expected:
            print(
                f"{OUTPUT.relative_to(REPO)} is stale. Run scripts/ci/generate-crud-route-policies.py.",
                file=sys.stderr,
            )
            return 1
        return 0
    OUTPUT.write_text(expected, encoding="utf-8")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
