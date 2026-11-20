import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest


class EvidenceTests(unittest.TestCase):
    def setUp(self):
        temporary = tempfile.TemporaryDirectory()
        self.addCleanup(temporary.cleanup)
        self.work = Path(temporary.name)
        self.plan = "oidcc-basic-certification-test-plan"
        self.modules = ["oidcc-server", "oidcc-scope-profile"]
        self.inputs = {
            "PlanName": self.plan,
            "PlanId": "plan123",
            "Variant": {"server_metadata": "discovery", "client_registration": "static_client"},
            "Results": [dict(Module=name, TestId=f"test{i}", Status="FINISHED", Result="PASSED",
                             BrowserNotes=["confidential-test-marker"]) for i, name in enumerate(self.modules)],
        }
        digest = "@sha256:" + "a" * 64
        suite = "registry.gitlab.com/openid/conformance-suite"
        product = "ghcr.io/tjeerd-menno/open-identity-stack-"
        self.images = dict(mode="candidate", revision="a" * 40, api=product + "api" + digest,
                           migrator=product + "db-migrator" + digest, suiteVersion="release-v5.3.4",
                           suiteCommit="b" * 40, suiteImage=suite + digest, nginxImage=suite + "/nginx" + digest,
                           environment="confidential-test-marker")

    def run_gate(self, tagged=True):
        for filename, data in (("input.json", self.inputs), ("images.json", self.images)):
            (self.work / filename).write_text(json.dumps(data), encoding="utf-8")
        return subprocess.run([
            sys.executable, str(Path(__file__).with_name("evidence.py")), "--plan", self.plan,
            "--only", ",".join(self.modules), "--input", "input.json", "--images", "images.json",
            "--out", "safe.json", *(["--tag", "v1.0.0-rc.1"] if tagged else []),
        ], cwd=self.work, capture_output=True, text=True, check=False)

    def test_publishes_only_reviewable_run_identifiers(self):
        result = self.run_gate()
        self.assertEqual(result.returncode, 0, result.stderr)
        safe = (self.work / "safe.json").read_text(encoding="utf-8")
        self.assertNotIn("confidential-test-marker", safe + result.stdout + result.stderr)
        for expected in ("plan123", "test0", self.images["migrator"], "v1.0.0-rc.1", "static_client"):
            self.assertIn(expected, safe)

    def test_exact_selection_requires_each_module_and_each_instance_once(self):
        original = self.inputs["Results"]
        for rows in ([], original[:1], original + original[:1],
                     [original[0], dict(original[1], Module="unexpected-module")],
                     [original[0], dict(original[1], TestId="test0")]):
            with self.subTest(rows=rows):
                self.inputs["Results"] = rows
                self.assertNotEqual(self.run_gate().returncode, 0)
        self.modules.clear()
        self.assertNotEqual(self.run_gate().returncode, 0)

    def test_finished_warnings_skips_and_manual_verdicts_are_recorded_and_gate(self):
        for verdict in ("WARNING", "SKIPPED", "REVIEW", "FAILED"):
            with self.subTest(verdict=verdict):
                self.inputs["Results"][0]["Result"] = verdict
                self.assertNotEqual(self.run_gate().returncode, 0)
                self.assertIn(verdict, (self.work / "safe.json").read_text(encoding="utf-8"))

    def test_incomplete_and_unrecognized_results_never_pass_or_disclose_raw_values(self):
        for status, verdict in (("INTERRUPTED", None), ("WAITING", "PASSED"), ("FINISHED", None),
                                ("STALLED(WAITING)", "PASSED"), ("FINISHED", "confidential-test-marker")):
            with self.subTest(status=status, verdict=verdict):
                self.inputs["Results"][0].update(Status=status, Result=verdict)
                result = self.run_gate()
                self.assertNotEqual(result.returncode, 0)
                self.assertNotIn("confidential-test-marker", result.stdout + result.stderr)
                public = self.work / "safe.json"
                if public.exists():
                    self.assertNotIn("confidential-test-marker", public.read_text(encoding="utf-8"))

    def test_wrong_plan_variant_and_unpinned_or_confused_images_are_rejected(self):
        for obj, key, value in ((self.inputs, "PlanName", "oidcc-config-certification-test-plan"),
                                (self.inputs, "Variant", {}), (self.images, "api", self.images["migrator"]),
                                (self.images, "suiteVersion", "latest"), (self.images, "revision", "HEAD")):
            with self.subTest(key=key):
                original = obj[key]
                obj[key] = value
                self.assertNotEqual(self.run_gate().returncode, 0)
                obj[key] = original

    def test_discovery_on_source_images_carries_only_source_provenance(self):
        self.plan = "oidcc-config-certification-test-plan"
        self.inputs.update(PlanName=self.plan, Variant={})
        self.images.update(mode="source", api="sha256:" + "a" * 64, migrator="sha256:" + "b" * 64)
        self.assertNotEqual(self.run_gate(tagged=True).returncode, 0)
        result = self.run_gate(tagged=False)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertNotIn("tag", json.loads((self.work / "safe.json").read_text(encoding="utf-8")))


if __name__ == "__main__":
    unittest.main()
