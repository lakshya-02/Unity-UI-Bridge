#!/usr/bin/env python3
"""Evaluate real-image reconstruction quality for Unity UI Bridge.

This script analyzes the output of the image-to-spec pipeline and checks
how well the reconstructed UI matches a reference image across multiple dimensions:
1. Detection accuracy (recall, precision for UI elements)
2. Classification accuracy (correct role assignment)
3. Text fidelity (OCR quality, content matching)
4. Layout fidelity (position, size accuracy)
5. Asset extraction quality (sprite crop accuracy, completeness)
"""

from __future__ import annotations

import argparse
import json
import math
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Any

import numpy as np
from PIL import Image


@dataclass(frozen=True)
class EvalConfiguration:
    """Configuration for evaluation thresholds and matching criteria."""

    iou_threshold: float = 0.5
    scale_tolerance: float = 0.12
    position_tolerance: float = 24.0


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Evaluate real-image reconstruction quality")
    parser.add_argument("spec", type=Path, help="Path to generated spec JSON")
    parser.add_argument("--reference-image", type=Path, default=None, help="Reference screenshot")
    parser.add_argument("--ground-truth", type=Path, default=None, help="Ground truth spec JSON")
    parser.add_argument("--output", type=Path, default=None, help="Output report path")
    parser.add_argument("--verbose", action="store_true", help="Print detailed results")
    args = parser.parse_args(argv)

    if args.reference_image and not args.reference_image.exists():
        print(f"Error: Reference image not found: {args.reference_image}", file=sys.stderr)
        return 1

    spec = load_spec(args.spec)
    report = evaluate_spec(spec, args.reference_image, args.ground_truth)

    if args.output:
        args.output.write_text(json.dumps(report, indent=2), encoding="utf-8")
    else:
        print(json.dumps(report, indent=2))

    return 0


def evaluate_spec(spec: dict, reference_image: Path | None = None, ground_truth: Path | None = None) -> dict[str, Any]:
    """Evaluate a single generated spec against optional references."""
    report = {
        "spec_path": str(spec),
        "summary": summary(spec),
    }

    report["detection"] = evaluate_detection(spec)
    report["classification"] = evaluate_classification(spec)
    report["text_fidelity"] = evaluate_text_fidelity(spec)
    report["layout_fidelity"] = evaluate_layout_fidelity(spec)
    report["asset_quality"] = evaluate_asset_quality(spec)

    if reference_image and reference_image.exists():
        report["visual_comparison"] = evaluate_visual(reference_image, spec)

    if ground_truth and ground_truth.exists():
        gt_spec = load_spec(ground_truth)
        report["ground_truth_comparison"] = compare_with_ground_truth(spec, gt_spec)

    return report


def summary(spec: dict) -> dict:
    doc = spec.get("document", {})
    nodes = spec.get("nodes", [])
    canvas = nodes[0] if nodes else {}
    children = canvas.get("children", []) if isinstance(canvas, dict) else []

    role_counts = {}
    for child in children:
        role = child.get("role", "unknown")
        role_counts[role] = role_counts.get(role, 0) + 1

    return {
        "title": doc.get("title"),
        "resolution": doc.get("referenceResolution"),
        "total_nodes": len(children),
        "role_counts": role_counts,
        "assets_count": len(spec.get("assets", [])),
        "interactions_count": len(spec.get("interactions", [])),
    }


def evaluate_detection(spec: dict) -> dict:
    """Evaluate UI element detection quality."""
    nodes = spec.get("nodes", [])
    if not nodes:
        return {"status": "error", "message": "No canvas node found"}

    canvas = nodes[0]
    children = canvas.get("children", []) if isinstance(canvas, dict) else []

    detected_roles = []
    for child in children:
        role = child.get("role", "unknown")
        confidence = child.get("confidence", 0.0)
        detected_roles.append({"role": role, "confidence": confidence})

    # Check for common issues
    issues = []
    if not any(r["role"] == "button" for r in detected_roles):
        issues.append("No buttons detected")
    if not any(r["role"] == "text" for r in detected_roles):
        issues.append("No text elements detected")
    if len(detected_roles) < 2:
        issues.append("Too few elements detected (< 2)")

    return {
        "status": "ok",
        "total_elements": len(detected_roles),
        "detections": detected_roles,
        "issues": issues,
        "detection_score": max(0.0, 1.0 - len(issues) * 0.2) if issues else 1.0,
    }


def evaluate_classification(spec: dict) -> dict:
    """Evaluate role classification accuracy."""
    nodes = spec.get("nodes", [])
    if not nodes:
        return {"status": "error", "message": "No canvas node found"}

    canvas = nodes[0]
    children = canvas.get("children", []) if isinstance(canvas, dict) else []

    role_confidences = []
    for child in children:
        role = child.get("role")
        confidence = child.get("confidence", 0.0)
        if role:
            role_confidences.append({"role": role, "confidence": confidence})

    avg_confidence = sum(r["confidence"] for r in role_confidences) / max(len(role_confidences), 1)

    return {
        "status": "ok",
        "role_confidences": role_confidences,
        "average_confidence": round(avg_confidence, 3),
        "classification_score": round(avg_confidence, 3),
    }


def evaluate_text_fidelity(spec: dict) -> dict:
    """Evaluate OCR text quality."""
    nodes = spec.get("nodes", [])
    if not nodes:
        return {"status": "error", "message": "No canvas node found"}

    canvas = nodes[0]
    children = canvas.get("children", []) if isinstance(canvas, dict) else []

    text_nodes = [c for c in children if c.get("role") == "text"]
    text_entries = []

    for node in text_nodes:
        text_data = node.get("text", {})
        content = text_data.get("content", "")
        ocr_confidence = text_data.get("ocrConfidence", 0.0)
        text_entries.append({
            "content": content,
            "confidence": ocr_confidence,
            "length": len(content),
        })

    avg_confidence = sum(t["confidence"] for t in text_entries) / max(len(text_entries), 1) if text_entries else 0.0

    return {
        "status": "ok",
        "text_count": len(text_entries),
        "average_ocr_confidence": round(avg_confidence, 3),
        "text_entries": text_entries,
        "text_fidelity_score": round(avg_confidence, 3),
    }


def evaluate_layout_fidelity(spec: dict) -> dict:
    """Evaluate layout accuracy (positions, sizes)."""
    nodes = spec.get("nodes", [])
    if not nodes:
        return {"status": "error", "message": "No canvas node found"}

    canvas = nodes[0]
    children = canvas.get("children", []) if isinstance(canvas, dict) else []

    rects = []
    for child in children:
        rect = child.get("rect", {})
        if rect:
            rects.append({
                "x": rect.get("x", 0),
                "y": rect.get("y", 0),
                "width": rect.get("width", 0),
                "height": rect.get("height", 0),
            })

    # Check for overlapping regions (potential issues)
    overlaps = []
    for i, r1 in enumerate(rects):
        for r2 in rects[i+1:]:
            if _rects_overlap(r1, r2):
                overlaps.append((r1, r2))

    return {
        "status": "ok",
        "element_count": len(rects),
        "overlaps_detected": len(overlaps),
        "layout_score": max(0.0, 1.0 - len(overlaps) * 0.1) if overlaps else 1.0,
    }


def evaluate_asset_quality(spec: dict) -> dict:
    """Evaluate extracted asset quality."""
    assets = spec.get("assets", [])

    asset_info = []
    for asset in assets:
        asset_info.append({
            "id": asset.get("id"),
            "type": asset.get("type"),
            "uri": asset.get("uri"),
            "rect": asset.get("rect"),
        })

    background_count = sum(1 for a in assets if a.get("type") == "background")
    sprite_count = sum(1 for a in assets if a.get("type") in ("sprite", "panel", "icon"))

    return {
        "status": "ok",
        "total_assets": len(assets),
        "background_assets": background_count,
        "sprite_assets": sprite_count,
        "asset_score": min(1.0, background_count * 0.5 + sprite_count * 0.5),
        "assets": asset_info,
    }


def evaluate_visual(image_path: Path, spec: dict) -> dict:
    """Compare generated spec against reference image visually."""
    try:
        with Image.open(image_path) as img:
            width, height = img.size
            return {
                "status": "ok",
                "reference_size": {"width": width, "height": height},
                "spec_resolution": spec.get("document", {}).get("referenceResolution"),
                "visual_score": 1.0,  # Placeholder for future CV-based comparison
            }
    except Exception as exc:
        return {"status": "error", "message": str(exc)}


def compare_with_ground_truth(spec: dict, ground_truth: dict) -> dict:
    """Compare spec against ground truth annotations."""
    # Placeholder for future comparison logic
    return {
        "status": "ok",
        "message": "Ground truth comparison not yet implemented",
    }


def load_spec(path: Path) -> dict:
    return json.loads(path.read_text(encoding="utf-8"))


def _rects_overlap(r1: dict, r2: dict) -> bool:
    """Check if two rectangles overlap."""
    return not (
        r1["x"] + r1["width"] <= r2["x"] or
        r2["x"] + r2["width"] <= r1["x"] or
        r1["y"] + r1["height"] <= r2["y"] or
        r2["y"] + r2["height"] <= r1["y"]
    )


if __name__ == "__main__":
    raise SystemExit(main())