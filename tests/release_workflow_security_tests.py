"""Regression checks for release input at the PowerShell parser boundary."""

import os
import subprocess
import tempfile
import unittest
from pathlib import Path


WORKFLOW = Path(__file__).resolve().parents[1] / ".github/workflows/release.yml"


def pwsh_steps():
    lines = WORKFLOW.read_text(encoding="utf-8").splitlines()
    steps = []
    for index, line in enumerate(lines):
        if line != "        shell: pwsh":
            continue
        name = lines[index - 1].strip().removeprefix("- name: ")
        if lines[index + 1] != "        run: |":
            raise AssertionError(f"Unexpected run block for {name}")
        script_lines = []
        for script_line in lines[index + 2 :]:
            if script_line and not script_line.startswith("          "):
                break
            script_lines.append(script_line[10:])
        steps.append((name, "\n".join(script_lines) + "\n"))
    return steps


class ReleaseWorkflowSecurityTests(unittest.TestCase):
    def test_pwsh_steps_do_not_interpolate_workflow_expressions(self):
        steps = pwsh_steps()
        self.assertEqual(3, len(steps))
        for name, script in steps:
            with self.subTest(step=name):
                self.assertNotIn("${{", script)

    def test_validation_accepts_supported_versions(self):
        for version in ("v1.2.3", "v1.2.3-rc.1"):
            for name, script in pwsh_steps():
                if name != "Resolve and validate release version":
                    continue
                with self.subTest(version=version):
                    result, env_file, _ = self.run_validation(script, version)
                    self.assertEqual(0, result.returncode, result.stderr)
                    self.assertIn(f"RELEASE_TAG={version}", env_file)
                    self.assertIn(f"PACKAGE_VERSION={version[1:]}", env_file)

    def test_validation_rejects_quotes_and_subexpressions_without_execution(self):
        for name, script in pwsh_steps():
            if name != "Resolve and validate release version":
                continue
            for payload in (
                'v1.2.3"; Set-Content -LiteralPath "{marker}" -Value injected; #',
                'v1.2.3$(Set-Content -LiteralPath "{marker}" -Value injected)',
            ):
                with self.subTest(payload=payload):
                    result, env_file, marker_exists = self.run_validation(
                        script, payload
                    )
                    self.assertNotEqual(0, result.returncode)
                    self.assertFalse(marker_exists, result.stderr)
                    self.assertEqual("", env_file)

    def run_validation(self, script, version):
        with tempfile.TemporaryDirectory() as directory:
            marker = Path(directory) / "injected.txt"
            env_file = Path(directory) / "github-env.txt"
            script_file = Path(directory) / "validate.ps1"
            version = version.format(marker=marker.as_posix())
            # Simulate GitHub's expression expansion before the runner invokes pwsh.
            script_file.write_text(
                script.replace("${{ env.RELEASE_TAG }}", version), encoding="utf-8"
            )
            env = os.environ.copy()
            env.update(
                RELEASE_TAG=version,
                GITHUB_ENV=str(env_file),
                GITHUB_EVENT_NAME="push",
            )
            result = subprocess.run(
                ["pwsh", "-NoProfile", "-NonInteractive", "-File", str(script_file)],
                env=env,
                text=True,
                capture_output=True,
                check=False,
            )
            return (
                result,
                env_file.read_text(encoding="utf-8") if env_file.exists() else "",
                marker.exists(),
            )


if __name__ == "__main__":
    unittest.main()
