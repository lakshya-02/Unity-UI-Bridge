# Contributing to Unity UI Bridge

Unity UI Bridge is an open-source, local-first project for AI-assisted Unity UI workflows. Contributions should preserve the project goals: transparent architecture, offline-friendly tooling, modular adapters, and no required paid APIs or proprietary inference services.

## Contribution Principles

- Prefer open-source dependencies with clear licenses.
- Keep model integrations replaceable through adapters.
- Do not make closed-source services part of the required workflow.
- Keep the JSON Schema as the shared contract between AI tools and Unity.
- Add sample fixtures and validation coverage for schema changes.
- Preserve unknown or low-confidence UI regions so reviewers can make informed decisions.

## Local Validation

Install Python dependencies:

```powershell
python -m pip install -r Assets\UnityUIBridge\Tools\Python\requirements.txt
```

Run the schema validator:

```powershell
python Assets\UnityUIBridge\Tools\Python\validate_specs.py --all
```

Run the Python validator tests:

```powershell
python -m unittest Assets.UnityUIBridge.Tools.Python.Tests.test_validate_specs
```

Unity EditMode tests should also pass from the Unity Test Runner.
