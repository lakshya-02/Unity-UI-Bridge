import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

import image_to_ui_spec
import validate_specs


class ImageToUiSpecTests(unittest.TestCase):
    def test_synthetic_image_generates_valid_v1_spec(self):
        project_root = Path(__file__).resolve().parents[5]

        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            image_path = temp_path / "synthetic-ui.png"
            output_path = temp_path / "synthetic-ui.json"
            _write_synthetic_ui_png(image_path)

            spec = image_to_ui_spec.generate_spec(
                image_path=image_path,
                title="Synthetic Test UI",
                run_ocr=False,
            )
            image_to_ui_spec.write_spec(spec, output_path)

            errors = validate_specs.validate_file(
                validate_specs.default_schema_path(project_root),
                output_path,
            )

        self.assertEqual([], errors)
        self.assertEqual("1.0.0", spec["schemaVersion"])
        self.assertEqual({"width": 640, "height": 360}, spec["document"]["referenceResolution"])
        self.assertEqual("canvas", spec["nodes"][0]["role"])
        self.assertGreaterEqual(len(spec["nodes"][0]["children"]), 2)

    def test_cli_writes_valid_spec_file(self):
        project_root = Path(__file__).resolve().parents[5]

        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            image_path = temp_path / "synthetic-ui.png"
            output_path = temp_path / "out.json"
            _write_synthetic_ui_png(image_path)

            exit_code = image_to_ui_spec.main(
                [
                    str(image_path),
                    "--output",
                    str(output_path),
                    "--title",
                    "CLI Synthetic",
                    "--no-ocr",
                    "--project-root",
                    str(project_root),
                ]
            )

            payload = json.loads(output_path.read_text(encoding="utf-8"))

        self.assertEqual(0, exit_code)
        self.assertEqual("CLI Synthetic", payload["document"]["title"])


def _write_synthetic_ui_png(path):
    from PIL import Image, ImageDraw

    image = Image.new("RGB", (640, 360), "#101820")
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle((140, 60, 500, 300), radius=12, fill="#203040", outline="#5cc8ff", width=3)
    draw.rounded_rectangle((210, 180, 430, 230), radius=8, fill="#2d6cdf", outline="#a8d7ff", width=2)
    draw.rectangle((225, 193, 245, 217), fill="#ffffff")
    draw.text((270, 195), "PLAY", fill="#ffffff")
    image.save(path)


if __name__ == "__main__":
    unittest.main()
