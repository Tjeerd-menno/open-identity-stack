#!/usr/bin/env bash

set -euo pipefail

# Defaults mirror the values the CI workflows pass explicitly. Keep the two in step: a default
# that advertises a higher bar than CI enforces makes the gate look stricter than it is.
results_dir="TestResults"
minimum_line_rate="0.10"
minimum_overall_line_rate="0.72"

while [[ $# -gt 0 ]]; do
  case "$1" in
    --results-dir)
      results_dir="${2:-}"
      shift 2
      ;;
    --minimum-line-rate)
      minimum_line_rate="${2:-}"
      shift 2
      ;;
    --minimum-overall-line-rate)
      minimum_overall_line_rate="${2:-}"
      shift 2
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 1
      ;;
  esac
done

python3 - "$results_dir" "$minimum_line_rate" "$minimum_overall_line_rate" <<'PY'
import sys
from pathlib import Path
import xml.etree.ElementTree as ET

results_dir = Path(sys.argv[1]).resolve()
minimum_line_rate = float(sys.argv[2])
minimum_overall_line_rate = float(sys.argv[3])

coverage_files = sorted(results_dir.rglob("*.cobertura.xml"))
if not coverage_files:
    raise SystemExit(f"No coverage reports found in '{results_dir}'.")

failed = []
unique_line_covered_by_key: dict[str, bool] = {}

for file in coverage_files:
    root = ET.parse(file).getroot()
    line_rate = float(root.attrib.get("line-rate", "0"))

    for class_node in root.findall("./packages/package/classes/class"):
        source_file_identifier = class_node.attrib.get("filename") or class_node.attrib.get("name") or ""
        for line_node in class_node.findall("./lines/line"):
            key = f"{source_file_identifier}|{line_node.attrib['number']}"
            is_covered = int(line_node.attrib.get("hits", "0")) > 0
            unique_line_covered_by_key[key] = unique_line_covered_by_key.get(key, False) or is_covered

    percent = round(line_rate * 100, 2)
    print(f"{file.name}: {percent}%")

    if line_rate < minimum_line_rate:
        failed.append(f"{file.name} ({percent}%)")

total_lines_valid = len(unique_line_covered_by_key)
total_lines_covered = sum(1 for is_covered in unique_line_covered_by_key.values() if is_covered)
overall = (total_lines_covered / total_lines_valid) if total_lines_valid > 0 else 0
overall_percent = round(overall * 100, 2)
print(f"Overall line coverage: {overall_percent}%")

if overall < minimum_overall_line_rate:
    raise SystemExit(
        f"Overall line coverage {overall_percent}% is below {round(minimum_overall_line_rate * 100, 2)}%."
    )

if failed:
    raise SystemExit(
        f"Coverage below threshold ({round(minimum_line_rate * 100, 2)}%): {', '.join(failed)}"
    )
PY
