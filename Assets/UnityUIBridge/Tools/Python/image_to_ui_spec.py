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
from dataclasses import dataclass, replace
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
    adapter: str


def generate_spec(
    image_path: Path | str,
    title: str | None = None,
    run_ocr: bool = True,
    max_regions: int = 32,
    asset_output_dir: Path | str | None = None,
    asset_uri_prefix: str | None = None,
    include_background: bool = True,
    emit_region_sprites: bool = False,
) -> dict:
    path = Path(image_path)
    image, width, height = _load_image(path)
    detection_limit = max(max_regions * 3, 96)
    regions = _detect_layout_regions(image, max_regions=detection_limit)
    ocr_regions = _detect_text_regions(path) if run_ocr else []
    reconstruction_regions = (
        regions[:max_regions]
        if emit_region_sprites
        else _filter_hotspot_regions(regions, ocr_regions, width, height)
    )[:max_regions]
    assets, asset_refs = _extract_assets(
        path,
        image,
        width,
        height,
        reconstruction_regions if emit_region_sprites else [],
        asset_output_dir=asset_output_dir,
        asset_uri_prefix=asset_uri_prefix,
        include_background=include_background,
    )

    document_id = _safe_id(path.stem or "ui")
    nodes = [_canvas_node(width, height)]
    canvas_children = []
    if include_background and "node.background" in asset_refs:
        canvas_children.append(_background_node(width, height, asset_refs["node.background"]))

    canvas_children.extend(
        _node_from_region(index, region, asset_refs.get(f"node.detected-{index:03d}"))
        for index, region in enumerate(reconstruction_regions, start=1)
    )

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
        "assets": assets,
        "styles": _default_styles(),
        "nodes": nodes,
        "interactions": _interactions_for_nodes(canvas_children),
        "extensions": {
            "org.unity-ui-bridge.pipeline": {
                "generator": "image_to_ui_spec.py",
                "layoutDetector": "opencv-contours",
                "ocr": "paddleocr-with-easyocr-fallback" if run_ocr else "disabled",
                "assetExtractor": "pil-region-cropper" if asset_output_dir is not None else "disabled",
                "compositingMode": "region-sprites" if emit_region_sprites else "background-with-hotspots",
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
    parser.add_argument("--asset-output-dir", type=Path, default=None, help="Directory for cropped sprite assets.")
    parser.add_argument("--asset-uri-prefix", default=None, help="URI prefix written into asset references.")
    parser.add_argument("--no-background", action="store_true", help="Do not emit a full-image background sprite.")
    parser.add_argument(
        "--emit-region-sprites",
        action="store_true",
        help="Emit visible cropped sprites for every detected region. Default uses one background plus hotspots.",
    )
    parser.add_argument("--project-root", type=Path, default=validate_specs.default_project_root())
    args = parser.parse_args(argv)

    asset_output_dir = args.asset_output_dir or _default_asset_output_dir(args.output, args.image)
    asset_uri_prefix = args.asset_uri_prefix or _default_asset_uri_prefix(args.project_root, asset_output_dir)

    spec = generate_spec(
        image_path=args.image,
        title=args.title,
        run_ocr=not args.no_ocr,
        max_regions=args.max_regions,
        asset_output_dir=asset_output_dir,
        asset_uri_prefix=asset_uri_prefix,
        include_background=not args.no_background,
        emit_region_sprites=args.emit_region_sprites,
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


def _extract_assets(
    image_path: Path,
    image,
    image_width: int,
    image_height: int,
    regions: list[Region],
    asset_output_dir: Path | str | None,
    asset_uri_prefix: str | None,
    include_background: bool,
) -> tuple[list[dict], dict[str, str]]:
    if asset_output_dir is None:
        return [], {}

    try:
        from PIL import Image
    except ImportError as exc:
        raise SystemExit(
            "Missing Pillow. Install it with "
            "`python -m pip install -r Assets/UnityUIBridge/Tools/Python/requirements-ai.txt`."
        ) from exc

    output_dir = Path(asset_output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)
    source_image = Image.fromarray(image)
    assets: list[dict] = []
    asset_refs: dict[str, str] = {}

    if include_background:
        asset_id = "asset.background"
        file_name = "source-background.png"
        asset_path = output_dir / file_name
        source_image.save(asset_path)
        assets.append(
            {
                "id": asset_id,
                "type": "background",
                "uri": _asset_uri(asset_path, asset_uri_prefix),
                "rect": _rect(0, 0, image_width, image_height),
                "sourceNodeId": "node.background",
            }
        )
        asset_refs["node.background"] = asset_id

    for index, region in enumerate(regions, start=1):
        node_id = f"node.detected-{index:03d}"
        asset_id = f"asset.detected-{index:03d}"
        crop_rect = _expanded_crop_rect(region, image_width, image_height)
        file_name = f"detected-{index:03d}-{region.role}.png"
        asset_path = output_dir / file_name
        source_image.crop(crop_rect).save(asset_path)

        assets.append(
            {
                "id": asset_id,
                "type": _asset_type_for_role(region.role),
                "uri": _asset_uri(asset_path, asset_uri_prefix),
                "rect": _rect(region.x, region.y, region.width, region.height),
                "sourceNodeId": node_id,
            }
        )
        asset_refs[node_id] = asset_id

    return assets, asset_refs


def _default_asset_output_dir(output_path: Path, image_path: Path) -> Path:
    stem = _safe_id(image_path.stem or output_path.stem or "generated-ui")
    output_parent = output_path.parent
    if output_parent.name.lower() == "specs":
        return output_parent.parent / "Sprites" / stem
    return output_parent / "Sprites" / stem


def _default_asset_uri_prefix(project_root: Path, asset_output_dir: Path) -> str:
    output_dir = asset_output_dir.resolve()
    root = project_root.resolve()
    try:
        return output_dir.relative_to(root).as_posix()
    except ValueError:
        return output_dir.as_posix()


def _asset_uri(asset_path: Path, asset_uri_prefix: str | None) -> str:
    if not asset_uri_prefix:
        return asset_path.as_posix()
    return f"{asset_uri_prefix.rstrip('/')}/{asset_path.name}"


def _expanded_crop_rect(region: Region, image_width: int, image_height: int) -> tuple[int, int, int, int]:
    padding = 2
    left = max(0, region.x - padding)
    top = max(0, region.y - padding)
    right = min(image_width, region.x + region.width + padding)
    bottom = min(image_height, region.y + region.height + padding)
    return left, top, right, bottom


def _asset_type_for_role(role: str) -> str:
    if role in {"button", "panel", "input", "toggle", "slider"}:
        return "panel"
    if role == "icon":
        return "icon"
    return "sprite"


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

    # Pre-compute gray once and adaptive Canny baseline from median
    gray = cv2.cvtColor(image, cv2.COLOR_RGB2GRAY)
    median = cv2.medianBlur(gray, 5).mean()
    lower = int(max(0, 0.33 * median))
    upper = int(min(255, 1.33 * median))

    # Try multiple scales and both RETR_EXTERNAL / RETR_TREE for better coverage
    all_contours = []
    scales = [(5, 5), (7, 7), (3, 3)]
    retrieval_modes = [cv2.RETR_EXTERNAL, cv2.RETR_TREE]

    for ksize in scales:
        blurred = cv2.GaussianBlur(gray, ksize, 0)
        edges = cv2.Canny(blurred, lower, upper)

        # Opening (erode then dilate) to remove salt-and-pepper noise
        kernel_small = cv2.getStructuringElement(cv2.MORPH_RECT, (3, 3))
        opened = cv2.morphologyEx(edges, cv2.MORPH_OPEN, kernel_small, iterations=1)

        # Close to bridge gaps in edge map
        kernel = cv2.getStructuringElement(cv2.MORPH_RECT, (5, 5))
        closed = cv2.morphologyEx(opened, cv2.MORPH_CLOSE, kernel, iterations=2)
        if cv2.countNonZero(closed) == 0:
            closed = cv2.morphologyEx(edges, cv2.MORPH_CLOSE, kernel, iterations=2)

        for mode in retrieval_modes:
            contours, _ = cv2.findContours(closed, mode, cv2.CHAIN_APPROX_SIMPLE)
            all_contours.extend(contours)

    candidates: list[Region] = []
    for contour in all_contours:
        x, y, region_width, region_height = cv2.boundingRect(contour)
        area = region_width * region_height
        aspect = region_width / max(region_height, 1)

        if area < min_area or area > max_area:
            continue
        if region_width < 16 or region_height < 16:
            continue
        if aspect < 0.15 or aspect > 18:
            continue
        if _touches_image_border(x, y, region_width, region_height, width, height):
            continue
        if _is_solid_color_region(gray, x, y, region_width, region_height, area, width * height):
            continue

        role = _classify_region(region_width, region_height, area, width, height)
        confidence = _calculate_region_confidence(region_width, region_height, area, width, height, gray, x, y)
        candidates.append(Region(x, y, region_width, region_height, role, round(confidence, 3)))

    return _filter_contained_regions(_dedupe_regions(candidates))[:max_regions]


def _filter_hotspot_regions(
    regions: list[Region],
    ocr_regions: list[OcrRegion],
    image_width: int,
    image_height: int,
) -> list[Region]:
    hotspots: list[Region] = []
    for region in regions:
        button_candidate = (
            (region.role == "button" and _is_plausible_button_region(region, image_width, image_height))
            or _is_icon_button_candidate(region, image_width, image_height)
        )
        if not button_candidate and _is_plausible_button_region(region, image_width, image_height):
            button_candidate = _has_action_text(region, ocr_regions)
        if not button_candidate:
            continue
        if not _is_bottom_navigation_region(region, image_height) and _is_text_dominated(region, ocr_regions):
            continue
        if _looks_like_decorative_side_art(region, image_width, image_height) and not _has_action_text(region, ocr_regions):
            continue

        hotspots.append(replace(region, role="button"))

    return _filter_nested_hotspots(_dedupe_regions(hotspots))


def _is_plausible_button_region(region: Region, image_width: int, image_height: int) -> bool:
    image_area = image_width * image_height
    aspect = region.width / max(region.height, 1)
    area_ratio = region.area / max(image_area, 1)

    if area_ratio < 0.001 or area_ratio > 0.10:
        return False
    if region.width > image_width * 0.72 or region.height > image_height * 0.28:
        return False
    if region.height < image_height * 0.04:
        return False
    return 0.45 <= aspect <= 10.0


def _is_icon_button_candidate(region: Region, image_width: int, image_height: int) -> bool:
    image_area = image_width * image_height
    aspect = region.width / max(region.height, 1)
    if not (0.65 <= aspect <= 1.55 and image_area * 0.002 <= region.area <= image_area * 0.08):
        return False

    center_x = region.x + region.width * 0.5
    center_y = region.y + region.height * 0.5
    central_action_area = (
        center_y >= image_height * 0.60
        and abs(center_x - image_width * 0.5) <= image_width * 0.16
        and region.area >= image_area * 0.006
    )
    bottom_navigation_area = center_y >= image_height * 0.86
    return central_action_area or bottom_navigation_area


def _is_bottom_navigation_region(region: Region, image_height: int) -> bool:
    center_y = region.y + region.height * 0.5
    return center_y >= image_height * 0.86


def _looks_like_decorative_side_art(region: Region, image_width: int, image_height: int) -> bool:
    center_x = region.x + region.width * 0.5
    center_y = region.y + region.height * 0.5
    near_side = center_x < image_width * 0.16 or center_x > image_width * 0.84
    middle_band = image_height * 0.38 <= center_y <= image_height * 0.68
    return near_side and middle_band


def _is_text_dominated(region: Region, ocr_regions: list[OcrRegion]) -> bool:
    if not ocr_regions:
        return False

    text_area = 0
    region_box = _region_box(region)
    for text_region in ocr_regions:
        text_area += _intersection_area(region_box, _ocr_box(text_region))

    return text_area / max(region.area, 1) > 0.6


def _filter_nested_hotspots(regions: list[Region]) -> list[Region]:
    result: list[Region] = []
    for index, region in enumerate(regions):
        region_box = _region_box(region)
        nested = False
        for other_index, other in enumerate(regions):
            if index == other_index or other.area <= region.area:
                continue
            if _intersection_area(region_box, _region_box(other)) / max(region.area, 1) > 0.82:
                nested = True
                break
        if not nested:
            result.append(region)
    return result


_ACTION_KEYWORDS = {
    "login", "sign in", "sign up", "submit", "cancel", "ok", "next", "back",
    "save", "delete", "edit", "add", "remove", "close", "send", "share",
    "download", "upload", "play", "pause", "stop", "start", "confirm",
    "register", "buy", "checkout", "continue", "search", "go", "apply",
    "accept", "decline", "agree", "skip", "try", "get", "install", "update",
    "open", "view", "more", "menu", "settings", "help", "home", "profile",
}


def _has_action_text(region: Region, ocr_regions: list[OcrRegion]) -> bool:
    """True if the region overlaps OCR text that looks like an action/button label."""
    if not ocr_regions:
        return False
    region_box = _region_box(region)
    for text_region in ocr_regions:
        text_box = _ocr_box(text_region)
        if _intersection_area(region_box, text_box) == 0:
            continue
        text_lower = text_region.text.strip().lower()
        if text_lower in _ACTION_KEYWORDS:
            return True
        # Short text (1-2 words) overlapping a clickable-looking region is likely a button
        if len(text_lower.split()) <= 2 and len(text_lower) <= 15:
            return True
    return False


def _detect_text_regions(image_path: Path) -> list[OcrRegion]:
    paddle_error: Exception | None = None
    try:
        from paddleocr import PaddleOCR
    except ImportError:
        paddle_error = None
    else:
        try:
            ocr = _create_paddle_ocr()
            raw_result = _run_paddle_ocr(ocr, image_path)
            return _parse_paddle_result(raw_result)
        except Exception as exc:  # pragma: no cover - depends on model/runtime state.
            paddle_error = exc

    try:
        return _detect_text_regions_easyocr(image_path)
    except Exception as exc:  # pragma: no cover - depends on model/runtime state.
        if paddle_error is not None:
            print(f"PaddleOCR failed: {paddle_error}", file=sys.stderr)
        print(f"EasyOCR failed; continuing without OCR text nodes: {exc}", file=sys.stderr)
        return []


def _detect_text_regions_easyocr(image_path: Path) -> list[OcrRegion]:
    try:
        import easyocr
    except ImportError as exc:
        raise RuntimeError(
            "No OCR backend is available. Install AI dependencies with "
            "`python -m pip install -r Assets/UnityUIBridge/Tools/Python/requirements-ai.txt`."
        ) from exc

    reader = easyocr.Reader(["en"], gpu=False, verbose=False)
    results = reader.readtext(str(image_path))
    regions: list[OcrRegion] = []
    for box, text, confidence in results:
        points = _box_to_points(box)
        if not points:
            continue
        xs = [point[0] for point in points]
        ys = [point[1] for point in points]
        x = int(min(xs))
        y = int(min(ys))
        width = max(1, int(max(xs) - x))
        height = max(1, int(max(ys) - y))
        regions.append(OcrRegion(x, y, width, height, str(text), round(float(confidence), 3), "easyocr"))
    return regions


def _create_paddle_ocr():
    from paddleocr import PaddleOCR

    try:
        return PaddleOCR(
            lang="en",
            use_doc_orientation_classify=False,
            use_doc_unwarping=False,
            use_textline_orientation=False,
        )
    except TypeError:
        return PaddleOCR(lang="en", use_angle_cls=False)


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
    return OcrRegion(x, y, width, height, text, round(confidence, 3), "paddleocr")


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


def _calculate_region_confidence(region_width, region_height, area, image_width, image_height,
                                 gray_image=None, x=0, y=0):
    """Calculate confidence score for a detected region."""
    image_area = image_width * image_height

    # Base confidence from area ratio
    area_ratio = area / image_area
    base_confidence = min(0.95, 0.55 + math.sqrt(area_ratio))

    # Penalty for very small regions
    if area < 800:
        base_confidence -= 0.1

    # Penalty for regions with extreme aspect ratios
    aspect = region_width / max(region_height, 1)
    if aspect < 0.2 or aspect > 15:
        base_confidence -= 0.15

    # Bonus for regions that look like buttons (horizontal rectangles)
    if 1.5 <= aspect <= 8.0:
        base_confidence += 0.05

    # Edge density bonus: textured regions are more likely real UI
    if gray_image is not None and area > 0:
        edge_density = _region_edge_density(gray_image, x, y, region_width, region_height)
        if edge_density > 0.08:
            base_confidence += 0.08
        elif edge_density < 0.02:
            base_confidence -= 0.05

    # Positional bonus: center of image is more likely to contain UI
    cx = x + region_width / 2
    cy = y + region_height / 2
    dist_from_center = math.sqrt(
        ((cx - image_width / 2) / (image_width / 2)) ** 2
        + ((cy - image_height / 2) / (image_height / 2)) ** 2
    )
    if dist_from_center < 0.35:
        base_confidence += 0.05

    # Ensure confidence is within valid range
    return max(0.1, min(0.95, base_confidence))


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


def _is_solid_color_region(gray_image, x, y, region_width, region_height, area, image_area) -> bool:
    """True if a tiny region is nearly uniform in color - likely a false positive."""
    try:
        import cv2
    except ImportError:
        return False
    if area > image_area * 0.01:
        return False
    crop = gray_image[y : y + region_height, x : x + region_width]
    if crop.size == 0:
        return True
    std = float(cv2.meanStdDev(crop)[1][0][0])
    return std < 3.0


def _region_edge_density(gray_image, x, y, region_width, region_height) -> float:
    """Proportion of edge pixels inside the region - higher means more textured."""
    try:
        import cv2
    except ImportError:
        return 0.0
    crop = gray_image[y : y + region_height, x : x + region_width]
    if crop.size == 0:
        return 0.0
    edges = cv2.Canny(crop, 50, 150)
    return float(cv2.countNonZero(edges)) / max(crop.size, 1)


def _filter_contained_regions(regions: list[Region]) -> list[Region]:
    """Remove regions that are almost entirely contained inside a larger region."""
    if len(regions) < 2:
        return regions
    result: list[Region] = []
    for i, inner in enumerate(regions):
        contained = False
        for j, outer in enumerate(regions):
            if i == j:
                continue
            if outer.area <= inner.area:
                continue
            iou = _intersection_over_union(inner, outer)
            if iou > 0.85:
                contained = True
                break
        if not contained:
            result.append(inner)
    return result


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


def _intersection_area(left: tuple[int, int, int, int], right: tuple[int, int, int, int]) -> int:
    x1 = max(left[0], right[0])
    y1 = max(left[1], right[1])
    x2 = min(left[2], right[2])
    y2 = min(left[3], right[3])
    return max(0, x2 - x1) * max(0, y2 - y1)


def _region_box(region: Region) -> tuple[int, int, int, int]:
    return region.x, region.y, region.x + region.width, region.y + region.height


def _ocr_box(region: OcrRegion) -> tuple[int, int, int, int]:
    return region.x, region.y, region.x + region.width, region.y + region.height


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


def _background_node(width: int, height: int, asset_ref: str) -> dict:
    return {
        "id": "node.background",
        "role": "image",
        "name": "Source Background",
        "rect": _rect(0, 0, width, height),
        "assetRef": asset_ref,
        "confidence": 1.0,
        "provenance": {
            "adapter": "source-image-copier",
            "sourceRect": _rect(0, 0, width, height),
        },
    }


def _node_from_region(index: int, region: Region, asset_ref: str | None = None) -> dict:
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
    if asset_ref is not None:
        node["assetRef"] = asset_ref
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
            "adapter": region.adapter,
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
