#!/usr/bin/env python3
"""Generate a Unity UI Bridge v1 spec from a reference image.

This is the first local-first image-to-spec pipeline. It combines deterministic
computer vision for layout regions with optional PaddleOCR for text regions.
"""

from __future__ import annotations

import argparse
import json
import math
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Sequence

import validate_specs


@dataclass(frozen=True)
class Region:
    x: int
    y: int
    width: int
    height: int
    role: str
    confidence: float

    @property
    def area(self) -> int:
        return self.width * self.height


@dataclass(frozen=True)
class OcrRegion:
    x: int
    y: int
    width: int
    height: int
    text: str
    confidence: float


def generate_spec(
    image_path: Path | str,
    title: str | None = None,
    run_ocr: bool = True,
    max_regions: int = 32,
) -> dict:
    path = Path(image_path)
    image, width, height = _load_image(path)
    regions = _detect_layout_regions(image, max_regions=max_regions)
    ocr_regions = _detect_text_regions(path) if run_ocr else []

    document_id = _safe_id(path.stem or "ui")
    nodes = [_canvas_node(width, height)]
    canvas_children = [_node_from_region(index, region) for index, region in enumerate(regions, start=1)]

    text_start = len(canvas_children) + 1
    canvas_children.extend(
        _node_from_ocr(index, ocr_region)
        for index, ocr_region in enumerate(ocr_regions, start=text_start)
        if ocr_region.text.strip()
    )
    nodes[0]["children"] = canvas_children

    return {
        "schemaVersion": "1.0.0",
        "document": {
            "id": f"doc.{document_id}",
            "title": title or path.stem or "Generated UI",
            "source": {
                "type": "screenshot",
                "uri": str(path).replace("\\", "/"),
            },
            "referenceResolution": {
                "width": width,
                "height": height,
            },
            "coordinateSystem": {
                "origin": "top-left",
                "unit": "pixel",
                "yAxis": "down",
            },
            "target": {
                "engine": "Unity",
                "uiSystem": "uGUI",
                "canvasMode": "screen-space-overlay",
            },
        },
        "assets": [],
        "styles": _default_styles(),
        "nodes": nodes,
        "interactions": _interactions_for_nodes(canvas_children),
        "extensions": {
            "org.unity-ui-bridge.pipeline": {
                "generator": "image_to_ui_spec.py",
                "layoutDetector": "opencv-contours",
                "ocr": "paddleocr" if run_ocr else "disabled",
            }
        },
    }


def write_spec(spec: dict, output_path: Path | str) -> None:
    path = Path(output_path)
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(spec, indent=2), encoding="utf-8")


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Generate a Unity UI Bridge v1 spec from an image.")
    parser.add_argument("image", type=Path, help="Input screenshot or concept image.")
    parser.add_argument("--output", "-o", type=Path, required=True, help="Output JSON spec path.")
    parser.add_argument("--title", default=None, help="Document title.")
    parser.add_argument("--no-ocr", action="store_true", help="Disable PaddleOCR text detection.")
    parser.add_argument("--max-regions", type=int, default=32, help="Maximum layout regions to emit.")
    parser.add_argument("--project-root", type=Path, default=validate_specs.default_project_root())
    args = parser.parse_args(argv)

    spec = generate_spec(
        image_path=args.image,
        title=args.title,
        run_ocr=not args.no_ocr,
        max_regions=args.max_regions,
    )
    write_spec(spec, args.output)

    errors = validate_specs.validate_file(validate_specs.default_schema_path(args.project_root), args.output)
    if errors:
        for error in errors:
            print(
                f"{args.output}: {error.instance_path}: {error.message} (schema: {error.schema_path})",
                file=sys.stderr,
            )
        return 1

    print(f"Wrote valid Unity UI Bridge spec: {args.output}")
    return 0


def _load_image(path: Path):
    try:
        from PIL import Image
        import numpy as np
    except ImportError as exc:
        raise SystemExit(
            "Missing image dependencies. Install them with "
            "`python -m pip install -r Assets/UnityUIBridge/Tools/Python/requirements-ai.txt`."
        ) from exc

    with Image.open(path) as loaded:
        rgb = loaded.convert("RGB")
        width, height = rgb.size
        return np.array(rgb), width, height


def _detect_layout_regions(image, max_regions: int) -> list[Region]:
    try:
        import cv2
    except ImportError as exc:
        raise SystemExit(
            "Missing OpenCV. Install it with "
            "`python -m pip install -r Assets/UnityUIBridge/Tools/Python/requirements-ai.txt`."
        ) from exc

    height, width = image.shape[:2]
    min_area = max(600, int(width * height * 0.002))
    max_area = int(width * height * 0.92)

    gray = cv2.cvtColor(image, cv2.COLOR_RGB2GRAY)
    blurred = cv2.GaussianBlur(gray, (5, 5), 0)
    edges = cv2.Canny(blurred, 40, 120)
    kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (5, 5))
    closed = cv2.morphologyEx(edges, cv2.MORPH_CLOSE, kernel, iterations=2)
    contours, _ = cv2.findContours(closed, cv2.RETR_TREE, cv2.CHAIN_APPROX_SIMPLE)

    candidates: list[Region] = []
    for contour in contours:
        x, y, region_width, region_height = cv2.boundingRect(contour)
        area = region_width * region_height
        if area < min_area or area > max_area:
            continue
        if region_width < 16 or region_height < 16:
            continue
        if _touches_image_border(x, y, region_width, region_height, width, height):
            continue

        role = _classify_region(region_width, region_height, area, width, height)
        confidence = min(0.95, 0.55 + math.sqrt(area / (width * height)))
        candidates.append(Region(x, y, region_width, region_height, role, round(confidence, 3)))

    return _dedupe_regions(candidates)[:max_regions]


def _detect_text_regions(image_path: Path) -> list[OcrRegion]:
    try:
        from paddleocr import PaddleOCR
    except ImportError:
        print("PaddleOCR is not installed; continuing without OCR text nodes.", file=sys.stderr)
        return []

    try:
        ocr = _create_paddle_ocr()
        raw_result = _run_paddle_ocr(ocr, image_path)
        return _parse_paddle_result(raw_result)
    except Exception as exc:  # pragma: no cover - depends on model/runtime state.
        print(f"PaddleOCR failed; continuing without OCR text nodes: {exc}", file=sys.stderr)
        return []


def _create_paddle_ocr():
    from paddleocr import PaddleOCR

    try:
        return PaddleOCR(lang="en", use_textline_orientation=True)
    except TypeError:
        return PaddleOCR(lang="en", use_angle_cls=True)


def _run_paddle_ocr(ocr, image_path: Path):
    if hasattr(ocr, "predict"):
        return ocr.predict(str(image_path))
    return ocr.ocr(str(image_path), cls=True)


def _parse_paddle_result(raw_result) -> list[OcrRegion]:
    regions: list[OcrRegion] = []
    for item in _flatten_ocr_items(raw_result):
        parsed = _parse_ocr_item(item)
        if parsed is not None:
            regions.append(parsed)
    return regions


def _flatten_ocr_items(raw_result):
    if isinstance(raw_result, list):
        for item in raw_result:
            if isinstance(item, dict):
                rec_texts = item.get("rec_texts") or []
                rec_scores = item.get("rec_scores") or []
                rec_boxes = item.get("rec_boxes") or item.get("dt_polys") or []
                for text, score, box in zip(rec_texts, rec_scores, rec_boxes):
                    yield [box, [text, score]]
            elif isinstance(item, list):
                for nested in item:
                    yield nested


def _parse_ocr_item(item) -> OcrRegion | None:
    if not isinstance(item, list) or len(item) < 2:
        return None
    box = item[0]
    text_payload = item[1]
    if not isinstance(text_payload, (list, tuple)) or len(text_payload) < 2:
        return None

    text = str(text_payload[0])
    confidence = float(text_payload[1])
    points = _box_to_points(box)
    if not points:
        return None

    xs = [point[0] for point in points]
    ys = [point[1] for point in points]
    x = int(min(xs))
    y = int(min(ys))
    width = max(1, int(max(xs) - x))
    height = max(1, int(max(ys) - y))
    return OcrRegion(x, y, width, height, text, round(confidence, 3))


def _box_to_points(box) -> list[tuple[float, float]]:
    if hasattr(box, "tolist"):
        box = box.tolist()
    if not isinstance(box, list):
        return []
    if len(box) == 4 and all(isinstance(value, (int, float)) for value in box):
        x1, y1, x2, y2 = box
        return [(x1, y1), (x2, y1), (x2, y2), (x1, y2)]
    points = []
    for point in box:
        if hasattr(point, "tolist"):
            point = point.tolist()
        if isinstance(point, (list, tuple)) and len(point) >= 2:
            points.append((float(point[0]), float(point[1])))
    return points


def _classify_region(region_width: int, region_height: int, area: int, image_width: int, image_height: int) -> str:
    aspect = region_width / max(region_height, 1)
    image_area = image_width * image_height
    if area > image_area * 0.15:
        return "panel"
    if 2.0 <= aspect <= 8.0 and region_height <= image_height * 0.22:
        return "button"
    if 0.7 <= aspect <= 1.3 and area < image_area * 0.05:
        return "icon"
    return "image"


def _dedupe_regions(regions: list[Region]) -> list[Region]:
    sorted_regions = sorted(regions, key=lambda region: region.area, reverse=True)
    kept: list[Region] = []
    for region in sorted_regions:
        if any(_intersection_over_union(region, existing) > 0.72 for existing in kept):
            continue
        kept.append(region)
    return sorted(kept, key=lambda region: (region.y, region.x, -region.area))


def _touches_image_border(x: int, y: int, width: int, height: int, image_width: int, image_height: int) -> bool:
    return x <= 1 and y <= 1 and x + width >= image_width - 2 and y + height >= image_height - 2


def _intersection_over_union(left: Region, right: Region) -> float:
    x1 = max(left.x, right.x)
    y1 = max(left.y, right.y)
    x2 = min(left.x + left.width, right.x + right.width)
    y2 = min(left.y + left.height, right.y + right.height)
    intersection = max(0, x2 - x1) * max(0, y2 - y1)
    if intersection == 0:
        return 0.0
    union = left.area + right.area - intersection
    return intersection / union


def _canvas_node(width: int, height: int) -> dict:
    return {
        "id": "node.canvas",
        "role": "canvas",
        "name": "Generated Canvas",
        "rect": {
            "x": 0,
            "y": 0,
            "width": width,
            "height": height,
        },
        "anchors": {
            "min": {"x": 0, "y": 0},
            "max": {"x": 1, "y": 1},
        },
        "layout": {
            "mode": "overlay",
        },
        "children": [],
    }


def _node_from_region(index: int, region: Region) -> dict:
    node_id = f"node.detected-{index:03d}"
    node = {
        "id": node_id,
        "role": region.role,
        "name": f"{region.role.title()} {index}",
        "rect": _rect(region.x, region.y, region.width, region.height),
        "styleRef": _style_ref_for_role(region.role),
        "confidence": region.confidence,
        "provenance": {
            "adapter": "opencv-contour-detector",
            "sourceRect": _rect(region.x, region.y, region.width, region.height),
        },
    }
    if region.role == "button":
        node["interactionRef"] = f"interaction.detected-{index:03d}"
    return node


def _node_from_ocr(index: int, region: OcrRegion) -> dict:
    return {
        "id": f"node.text-{index:03d}",
        "role": "text",
        "name": f"Text {index}",
        "rect": _rect(region.x, region.y, region.width, region.height),
        "styleRef": "style.generated.text",
        "text": {
            "content": region.text,
            "language": "en",
            "ocrConfidence": region.confidence,
        },
        "confidence": region.confidence,
        "provenance": {
            "adapter": "paddleocr",
            "sourceRect": _rect(region.x, region.y, region.width, region.height),
        },
    }


def _interactions_for_nodes(nodes: list[dict]) -> list[dict]:
    interactions = []
    for node in nodes:
        if node.get("role") != "button":
            continue
        interaction_id = node["interactionRef"]
        interactions.append(
            {
                "id": interaction_id,
                "nodeId": node["id"],
                "type": "button",
                "label": node.get("name", node["id"]),
                "states": ["normal", "hover", "pressed", "disabled"],
            }
        )
    return interactions


def _default_styles() -> list[dict]:
    return [
        {
            "id": "style.generated.panel",
            "name": "Generated Panel",
            "colors": {
                "fill": "#243241CC",
                "accent": "#5CC8FFFF",
            },
            "border": {
                "color": "#5CC8FFFF",
                "width": 2,
                "radius": 8,
            },
        },
        {
            "id": "style.generated.button",
            "name": "Generated Button",
            "colors": {
                "fill": "#2D6CDFFF",
                "text": "#FFFFFFFF",
            },
            "typography": {
                "fontFamily": "Default",
                "fontSize": 28,
                "fontStyle": "bold",
                "alignment": "center",
            },
        },
        {
            "id": "style.generated.text",
            "name": "Generated Text",
            "colors": {
                "text": "#FFFFFFFF",
            },
            "typography": {
                "fontFamily": "Default",
                "fontSize": 24,
                "fontStyle": "regular",
                "alignment": "center",
            },
        },
    ]


def _style_ref_for_role(role: str) -> str:
    if role == "button":
        return "style.generated.button"
    if role == "text":
        return "style.generated.text"
    return "style.generated.panel"


def _rect(x: int | float, y: int | float, width: int | float, height: int | float) -> dict:
    return {
        "x": float(x),
        "y": float(y),
        "width": float(width),
        "height": float(height),
    }


def _safe_id(value: str) -> str:
    safe = "".join(char if char.isalnum() else "-" for char in value.lower()).strip("-")
    return safe or "generated-ui"


if __name__ == "__main__":
    raise SystemExit(main())
