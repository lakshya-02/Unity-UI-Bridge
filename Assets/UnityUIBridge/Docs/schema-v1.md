# Unity UI Bridge Schema v1

Unity UI Bridge v1 is a contract-first intermediate representation for AI-assisted Unity UI reconstruction. It does not try to clone screenshots pixel-for-pixel. Its job is to describe reusable UI structure, extracted assets, styles, and interactions well enough that local tools can inspect, validate, edit, and eventually assemble uGUI canvases.

The canonical schema is `Assets/UnityUIBridge/Specs/v1/ui-bridge.schema.json`.

## Design Goals

- Keep the format open, readable, and self-hostable.
- Make AI output transparent enough for developers and technical artists to review.
- Support local-first model adapters without depending on paid APIs.
- Preserve low-confidence or unknown regions instead of silently dropping them.
- Target Unity uGUI first, including screen-space and world-space/XR canvases.

## Root Structure

Every v1 document contains:

- `schemaVersion`: fixed to `1.0.0`.
- `document`: source metadata, reference resolution, coordinate system, and Unity target.
- `assets`: reusable extracted sprites, icons, panels, backgrounds, text regions, and masks.
- `styles`: reusable visual tokens for colors, typography, borders, shadows, glow, states, and transitions.
- `nodes`: the semantic/layout hierarchy that a Unity importer will later assemble.
- `interactions`: button, toggle, input, slider, navigation, and action metadata.
- `extensions`: namespaced experimental metadata.

## Coordinates

The base coordinate system is always source-image pixel space:

- Origin: `top-left`
- Unit: `pixel`
- Y axis: `down`

Every node must include a raw `rect` in this source coordinate system. Unity-specific `anchors` and `pivot` may be included, but they do not replace the raw rect. This keeps the spec useful to non-Unity tools and makes model output easier to inspect.

## Nodes

The v1 roles are:

`canvas`, `group`, `panel`, `image`, `icon`, `text`, `button`, `input`, `toggle`, `slider`, and `unknown`.

Use `unknown` for low-confidence regions that should remain visible to downstream tools or reviewers. A future Unity importer can decide whether to create a placeholder object, flag it in an editor report, or ignore it by policy.

Nodes may include:

- `styleRef` for reusable style tokens.
- `assetRef` for extracted sprites or panel assets.
- `interactionRef` for interactable components.
- `text` for OCR or model-provided text metadata.
- `confidence` and `provenance` for traceability.
- `children` for hierarchy.
- `extensions` for adapter-specific metadata.

## Extensions

Extension keys must be namespaced, such as:

- `org.unity-ui-bridge.xr`
- `org.unity-ui-bridge.semantic`
- `community.example-extractor`

Core tools should preserve extension objects even when they do not understand them. Extensions are for experimental model outputs, XR hints, semantic bindings, importer hints, and cross-engine research. Stable concepts should graduate into the core schema only after real usage proves they are broadly useful.

## Adapter Slots

v1 defines the contract, not the full AI pipeline. Future contributors can add adapters around the schema:

- Vision-language understanding adapter: proposes hierarchy, roles, layout patterns, confidence, and provenance.
- Segmentation adapter: produces masks and asset regions for panels, icons, sprites, and backgrounds.
- OCR/text-region adapter: identifies text regions and recognized content.
- Asset extraction adapter: turns detected regions into reusable sprite assets.
- Unity importer adapter: reads the JSON spec and assembles uGUI canvases, prefabs, and interactable components.

Candidate open-source model families may be documented as examples, but they must remain replaceable. The project should not require paid inference systems or proprietary AI services.

## Validation

Install the validator dependency:

```powershell
python -m pip install -r Assets\UnityUIBridge\Tools\Python\requirements.txt
```

Validate all sample fixtures:

```powershell
python Assets\UnityUIBridge\Tools\Python\validate_specs.py --all
```

The fixture convention is:

- `*.valid.json` must pass schema validation.
- `*.invalid.json` must fail schema validation.

Unity EditMode tests call the same Python validator so Unity-facing tests and AI/tooling tests share one source of truth.
