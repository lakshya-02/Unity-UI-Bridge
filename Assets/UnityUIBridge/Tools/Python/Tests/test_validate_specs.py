import tempfile
import unittest
from pathlib import Path
import sys

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import validate_specs


class ValidateSpecsTests(unittest.TestCase):
    def test_valid_sample_specs_pass(self):
        project_root = Path(__file__).resolve().parents[5]

        result = validate_specs.validate_all(project_root)

        self.assertEqual([], result.failures)
        self.assertGreaterEqual(result.valid_count, 2)
        self.assertGreaterEqual(result.expected_invalid_count, 1)

    def test_invalid_spec_reports_schema_path(self):
        project_root = Path(__file__).resolve().parents[5]
        schema_path = validate_specs.default_schema_path(project_root)
        invalid_spec = project_root / "Assets" / "UnityUIBridge" / "Samples" / "Specs" / "missing-node-rect.invalid.json"

        errors = validate_specs.validate_file(schema_path, invalid_spec)

        self.assertTrue(errors)
        self.assertIn("nodes/0", errors[0].instance_path)

    def test_unexpected_invalid_file_fails(self):
        project_root = Path(__file__).resolve().parents[5]
        schema_path = validate_specs.default_schema_path(project_root)
        with tempfile.TemporaryDirectory() as temp_dir:
            invalid_spec = Path(temp_dir) / "unexpected.json"
            invalid_spec.write_text('{"schemaVersion":"1.0.0"}', encoding="utf-8")

            result = validate_specs.validate_expected_valid(schema_path, [invalid_spec])

        self.assertEqual(1, len(result.failures))
        self.assertIn("unexpected.json", result.failures[0])


if __name__ == "__main__":
    unittest.main()
