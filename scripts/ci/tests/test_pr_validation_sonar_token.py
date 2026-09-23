"""Keep PR-controlled work separate from the Sonar credential."""

from pathlib import Path
import unittest


REPO = Path(__file__).resolve().parents[3]
PR_WORKFLOW = REPO / ".github/workflows/pr-validation.yml"
TRUSTED_WORKFLOW = REPO / ".github/workflows/ci.yml"


class PrValidationSonarTokenTests(unittest.TestCase):
    def test_pr_workflow_never_receives_sonar_token(self):
        workflow = PR_WORKFLOW.read_text(encoding="utf-8")
        self.assertIn("  pull_request:", workflow)
        self.assertIn("  build-test:", workflow)
        self.assertNotIn("SONAR_TOKEN", workflow)
        self.assertNotIn("sonar.token", workflow)
        self.assertNotIn("dotnet-sonarscanner", workflow)

    def test_trusted_push_workflow_keeps_sonar_analysis(self):
        workflow = TRUSTED_WORKFLOW.read_text(encoding="utf-8")
        self.assertIn("  push:", workflow)
        self.assertNotIn("  pull_request:", workflow)
        self.assertIn("      - name: Begin SonarQube analysis", workflow)
        self.assertIn("      - name: End SonarQube analysis", workflow)
        self.assertEqual(2, workflow.count("SONAR_TOKEN: ${{ secrets.SONAR_TOKEN }}"))


if __name__ == "__main__":
    unittest.main()
