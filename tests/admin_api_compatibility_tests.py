"""Exercise the release gate using the same oasdiff executable as PR validation."""

import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


GATE = Path(__file__).resolve().parents[1] / "scripts/ci/check-admin-api-compatibility.py"
BASELINE = Path("contracts/openapi/baselines/1.0.0/admin-api.json")
ROUTE = "/api/admin/widgets"


class AdminApiCompatibilityTests(unittest.TestCase):
    def setUp(self):
        temp = tempfile.TemporaryDirectory()
        self.addCleanup(temp.cleanup)
        self.root = Path(temp.name)
        self.schema = {"type": "object", "properties": {"name": {"type": "string"}}}
        self.operation = {
            "requestBody": {"content": {"application/json": {"schema": self.schema}}},
            "responses": {"200": {"description": "OK"}},
        }
        self.spec = {
            "openapi": "3.0.3",
            "info": {"title": "Admin API", "version": "1.0.0"},
            "paths": {ROUTE: {"post": self.operation}},
        }
        (self.root / BASELINE.parent).mkdir(parents=True)
        self.git("init", "--quiet")
        self.commit_baseline()

    def git(self, *args):
        subprocess.run(["git", *args], cwd=self.root, check=True, capture_output=True)

    def commit_baseline(self):
        (self.root / BASELINE).write_text(json.dumps(self.spec), encoding="utf-8")
        self.git("add", ".")
        self.git("-c", "user.name=Test", "-c", "user.email=test@example.test",
                 "commit", "--quiet", "-m", "Capture snapshot")

    def check(self, succeeds, diagnostic=None):
        (self.root / "candidate.json").write_text(json.dumps(self.spec), encoding="utf-8")
        result = subprocess.run(
            [sys.executable, str(GATE), "--base-ref", "HEAD", "candidate.json"],
            cwd=self.root, capture_output=True, text=True,
        )
        output = result.stdout + result.stderr
        self.assertEqual(succeeds, result.returncode == 0, output)
        if diagnostic:
            self.assertIn(diagnostic, output)

    def test_removed_operation_fails_even_with_new_document_version(self):
        del self.spec["paths"][ROUTE]
        self.spec["info"]["version"] = "1.1.0"
        self.check(False, ROUTE)

    def test_required_request_field_fails(self):
        self.schema["properties"]["owner"] = {"type": "string"}
        self.schema["required"] = ["owner"]
        self.check(False, "owner")

    def test_required_request_field_with_schema_default_still_fails(self):
        self.schema["properties"]["owner"] = {"type": "string", "default": "default owner"}
        self.schema["required"] = ["owner"]
        self.check(False, "owner")

    def test_optional_request_field_passes(self):
        self.schema["properties"]["owner"] = {"type": "string"}
        self.check(True)

    def test_operation_can_be_deprecated_but_stays_available(self):
        self.operation["deprecated"] = True
        self.check(True)
        self.commit_baseline()
        del self.spec["paths"][ROUTE]
        self.check(False, ROUTE)

    def test_editing_baseline_with_candidate_fails(self):
        self.schema["required"] = ["name"]
        (self.root / BASELINE).write_text(json.dumps(self.spec), encoding="utf-8")
        self.check(False, "immutable")

    def test_removing_release_snapshot_fails(self):
        (self.root / BASELINE).unlink()
        self.check(False, "immutable")

    def test_new_release_snapshot_must_match_export(self):
        new_baseline = self.root / "contracts/openapi/baselines/1.1.0/admin-api.json"
        new_baseline.parent.mkdir()
        new_baseline.write_text(json.dumps(self.spec), encoding="utf-8")
        self.check(True)
        self.schema["properties"]["owner"] = {"type": "string"}
        self.check(False, "must match")

    def test_later_release_additions_are_protected(self):
        new_baseline = self.root / "contracts/openapi/baselines/1.1.0/admin-api.json"
        new_baseline.parent.mkdir()
        self.spec["paths"]["/api/admin/later"] = {"post": self.operation}
        new_baseline.write_text(json.dumps(self.spec), encoding="utf-8")
        self.git("add", str(new_baseline))
        self.git("-c", "user.name=Test", "-c", "user.email=test@example.test",
                 "commit", "--quiet", "-m", "Capture next release")
        del self.spec["paths"]["/api/admin/later"]
        self.check(False, "/api/admin/later")


if __name__ == "__main__":
    unittest.main()
