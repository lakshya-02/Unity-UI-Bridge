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

By default, the generator also writes cropped sprite assets next to the generated spec:

```text
Assets/UnityUIBridge/Generated/Sprites/<image-name>/
```

Disable OCR for a faster layout-only pass:

```powershell
python Assets\UnityUIBridge\Tools\Python\image_to_ui_spec.py path\to\image.png --output Assets\UnityUIBridge\Generated\Specs\generated-ui.json --no-ocr
```

Disable the full-image background fallback when you only want cropped regions:

```powershell
python Assets\UnityUIBridge\Tools\Python\image_to_ui_spec.py path\to\image.png --output Assets\UnityUIBridge\Generated\Specs\generated-ui.json --no-background
```

Generated specs and sprites under `Assets/UnityUIBridge/Generated/` are ignored by Git. Move a generated spec into `Assets/UnityUIBridge/Samples/Specs/` only when it is curated enough to become a fixture.

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

## Planned Prompt To Concept Adapter

The project can support a `prompt -> concept image -> spec -> Unity` path for users who do not already have a UI reference image. This should be implemented as an optional adapter, not as a core dependency.

Recommended adapter rules:

- Local/open-source image generation comes first, for example a user-managed ComfyUI workflow.
- Any online trial service, including NVIDIA NIM Qwen-Image, must be optional, clearly marked as online, and require the user to provide their own API key.
- No image-generation model weights, caches, generated images, or API keys should be committed.
- The generated concept image should enter the same image-to-spec pipeline as a user-provided screenshot.

## Current Capabilities

- Detects major layout regions with OpenCV contours.
- Classifies broad regions as panels, buttons, icons, or images.
- Crops detected visual regions into reusable PNG sprite assets.
- Emits a source-background asset so first-pass imports visually line up with the reference image.
- Detects text using PaddleOCR when available, with EasyOCR fallback on Windows.
- Emits a valid Unity UI Bridge v1 JSON spec.
- Imports the generated spec into uGUI and assigns referenced sprites to Image/Button/Panel nodes.

## Current Limits

- This is a first-pass reconstruction scaffold, not pixel-perfect cloning.
- Cropped assets may include baked text or background pixels until stronger segmentation/inpainting adapters are added.
- Stylized pixel fonts can confuse OCR and may need manual text cleanup.
- Buttons are inferred from geometry and may need review.
- Advanced vision-language hierarchy reasoning is not installed yet.
