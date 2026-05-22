# Unity UI Bridge Importer v1

The v1 importer is a local Unity Editor workflow that reads a validated Unity UI Bridge JSON spec and creates a uGUI Canvas hierarchy.

## Install and Access

No model download is required.

No API key is required.

No Unity package install is required for this step. The project already includes:

- `com.unity.ugui`
- `com.unity.test-framework`

The only Python package currently used by the project is `jsonschema`, for local spec validation:

```powershell
python -m pip install -r Assets\UnityUIBridge\Tools\Python\requirements.txt
```

For image-to-spec generation, install the local AI/CV stack:

```powershell
python -m pip install -r Assets\UnityUIBridge\Tools\Python\requirements-ai.txt
```

## Editor Menu

Open the importer from:

```text
Tools > Unity UI Bridge > Importer
```

Quick-import the bundled main menu fixture from:

```text
Tools > Unity UI Bridge > Import Main Menu Sample
```

For best alignment in an existing scene, select your scene Canvas before importing or press `Use Scene Canvas` in the importer window. The importer will place the generated hierarchy under that Canvas and scale the screenshot coordinates to the target Canvas size.

## What It Builds

The importer currently creates:

- Canvas
- CanvasScaler
- GraphicRaycaster
- Optional EventSystem
- Import under a selected/existing scene Canvas
- Stretch the imported root inside the selected scene Canvas
- Fill the target Canvas by default, with optional letterbox fitting
- RectTransform hierarchy from spec nodes
- Image components for panels, icons, image nodes, and unknown nodes
- Sprite assignment for generated `assetRef` PNG files
- Button components for `button` nodes
- Transparent button hotspots when a generated full-image background is present
- Text components for `text` nodes
- InputField, Toggle, and Slider components for matching roles
- Optional vertical or horizontal layout groups when explicitly enabled in the importer window

This is intentionally not a pixel-perfect renderer yet. It is a reconstruction scaffold: a fast, inspectable uGUI hierarchy that developers can tune by hand or improve with future adapters.

For screenshot reconstruction, keep `Apply Layout Groups` off. Unity layout groups intentionally reposition their children, which is useful for responsive UI authoring but wrong when the goal is to preserve absolute positions from the source spec.

If placement looks wrong, delete the imported root, select the Canvas you actually want to import into, open `Tools > Unity UI Bridge > Importer`, click `Use Scene Canvas`, and import with `Fit To Target Canvas` and `Fill Target Canvas` on.

Use `Fill Target Canvas` for normal image reconstruction. Disable it only when you want letterboxing and no cropping.

## Current Limitations

- Sprite assignment expects assets to live inside the Unity project, usually under `Assets/UnityUIBridge/Generated/Sprites/`.
- Automatic button detection is heuristic and should be reviewed in the Scene hierarchy.
- Style color dictionaries are validated by JSON Schema but not fully mapped into Unity components yet.
- Runtime import is not implemented yet; this is editor-only.
- Vision-language hierarchy reasoning and advanced segmentation adapters are not implemented yet.

## Model and API Policy

Before adding any model dependency, model download, online inference service, API key, or paid/closed-source integration, the contributor should stop and get explicit approval.

The intended future path is:

1. Keep the JSON spec and importer local-first.
2. Add optional open-source model adapters behind clear interfaces.
3. Allow contributors to choose their own local models and hardware.
4. Never require a paid API or proprietary SaaS workflow for core project use.
