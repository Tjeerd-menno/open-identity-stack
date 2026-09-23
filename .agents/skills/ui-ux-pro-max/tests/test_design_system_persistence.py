"""Security regression tests for persisted design-system paths."""

import sys
import tempfile
import unittest
from pathlib import Path
from types import SimpleNamespace
from unittest.mock import patch


sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "scripts"))
from design_system import persist_design_system  # noqa: E402
import design_system  # noqa: E402


class DesignSystemPersistenceTests(unittest.TestCase):
    def setUp(self):
        self.temporary_directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.temporary_directory.cleanup)
        self.output_dir = Path(self.temporary_directory.name)

    def test_project_traversal_does_not_overwrite_existing_file(self):
        outside = self.output_dir / "outside" / "MASTER.md"
        outside.parent.mkdir()
        outside.write_text("keep this", encoding="utf-8")

        with self.assertRaises(ValueError):
            persist_design_system({"project_name": "../outside"}, output_dir=str(self.output_dir))

        self.assertEqual(outside.read_text(encoding="utf-8"), "keep this")
        self.assertFalse((self.output_dir / "design-system").exists())

    def test_page_traversal_does_not_overwrite_existing_file(self):
        outside = self.output_dir / "outside.md"
        outside.write_text("keep this", encoding="utf-8")

        with self.assertRaises(ValueError):
            persist_design_system(
                {"project_name": "Project"}, page="../../../outside", output_dir=str(self.output_dir)
            )

        self.assertEqual(outside.read_text(encoding="utf-8"), "keep this")
        self.assertFalse((self.output_dir / "design-system").exists())

    def test_rejects_windows_separators_drive_and_dot_segments(self):
        for label in (r"..\outside", str(self.output_dir / "drive-target"), ".", ".."):
            with self.subTest(label=label), self.assertRaises(ValueError):
                persist_design_system({"project_name": label}, output_dir=str(self.output_dir))
            with self.subTest(page=label), self.assertRaises(ValueError):
                persist_design_system(
                    {"project_name": "Project"}, page=label, output_dir=str(self.output_dir)
                )

    def test_rejects_absolute_project_and_page_paths(self):
        absolute_project = str(self.output_dir / "absolute-project")
        absolute_page = str(self.output_dir / "absolute-page")

        with self.assertRaises(ValueError):
            persist_design_system({"project_name": absolute_project}, output_dir=str(self.output_dir))
        with self.assertRaises(ValueError):
            persist_design_system(
                {"project_name": "Project"}, page=absolute_page, output_dir=str(self.output_dir)
            )

    def test_valid_labels_create_master_and_page_beneath_design_system_root(self):
        result = persist_design_system(
            {"project_name": "My Project"}, page="Admin Dashboard", output_dir=str(self.output_dir)
        )

        expected_dir = self.output_dir / "design-system" / "my-project"
        self.assertEqual(Path(result["design_system_dir"]), expected_dir)
        self.assertEqual(
            {Path(path) for path in result["created_files"]},
            {expected_dir / "MASTER.md", expected_dir / "pages" / "admin-dashboard.md"},
        )
        for path in result["created_files"]:
            self.assertTrue(Path(path).is_file())

    def test_missing_nested_output_directory_is_created_before_unix_persistence(self):
        output_dir = self.output_dir / "new" / "nested" / "output"
        def open_base(path):
            path.mkdir(parents=True, exist_ok=True)
            return object()

        fake_os = SimpleNamespace(name="posix", close=lambda _fd: None)
        with patch.object(design_system, "os", fake_os), \
             patch.object(design_system, "_open_unix_base_directory", side_effect=open_base), \
             patch.object(design_system, "_persist_unix") as persist:
            persist_design_system({"project_name": "Project"}, output_dir=str(output_dir))

        self.assertTrue(output_dir.is_dir())
        persist.assert_called_once()

    def test_unix_base_directory_is_opened_before_content_generation(self):
        opened_base = object()
        events = []
        fake_os = SimpleNamespace(name="posix", close=lambda _fd: events.append("close"))

        def format_master(_design_system):
            events.append("format")
            return "master"

        def open_base(_path):
            events.append("open")
            return opened_base

        with patch.object(design_system, "os", fake_os), \
             patch.object(design_system, "_open_unix_base_directory", create=True, side_effect=open_base) as open_base_mock, \
             patch.object(design_system, "_persist_unix") as persist, \
             patch.object(design_system, "format_master_md", side_effect=format_master):
            persist_design_system({"project_name": "Project"}, output_dir=str(self.output_dir))

        self.assertEqual(events, ["open", "format", "close"])
        open_base_mock.assert_called_once_with(self.output_dir)
        persist.assert_called_once()
        self.assertIs(persist.call_args.args[0], opened_base)

    def test_existing_master_symlink_cannot_escape_root(self):
        outside = self.output_dir / "outside.md"
        outside.write_text("keep this", encoding="utf-8")
        project_dir = self.output_dir / "design-system" / "project"
        project_dir.mkdir(parents=True)
        try:
            (project_dir / "MASTER.md").symlink_to(outside)
        except (OSError, NotImplementedError):
            self.skipTest("File symlinks are unavailable")

        with self.assertRaises(ValueError):
            persist_design_system({"project_name": "Project"}, output_dir=str(self.output_dir))

        self.assertEqual(outside.read_text(encoding="utf-8"), "keep this")

    def test_existing_page_symlink_cannot_escape_pages_directory(self):
        outside = self.output_dir / "outside.md"
        outside.write_text("keep this", encoding="utf-8")
        pages_dir = self.output_dir / "design-system" / "project" / "pages"
        pages_dir.mkdir(parents=True)
        try:
            (pages_dir / "dashboard.md").symlink_to(outside)
        except (OSError, NotImplementedError):
            self.skipTest("File symlinks are unavailable")

        with self.assertRaises(ValueError):
            persist_design_system(
                {"project_name": "Project"}, page="Dashboard", output_dir=str(self.output_dir)
            )

        self.assertEqual(outside.read_text(encoding="utf-8"), "keep this")
        self.assertFalse((pages_dir.parent / "MASTER.md").exists())

    def test_master_symlink_swap_after_preflight_does_not_overwrite_outside_file(self):
        outside = self.output_dir / "outside.md"
        outside.write_text("keep this", encoding="utf-8")
        project_dir = self.output_dir / "design-system" / "project"
        project_dir.mkdir(parents=True)
        master = project_dir / "MASTER.md"
        master.write_text("old content", encoding="utf-8")
        probe = project_dir / "symlink-probe"
        try:
            probe.symlink_to(outside)
        except (OSError, NotImplementedError):
            self.skipTest("File symlinks are unavailable")
        probe.unlink()

        def swap_target(_design_system):
            master.unlink()
            master.symlink_to(outside)
            return "new content"

        with patch.object(design_system, "format_master_md", side_effect=swap_target):
            with self.assertRaises((ValueError, OSError)):
                persist_design_system({"project_name": "Project"}, output_dir=str(self.output_dir))

        self.assertEqual(outside.read_text(encoding="utf-8"), "keep this")


if __name__ == "__main__":
    unittest.main()
