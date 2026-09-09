"""Exercise the Bash and PowerShell contract gates against temporary Git histories."""

import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import unittest


REPO = Path(__file__).resolve().parents[3]
CONTRACT = "identity-boundaries/credential-cutover.openapi.yaml"
OTHER = "identity-boundaries/other.openapi.yaml"
SPEC = "openapi: 3.0.3\ninfo:\n  title: Fixture\n  version: 1.0.0\npaths: {}\n"


class OpenApiComparisonTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.env = os.environ.copy()
        self.env.update(GIT_CONFIG_NOSYSTEM="1", GIT_CONFIG_GLOBAL=os.devnull)
        self.git("init", "--quiet")
        self.git("config", "user.name", "Contract gate test")
        self.git("config", "user.email", "contract-test@example.test")
        for key in (CONTRACT, OTHER):
            path = self.root / "contracts/openapi" / key
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(SPEC, encoding="utf-8")
        self.git("add", ".")
        self.git("commit", "--quiet", "-m", "Base contracts")
        self.git("tag", "base")
        # Unchanged live contracts exercise the real comparison branch. The stub
        # controls only its exit status; these tests do not test oasdiff itself.
        bin_dir = self.root / "bin"
        bin_dir.mkdir()
        docker = bin_dir / "docker"
        docker.write_text('#!/bin/sh\nexit "${OASDIFF_TEST_EXIT:-0}"\n', encoding="utf-8")
        docker.chmod(0o755)
        (bin_dir / "docker.cmd").write_text(
            "@echo off\nif defined OASDIFF_TEST_EXIT exit /b %OASDIFF_TEST_EXIT%\nexit /b 0\n",
            encoding="utf-8",
        )
        self.env["PATH"] = str(bin_dir) + os.pathsep + self.env["PATH"]

    def git(self, *args):
        return subprocess.run(
            ["git", *args], cwd=self.root, env=self.env, check=True,
            capture_output=True, text=True,
        )

    def run_gate(self, shell, allow=False):
        if shell == "bash":
            git_bash = Path("C:/Program Files/Git/bin/bash.exe")
            executable = str(git_bash) if os.name == "nt" and git_bash.exists() else shutil.which("bash")
            args = [str(REPO / "scripts/ci/compare-openapi-breaking-changes.sh"), "--base-ref", "base"]
            if allow:
                args += ["--allow-removed-spec", CONTRACT]
        else:
            executable = shutil.which("pwsh")
            args = ["-NoProfile", "-File", str(REPO / "scripts/ci/compare-openapi-breaking-changes.ps1"), "-BaseRef", "base"]
            if allow:
                args += ["-AllowRemovedSpec", CONTRACT]
        self.assertIsNotNone(executable, f"{shell} is required to test both CI gate implementations")
        return subprocess.run(
            [executable, *args], cwd=self.root, env=self.env,
            capture_output=True, text=True, timeout=30,
        )

    def test_removed_contract_requires_explicit_exception(self):
        (self.root / "contracts/openapi" / CONTRACT).unlink()
        for shell in ("bash", "pwsh"):
            with self.subTest(shell=shell):
                result = self.run_gate(shell)
                self.assertNotEqual(result.returncode, 0)
                self.assertIn("Spec removal is treated as a breaking", result.stdout)

    def test_explicitly_retired_contract_can_be_removed(self):
        (self.root / "contracts/openapi" / CONTRACT).unlink()
        for shell in ("bash", "pwsh"):
            with self.subTest(shell=shell):
                result = self.run_gate(shell, allow=True)
                self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
                self.assertIn("explicitly approved contract removal", result.stdout)

    def test_exception_does_not_allow_another_contract_removal(self):
        for key in (CONTRACT, OTHER):
            (self.root / "contracts/openapi" / key).unlink()
        for shell in ("bash", "pwsh"):
            with self.subTest(shell=shell):
                result = self.run_gate(shell, allow=True)
                self.assertNotEqual(result.returncode, 0)
                self.assertIn(f"OpenAPI spec 'contracts/openapi/{OTHER}'", result.stdout)

    def test_exception_does_not_skip_comparison_of_an_existing_contract(self):
        self.env["OASDIFF_TEST_EXIT"] = "42"
        for shell in ("bash", "pwsh"):
            with self.subTest(shell=shell):
                result = self.run_gate(shell, allow=True)
                self.assertNotEqual(result.returncode, 0)
                self.assertIn(f"Checking 'contracts/openapi/{CONTRACT}'", result.stdout)


if __name__ == "__main__":
    unittest.main()
