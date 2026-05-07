# Unity UI Bridge

Unity UI Bridge is an open-source, local-first pipeline for reconstructing modular Unity uGUI layouts from reference UI images.

The project currently includes:

- A v1 JSON intermediate representation and schema.
- Sample valid and invalid UI specs.
- A local Python validator.
- A Unity Editor importer for v1 specs.
- Early local image-to-spec tooling for screenshot/concept image reconstruction.
- Local OCR/CV dependencies for image-to-spec generation without API keys.

Core goals:

- Fully open source and free to use.
- Local-first workflows.
- Replaceable open-source AI/model adapters.
- No required paid APIs or proprietary services.
- Unity uGUI first, with XR/world-space workflows in mind.
