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
            asset_output_dir = temp_path / "sprites"
            _write_synthetic_ui_png(image_path)

            spec = image_to_ui_spec.generate_spec(
                image_path=image_path,
                title="Synthetic Test UI",
                run_ocr=False,
                asset_output_dir=asset_output_dir,
                asset_uri_prefix="Assets/UnityUIBridge/Generated/Sprites/synthetic-ui",
            )
            image_to_ui_spec.write_spec(spec, output_path)

            errors = validate_specs.validate_file(
                validate_specs.default_schema_path(project_root),
                output_path,
            )
            asset_files_exist = all((asset_output_dir / Path(asset["uri"]).name).exists() for asset in spec["assets"])

        self.assertEqual([], errors)
        self.assertEqual("1.0.0", spec["schemaVersion"])
        self.assertEqual({"width": 640, "height": 360}, spec["document"]["referenceResolution"])
        self.assertEqual("canvas", spec["nodes"][0]["role"])
        self.assertGreaterEqual(len(spec["nodes"][0]["children"]), 2)
        self.assertEqual(1, len(spec["assets"]))

        asset_ids = {asset["id"] for asset in spec["assets"]}
        background_nodes = [node for node in spec["nodes"][0]["children"] if node["id"] == "node.background"]
        overlay_nodes = [node for node in spec["nodes"][0]["children"] if node["id"] != "node.background"]

        self.assertTrue(asset_files_exist)
        self.assertEqual(["asset.background"], list(asset_ids))
        self.assertEqual("asset.background", background_nodes[0].get("assetRef"))
        self.assertTrue(all("assetRef" not in node for node in overlay_nodes))
        self.assertTrue(
            any(asset["type"] == "background" and asset["sourceNodeId"] == "node.background" for asset in spec["assets"])
        )
        self.assertTrue(any(node["role"] == "button" for node in overlay_nodes))

    def test_region_sprite_mode_emits_cropped_assets_for_detected_regions(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            temp_path = Path(temp_dir)
            image_path = temp_path / "synthetic-ui.png"
            asset_output_dir = temp_path / "sprites"
            _write_synthetic_ui_png(image_path)

            spec = image_to_ui_spec.generate_spec(
                image_path=image_path,
                run_ocr=False,
                asset_output_dir=asset_output_dir,
                asset_uri_prefix="Assets/UnityUIBridge/Generated/Sprites/synthetic-ui",
                emit_region_sprites=True,
            )

            asset_files_exist = all((asset_output_dir / Path(asset["uri"]).name).exists() for asset in spec["assets"])

        region_assets = [asset for asset in spec["assets"] if asset["id"].startswith("asset.detected-")]
        visual_nodes = [
            node for node in spec["nodes"][0]["children"]
            if node["id"].startswith("node.detected-") and node["role"] != "text"
        ]

        self.assertTrue(asset_files_exist)
        self.assertGreaterEqual(len(region_assets), 1)
        self.assertTrue(all("assetRef" in node for node in visual_nodes))

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
            generated_assets = payload["assets"]

        self.assertEqual(0, exit_code)
        self.assertEqual("CLI Synthetic", payload["document"]["title"])
        self.assertEqual(["asset.background"], [asset["id"] for asset in generated_assets])

    def test_hotspot_filter_rejects_large_panels_as_buttons(self):
        regions = [
            image_to_ui_spec.Region(0, 52, 1800, 983, "panel", 0.95),
            image_to_ui_spec.Region(67, 88, 1681, 439, "panel", 0.95),
            image_to_ui_spec.Region(49, 532, 193, 187, "icon", 0.67),
            image_to_ui_spec.Region(809, 799, 181, 151, "icon", 0.85),
            image_to_ui_spec.Region(864, 818, 72, 109, "image", 0.61),
            image_to_ui_spec.Region(700, 640, 360, 84, "button", 0.82),
            image_to_ui_spec.Region(1606, 1103, 74, 74, "icon", 0.68),
        ]
        ocr_regions = [
            image_to_ui_spec.OcrRegion(202, 230, 1051, 144, "CYBERPUNK GUI", 0.9, "test"),
            image_to_ui_spec.OcrRegion(760, 660, 240, 42, "PLAY", 0.9, "test"),
            image_to_ui_spec.OcrRegion(1608, 1098, 84, 78, "4p", 0.8, "test"),
        ]

        hotspots = image_to_ui_spec._filter_hotspot_regions(regions, ocr_regions, 1800, 1200)
        hotspot_boxes = {(region.x, region.y, region.width, region.height) for region in hotspots}

        self.assertNotIn((0, 52, 1800, 983), hotspot_boxes)
        self.assertNotIn((67, 88, 1681, 439), hotspot_boxes)
        self.assertNotIn((49, 532, 193, 187), hotspot_boxes)
        self.assertIn((809, 799, 181, 151), hotspot_boxes)
        self.assertNotIn((864, 818, 72, 109), hotspot_boxes)
        self.assertIn((700, 640, 360, 84), hotspot_boxes)
        self.assertIn((1606, 1103, 74, 74), hotspot_boxes)


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
