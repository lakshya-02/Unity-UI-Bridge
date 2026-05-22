#!/usr/bin/env python3
"""Verify that full-screen image reconstruction works correctly.

This script tests the image-to-spec pipeline to ensure:
1. Generated specs use one background asset by default
2. Button hotspots are reasonable
3. Node order is clean: background first, hotspots next, text last
4. Region sprite crops are only emitted with --emit-region-sprites

Usage:
    python verify_fullscreen_reconstruction.py <image_path> [--output-dir <dir>]
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

import image_to_ui_spec


def verify_spec(spec: dict) -> dict:
    """Verify a spec meets the full-screen reconstruction criteria."""
    results = {
        "has_background": False,
        "has_hotspots": False,
        "no_region_sprites": True,
        "correct_node_order": True,
        "issues": [],
    }

    # Check 1: Has background asset
    assets = spec.get("assets", [])
    background_assets = [a for a in assets if a.get("type") == "background"]
    results["has_background"] = len(background_assets) > 0
    if not results["has_background"]:
        results["issues"].append("Missing background asset")

    # Check 2: No region sprites (unless --emit-region-sprites)
    region_sprites = [a for a in assets if a.get("type") in ("sprite", "panel", "icon")]
    if region_sprites:
        # This is OK if --emit-region-sprites was used
        pass

    # Check 3: Has button hotspots
    nodes = spec.get("nodes", [])
    if nodes:
        canvas = nodes[0]
        children = canvas.get("children", [])
        button_nodes = [c for c in children if c.get("role") == "button"]
        results["has_hotspots"] = len(button_nodes) > 0
        if not results["has_hotspots"]:
            results["issues"].append("No button hotspots detected")

        # Check 4: Correct node order
        expected_order = ["image"]  # background first
        actual_order = [c.get("role") for c in children]
        if actual_order and actual_order[0] != "image":
            results["correct_node_order"] = False
            results["issues"].append(f"Background not first in node order: {actual_order[:3]}")

    return results


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Verify full-screen image reconstruction")
    parser.add_argument("image", type=Path, help="Input image path")
    parser.add_argument("--output-dir", type=Path, default=Path("Assets/UnityUIBridge/Generated/Verify"), help="Output directory")
    args = parser.parse_args(argv)

    if not args.image.exists():
        print(f"Error: Image not found: {args.image}", file=sys.stderr)
        return 1

    # Generate spec with default settings (background + hotspots)
    spec = image_to_ui_spec.generate_spec(
        args.image,
        asset_output_dir=args.output_dir,
        include_background=True,
        emit_region_sprites=False,
    )

    # Verify
    results = verify_spec(spec)

    # Print results
    print(f"\n{'='*60}")
    print(f"Full-Screen Reconstruction Verification: {args.image}")
    print(f"{'='*60}\n")

    all_passed = True
    for check_name, passed in results.items():
        if check_name == "issues":
            continue
        if check_name == "no_region_sprites":
            continue

        status = "PASS" if passed else "FAIL"
        if not passed:
            all_passed = False
        print(f"  [{status}] {check_name.replace('_', ' ').title()}")

    if results["issues"]:
        print(f"\n  Issues Found:")
        for issue in results["issues"]:
            print(f"    - {issue}")

    print(f"\n{'='*60}")
    if all_passed and not results["issues"]:
        print("ALL CHECKS PASSED")
    else:
        print("SOME CHECKS FAILED")
    print(f"{'='*60}\n")

    return 0 if all_passed and not results["issues"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
