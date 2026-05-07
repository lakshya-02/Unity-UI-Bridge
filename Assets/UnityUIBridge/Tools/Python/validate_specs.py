#!/usr/bin/env python3
"""Validate Unity UI Bridge v1 JSON fixtures against the canonical schema."""

from __future__ import annotations

import argparse
import json
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable, Sequence

try:
    from jsonschema import Draft202012Validator
except ImportError as exc:  # pragma: no cover - exercised by CLI users without deps.
    raise SystemExit(
        "Missing dependency: jsonschema. Install it with `python -m pip install jsonschema`."
    ) from exc


@dataclass(frozen=True)
class ValidationErrorInfo:
    message: str
    instance_path: str
    schema_path: str


@dataclass(frozen=True)
class ValidationRunResult:
    valid_count: int
    expected_invalid_count: int
    failures: list[str]


def default_project_root() -> Path:
    return Path(__file__).resolve().parents[4]


def default_schema_path(project_root: Path) -> Path:
    return project_root / "Assets" / "UnityUIBridge" / "Specs" / "v1" / "ui-bridge.schema.json"


def default_samples_dir(project_root: Path) -> Path:
    return project_root / "Assets" / "UnityUIBridge" / "Samples" / "Specs"


def validate_file(schema_path: Path, spec_path: Path) -> list[ValidationErrorInfo]:
    schema = _read_json(schema_path)
    document = _read_json(spec_path)
    validator = Draft202012Validator(schema)
    errors = sorted(validator.iter_errors(document), key=lambda error: list(error.path))
    return [
        ValidationErrorInfo(
            message=error.message,
            instance_path=_format_path(error.path),
            schema_path=_format_path(error.schema_path),
        )
        for error in errors
    ]


def validate_expected_valid(schema_path: Path, spec_paths: Sequence[Path]) -> ValidationRunResult:
    failures: list[str] = []
    for spec_path in spec_paths:
        errors = validate_file(schema_path, spec_path)
        if errors:
            failures.append(_format_failure(spec_path, errors))

    return ValidationRunResult(valid_count=len(spec_paths), expected_invalid_count=0, failures=failures)


def validate_all(project_root: Path | None = None) -> ValidationRunResult:
    root = project_root or default_project_root()
    schema_path = default_schema_path(root)
    samples_dir = default_samples_dir(root)
    valid_specs = sorted(samples_dir.glob("*.valid.json"))
    invalid_specs = sorted(samples_dir.glob("*.invalid.json"))

    failures: list[str] = []
    failures.extend(validate_expected_valid(schema_path, valid_specs).failures)

    for spec_path in invalid_specs:
        errors = validate_file(schema_path, spec_path)
        if not errors:
            failures.append(f"{spec_path}: expected fixture to fail schema validation, but it passed")

    return ValidationRunResult(
        valid_count=len(valid_specs),
        expected_invalid_count=len(invalid_specs),
        failures=failures,
    )


def main(argv: Sequence[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Validate Unity UI Bridge JSON specs.")
    parser.add_argument("specs", nargs="*", type=Path, help="Specific spec files to validate as expected-valid.")
    parser.add_argument("--schema", type=Path, help="Path to ui-bridge.schema.json.")
    parser.add_argument("--project-root", type=Path, default=default_project_root(), help="Unity project root.")
    parser.add_argument("--all", action="store_true", help="Validate all sample fixtures.")
    args = parser.parse_args(argv)

    schema_path = args.schema or default_schema_path(args.project_root)
    if args.all:
        result = validate_all(args.project_root)
    else:
        if not args.specs:
            parser.error("provide spec paths or use --all")
        result = validate_expected_valid(schema_path, args.specs)

    if result.failures:
        for failure in result.failures:
            print(failure, file=sys.stderr)
        return 1

    if args.all:
        print(
            f"{result.valid_count} valid specs passed; "
            f"{result.expected_invalid_count} expected invalid specs failed."
        )
    else:
        print(f"{result.valid_count} specs passed.")
    return 0


def _read_json(path: Path) -> object:
    with path.open("r", encoding="utf-8") as handle:
        return json.load(handle)


def _format_failure(spec_path: Path, errors: Sequence[ValidationErrorInfo]) -> str:
    details = "; ".join(
        f"{error.instance_path}: {error.message} (schema: {error.schema_path})" for error in errors
    )
    return f"{spec_path}: {details}"


def _format_path(path: Iterable[object]) -> str:
    parts = [str(part) for part in path]
    return "/".join(parts) if parts else "<root>"


if __name__ == "__main__":
    raise SystemExit(main())
