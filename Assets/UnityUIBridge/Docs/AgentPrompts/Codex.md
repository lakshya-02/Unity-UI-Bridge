Task: Verify full-screen image reconstruction and prepare detector improvements

Goal:
Make generated UIs import as one full-screen background image on the selected Canvas, with transparent Unity Button hotspots and editable OCR text layered on top.

Files likely involved:
- Assets/UnityUIBridge/Tools/Python/image_to_ui_spec.py
- Assets/UnityUIBridge/Editor/Import/UnityUiBridgeImporter.cs
- Assets/UnityUIBridge/Editor/UnityUiBridgeImporterWindow.cs
- Assets/UnityUIBridge/Tests/EditMode/UnityUiBridgeImporterTests.cs
- Assets/UnityUIBridge/Docs/image-to-ui-pipeline.md

Acceptance criteria:
- Generated spec contains one background asset by default.
- Region sprite crops are only emitted with --emit-region-sprites.
- Import root stretches inside the selected Canvas.
- Source Frame fills the target Canvas by default, with a letterbox option still available.
- Unity buttons are transparent hotspots when a background is present.
- Python validation and unit tests pass.
- dotnet build passes.
- Generated files and model caches are not committed.

Next research after this:
Evaluate OpenCV adaptive thresholding, multi-scale contour detection, OCR confidence filtering, and optional Tesseract/scikit-image/ImageMagick adapters using 3-4 user-provided test images.