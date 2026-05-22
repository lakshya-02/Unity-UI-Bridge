#!/usr/bin/env python3
"""Generate test UI images for evaluating image-to-spec reconstruction quality.

This script creates synthetic UI screenshots with known layouts to test
the image-to-spec pipeline with verifiable ground truth.
"""

from __future__ import annotations

import argparse
import json
import math
import sys
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont


def create_test_button_1(size=(640, 360)):
    """Create a simple test image with one large button."""
    img = Image.new("RGB", size, "#101820")
    draw = ImageDraw.Draw(img)

    # Draw rounded rectangle button
    button_rect = (150, 120, 470, 240)
    draw.rounded_rectangle(button_rect, radius=16, fill="#2d6cdf", outline="#5cc8ff", width=4)

    # Draw text
    font = get_font(48)
    text = "CLICK ME"
    bbox = draw.textbbox((0, 0), text, font=font)
    text_width = bbox[2] - bbox[0]
    text_x = (size[0] - text_width) // 2
    text_y = (size[1] - (bbox[3] - bbox[1])) // 2
    draw.text((text_x, text_y), text, fill="#ffffff", font=font)

    return img


def create_test_button_and_text(size=(640, 360)):
    """Create test image with button and text elements."""
    img = Image.new("RGB", size, "#101820")
    draw = ImageDraw.Draw(img)

    # Draw title text at top
    title_font = get_font(36)
    draw.text((150, 30), "Settings", fill="#ffffff", font=title_font)

    # Draw two buttons
    draw.rounded_rectangle((150, 120, 470, 180), radius=12, fill="#2d6cdf", outline="#5cc8ff", width=3)
    draw.text((250, 134), "Save", fill="#ffffff", font=get_font(32))

    draw.rounded_rectangle((150, 220, 470, 280), radius=12, fill="#2d6cdf", outline="#5cc8ff", width=3)
    draw.text((250, 234), "Cancel", fill="#ffffff", font=get_font(32))

    return img


def create_test_complex_ui(size=(640, 360)):
    """Create a more complex UI with multiple elements."""
    img = Image.new("RGB", size, "#101820")
    draw = ImageDraw.Draw(img)

    # Title bar
    draw.rectangle((0, 0, 640, 100), fill="#243241")
    font = get_font(28)
    draw.text((20, 30), "Menu", fill="#ffffff", font=font)

    # Menu items
    for i in range(4):
        y = 100 + i * 60
        draw.rectangle((40, y, 600, y + 50), fill="#1a2332")
        draw.text((60, y + 10), f"Option {i+1}", fill="#ffffff", font=get_font(24))

    return img


def get_font(size):
    """Try to get a system font, fallback to default."""
    try:
        # Try to use a system font
        return ImageFont.truetype("/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf", size)
    except:
        try:
            return ImageFont.truetype("arial.ttf", size)
        except:
            return ImageFont.load_default()


def main():
    parser = argparse.ArgumentParser(description="Generate test UI images")
    parser.add_argument("--output-dir", default="Assets/UnityUIBridge/Generated/TestImages", help="Output directory")
    args = parser.parse_args()

    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    # Generate test images
    tests = [
        ("test_single_button", create_test_button_1),
        ("test_buttons_and_text", create_test_button_and_text),
        ("test_complex_ui", create_test_complex_ui),
    ]

    for name, create_func in tests:
        img = create_func()
        output_path = output_dir / f"{name}.png"
        img.save(output_path)
        print(f"Created: {output_path}")

    print(f"\nGenerated {len(tests)} test images in {output_dir}")


if __name__ == "__main__":
    main()
