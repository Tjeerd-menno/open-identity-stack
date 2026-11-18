"""Compare the runtime Admin API to each published 1.x OpenAPI snapshot."""

import argparse
import json
from pathlib import Path
import subprocess
import sys


BASELINES = Path("contracts/openapi/baselines")
SEVERITIES = Path(__file__).with_name("admin-api-severity-levels.txt")


def check(base_ref, candidate):
    # Compare to the PR target, including deletions and moves. A PR cannot
    # change a published baseline along with the API it proposes to change.
    changed = subprocess.check_output(
        ["git", "diff", "--name-only", "--no-renames", "--diff-filter=DMT",
         base_ref, "--", BASELINES], text=True,
    )
    if changed:
        sys.exit(f"Published release snapshots are immutable:\n{changed}")

    original = BASELINES / "1.0.0/admin-api.json"
    if not original.is_file():
        sys.exit(f"Missing required Admin API baseline: {original}")
    existing = subprocess.check_output(
        ["git", "ls-tree", "-r", "--name-only", base_ref, "--", BASELINES], text=True,
    ).splitlines()
    for baseline in sorted(BASELINES.glob("1.*/admin-api.json")):
        print(f"Checking {candidate} against {baseline}", flush=True)
        if baseline.as_posix() not in existing:
            if json.loads(baseline.read_bytes()) != json.loads(candidate.read_bytes()):
                sys.exit(f"New release snapshot {baseline} must match the generated Admin API.")
        result = subprocess.run(
            ["oasdiff", "breaking", "--fail-on", "WARN", "--allow-external-refs=false",
             "--severity-levels", SEVERITIES, baseline, candidate],
            check=False,
        )
        if result.returncode:
            return result.returncode
    return 0


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--base-ref", required=True, help="Git commit of the target branch")
    parser.add_argument("candidate", type=Path, help="Generated Admin API OpenAPI JSON")
    args = parser.parse_args()
    sys.exit(check(args.base_ref, args.candidate))
