"""Gate selected OIDF tests and export identifiers and verdicts safe to retain in CI."""

import argparse
import json
from pathlib import Path
import re
import sys


VARIANTS = {
    "oidcc-basic-certification-test-plan": {"server_metadata": "discovery", "client_registration": "static_client"},
    "oidcc-config-certification-test-plan": {},
}
VERDICTS = {"PASSED", "WARNING", "SKIPPED", "REVIEW", "FAILED"}
DIGEST = r"sha256:[0-9a-f]{64}"
IDENTIFIER = r"[A-Za-z0-9_-]{1,128}"


def checked(value, pattern, label):
    if not isinstance(value, str) or not re.fullmatch(pattern, value):
        raise ValueError(f"Missing or invalid {label}")
    return value


def provenance(data, tag):
    mode = checked(data.get("mode"), "source|candidate", "image mode")
    metadata = {
        "mode": mode,
        "revision": checked(data.get("revision"), "[0-9a-f]{40}", "product revision"),
        "suiteVersion": checked(data.get("suiteVersion"), r"release-v[0-9]+\.[0-9]+\.[0-9]+", "suite release"),
        "suiteCommit": checked(data.get("suiteCommit"), "[0-9a-f]{40}", "suite revision"),
    }
    if mode == "candidate":
        metadata["tag"] = checked(tag, r"v[0-9]+\.[0-9]+\.[0-9]+(?:-[0-9A-Za-z]+(?:\.[0-9A-Za-z]+)*)?",
                                  "candidate tag")
    elif tag is not None:
        raise ValueError("A source image cannot represent a published tag")

    suite = "registry.gitlab.com/openid/conformance-suite"
    product = "ghcr.io/tjeerd-menno/open-identity-stack-"
    for key, repository in (("suiteImage", suite), ("nginxImage", suite + "/nginx"),
                            ("api", product + "api"), ("migrator", product + "db-migrator")):
        local = mode == "source" and key in {"api", "migrator"}
        pattern = DIGEST if local else re.escape(repository) + "@" + DIGEST
        metadata[key] = checked(data.get(key), pattern, key)
    return metadata


def build_report(args, run, images):
    if run.get("PlanName") != args.plan or run.get("Variant") != VARIANTS[args.plan]:
        raise ValueError("The runner plan or variant differs from the CI selection")
    expected = args.only.split(",")
    for name in expected:
        checked(name, r"[a-z0-9]+(?:-[a-z0-9]+)+", "selected module")
    if len(expected) != len(set(expected)):
        raise ValueError("Duplicate module in CI selection")
    rows = run.get("Results")
    if not isinstance(rows, list) or len(rows) != len(expected):
        raise ValueError("Runner result count differs from the CI selection")

    modules = {}
    seen_ids = set()
    for row in rows:
        if not isinstance(row, dict):
            raise ValueError("Invalid module row")
        name = row.get("Module")
        if not isinstance(name, str) or name not in expected or name in modules:
            raise ValueError("Unexpected or duplicate module in runner output")
        instance = checked(row.get("TestId"), IDENTIFIER, f"{name} test identifier")
        if instance in seen_ids:
            raise ValueError("Duplicate test instance identifier")
        seen_ids.add(instance)
        status = row.get("Status")
        verdict = row.get("Result")
        # Runner diagnostics can contain callback URLs or HTTP errors. Never copy
        # arbitrary fields or values into the public record, even on failure.
        modules[name] = {
            "module": name,
            "testId": instance,
            "status": status if status in ("FINISHED", "INTERRUPTED") else "INCOMPLETE",
            "verdict": verdict if isinstance(verdict, str) and verdict in VERDICTS else "UNKNOWN",
        }
    return {
        **provenance(images, args.tag),
        "plan": args.plan,
        "planId": checked(run.get("PlanId"), IDENTIFIER, "plan identifier"),
        "variant": VARIANTS[args.plan],
        "modules": [modules[name] for name in expected],
        "passed": all(row["status"] == "FINISHED" and row["verdict"] == "PASSED" for row in modules.values()),
    }


def read_object(path):
    try:
        value = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        raise ValueError("Evidence input is unavailable or malformed") from None
    if not isinstance(value, dict):
        raise ValueError("Evidence input must be a JSON object")
    return value


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--plan", required=True, choices=VARIANTS)
    parser.add_argument("--only", required=True, help="Exact comma-separated module list supplied to the runner")
    parser.add_argument("--input", required=True, type=Path)
    parser.add_argument("--images", required=True, type=Path)
    parser.add_argument("--tag")
    parser.add_argument("--out", required=True, type=Path)
    args = parser.parse_args()
    try:
        report = build_report(args, read_object(args.input), read_object(args.images))
    except ValueError as error:
        report = {"plan": args.plan, "passed": False, "error": str(error)}
        print(error, file=sys.stderr)
    args.out.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    for row in report.get("modules", []):
        print(row["module"], row["status"], row["verdict"])
    return 0 if report["passed"] else 1


if __name__ == "__main__":
    sys.exit(main())
