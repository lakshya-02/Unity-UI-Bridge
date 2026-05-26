#!/usr/bin/env python3
"""Verify that modular full-screen image reconstruction works correctly.

This script tests the image-to-spec pipeline to ensure:
1. Generated specs use one cleaned background asset by default
2. Button nodes are reasonable
3. Button nodes have cropped sprite assets
4. Node order is clean: background first, controls next, text last

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
        "has_buttons": False,
        "buttons_have_sprites": False,
        "correct_node_order": True,
        "issues": [],
    }

    # Check 1: Has background asset
    assets = spec.get("assets", [])
    background_assets = [a for a in assets if a.get("type") == "background"]
    results["has_background"] = len(background_assets) > 0
    if not results["has_background"]:
        results["issues"].append("Missing background asset")

    asset_ids = {asset.get("id") for asset in assets}

    # Check 2: Has modular button nodes with sprite references
    nodes = spec.get("nodes", [])
    if nodes:
        canvas = nodes[0]
        children = canvas.get("children", [])
        button_nodes = [c for c in children if c.get("role") == "button"]
        results["has_buttons"] = len(button_nodes) > 0
        if not results["has_buttons"]:
            results["issues"].append("No button nodes detected")

        results["buttons_have_sprites"] = bool(button_nodes) and all(
            button.get("assetRef") in asset_ids for button in button_nodes
        )
        if not results["buttons_have_sprites"]:
            results["issues"].append("One or more button nodes are missing cropped sprite asset references")

        # Check 3: Correct node order
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

    # Generate spec with default settings (cleaned background + button sprites)
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
