# Image To UI Pipeline

The local image-to-UI path is:

1. Select a screenshot or concept image.
2. Run the Python image-to-spec generator.
3. Validate the generated JSON against the v1 schema.
4. Import the generated spec into a Unity uGUI Canvas.

No API key is required.

## Installed Local Models

The current OCR stack is open source and local:

- PaddleOCR/PaddlePaddle is installed and its OCR models are cached in `C:\Users\Lakshya\.paddlex\official_models`.
- EasyOCR is installed as a Windows-safe fallback and its English models are cached in `C:\Users\Lakshya\.EasyOCR\model`.

The generated model files are intentionally not committed to Git.

## Command Line

Generate and validate a spec:

```powershell
python Assets\UnityUIBridge\Tools\Python\image_to_ui_spec.py path\to\image.png --output Assets\UnityUIBridge\Generated\Specs\generated-ui.json
```

Disable OCR for a faster layout-only pass:

```powershell
python Assets\UnityUIBridge\Tools\Python\image_to_ui_spec.py path\to\image.png --output Assets\UnityUIBridge\Generated\Specs\generated-ui.json --no-ocr
```

Generated specs under `Assets/UnityUIBridge/Generated/` are ignored by Git. Move a generated spec into `Assets/UnityUIBridge/Samples/Specs/` only when it is curated enough to become a fixture.

## Unity Editor

Open:

```text
Tools > Unity UI Bridge > Importer
```

Use the `Image To Spec` section:

- `Image Path`: screenshot or UI concept image.
- `Output Spec`: generated JSON destination.
- `Run OCR`: enable OCR text nodes.
- `Import After Generate`: immediately import the generated spec into the target Canvas.

For scene alignment, select your target Canvas first or click `Use Scene Canvas`.

## Current Capabilities

- Detects major layout regions with OpenCV contours.
- Classifies broad regions as panels, buttons, icons, or images.
- Detects text using PaddleOCR when available, with EasyOCR fallback on Windows.
- Emits a valid Unity UI Bridge v1 JSON spec.
- Imports the generated spec into uGUI through the existing importer.

## Current Limits

- This is a first-pass reconstruction scaffold, not pixel-perfect cloning.
- Icon separation and sprite extraction are not yet implemented.
- Buttons are inferred from geometry and may need review.
- Advanced vision-language hierarchy reasoning is not installed yet.
