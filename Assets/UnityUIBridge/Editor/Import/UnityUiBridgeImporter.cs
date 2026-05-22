using System.IO;
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityUIBridge.Runtime.Spec;
using Object = UnityEngine.Object;

namespace UnityUIBridge.Editor.Import
{
    public sealed class UnityUiBridgeImportOptions
    {
        public string RootName = "Unity UI Bridge Import";
        public bool CreateEventSystem = true;
        public bool ApplyLayoutGroups = false;
        public Canvas TargetCanvas;
        public bool FitToTargetCanvas = true;
        public bool PreserveAspectRatio = true;
        public bool FillTargetCanvas = true;
        public bool CreateDebugOverlay = false;
        public bool ReplaceExistingImports = true;
    }

    public static class UnityUiBridgeImporter
    {
        public static GameObject Import(UnityUiBridgeSpec spec, UnityUiBridgeImportOptions options = null)
        {
            if (spec == null)
            {
                throw new System.ArgumentNullException(nameof(spec));
            }

            spec.Normalize();
            options ??= new UnityUiBridgeImportOptions();
            options.TargetCanvas = ResolveImportTargetCanvas(options.TargetCanvas);
            if (options.ReplaceExistingImports)
            {
                DeleteExistingImportRoots(options.RootName, options.TargetCanvas);
            }

            var canvasNode = spec.nodes.Length > 0 ? spec.nodes[0] : null;
            var referenceResolution = ResolveReferenceResolution(spec, canvasNode);
            var root = options.TargetCanvas != null
                ? CreateImportRoot(options.TargetCanvas, options.RootName, referenceResolution, options)
                : CreateCanvas(options.RootName, referenceResolution, spec.document?.target?.canvasMode);
            var targetResolution = options.TargetCanvas != null && options.FitToTargetCanvas
                ? ResolveTargetCanvasSize(options.TargetCanvas, referenceResolution)
                : ResolveRootSize(root, referenceResolution);
            var sourceFrame = CreateSourceFrame(
                root.transform,
                canvasNode?.rect,
                referenceResolution,
                targetResolution,
                options);

            if (canvasNode?.children != null)
            {
                foreach (var child in canvasNode.children)
                {
                    CreateNode(spec, child, sourceFrame, canvasNode.rect, options);
                }

                if (options.CreateDebugOverlay)
                {
                    CreateDebugOverlay(canvasNode.children, sourceFrame, canvasNode.rect);
                }
            }

            if (options.CreateEventSystem)
            {
                EnsureEventSystem();
            }

            Selection.activeGameObject = root;
            return root;
        }

        public static Canvas FindSceneCanvasForImport()
        {
            var selectedCanvas = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<Canvas>()
                : null;
            if (selectedCanvas != null && !IsUnityUiBridgeImportCanvas(selectedCanvas))
            {
                return selectedCanvas;
            }

            var canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var canvas in canvases)
            {
                if (!IsUnityUiBridgeImportCanvas(canvas))
                {
                    return canvas;
                }
            }

            return null;
        }

        public static Canvas ResolveImportTargetCanvas(Canvas requestedCanvas)
        {
            if (requestedCanvas != null && !IsUnityUiBridgeImportCanvas(requestedCanvas))
            {
                return requestedCanvas;
            }

            return FindSceneCanvasForImport();
        }

        private static GameObject CreateCanvas(string rootName, Vector2 referenceResolution, string canvasMode)
        {
            var root = new GameObject(string.IsNullOrWhiteSpace(rootName) ? "Unity UI Bridge Import" : rootName);
            var rectTransform = root.AddComponent<RectTransform>();
            rectTransform.sizeDelta = referenceResolution;

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = canvasMode == "world-space" ? RenderMode.WorldSpace : RenderMode.ScreenSpaceOverlay;

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();
            Undo.RegisterCreatedObjectUndo(root, "Import Unity UI Bridge Spec");
            return root;
        }

        private static void DeleteExistingImportRoots(string rootName, Canvas targetCanvas)
        {
            var normalizedRootName = string.IsNullOrWhiteSpace(rootName) ? "Unity UI Bridge Import" : rootName;
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var transform in transforms)
            {
                var gameObject = transform.gameObject;
                if (targetCanvas != null && gameObject == targetCanvas.gameObject)
                {
                    continue;
                }

                if (!IsImportRootName(gameObject.name, normalizedRootName))
                {
                    continue;
                }

                Undo.DestroyObjectImmediate(gameObject);
            }
        }

        private static bool IsImportRootName(string objectName, string rootName)
        {
            return string.Equals(objectName, rootName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(objectName, "Unity UI Bridge Import", StringComparison.OrdinalIgnoreCase);
        }

        private static Transform CreateSourceFrame(
            Transform parent,
            UnityUiBridgeRect sourceCanvasRect,
            Vector2 referenceResolution,
            Vector2 targetResolution,
            UnityUiBridgeImportOptions options)
        {
            var sourceSize = ResolveSourceCanvasSize(sourceCanvasRect, referenceResolution);
            var frame = new GameObject("Source Frame");
            Undo.RegisterCreatedObjectUndo(frame, "Create UI Bridge Source Frame");
            frame.transform.SetParent(parent, false);

            var rectTransform = frame.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.sizeDelta = sourceSize;

            var layout = UnityUiBridgeSourceFrameLayout.Create(
                sourceSize,
                options.FitToTargetCanvas ? targetResolution : sourceSize,
                options.PreserveAspectRatio,
                options.FillTargetCanvas);
            rectTransform.anchoredPosition = new Vector2(layout.OffsetX, -layout.OffsetY);
            frame.transform.localScale = new Vector3(layout.ScaleX, layout.ScaleY, 1f);
            return frame.transform;
        }

        private static GameObject CreateImportRoot(
            Canvas targetCanvas,
            string rootName,
            Vector2 referenceResolution,
            UnityUiBridgeImportOptions options)
        {
            var root = new GameObject(string.IsNullOrWhiteSpace(rootName) ? "Unity UI Bridge Import" : rootName);
            Undo.RegisterCreatedObjectUndo(root, "Import Unity UI Bridge Spec");
            root.transform.SetParent(targetCanvas.transform, false);

            var rectTransform = root.AddComponent<RectTransform>();

            if (options.FitToTargetCanvas)
            {
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.pivot = new Vector2(0.5f, 0.5f);
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;
                return root;
            }

            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = referenceResolution;
            return root;
        }

        private static void CreateNode(
            UnityUiBridgeSpec spec,
            UnityUiBridgeNode node,
            Transform parent,
            UnityUiBridgeRect parentRect,
            UnityUiBridgeImportOptions options)
        {
            if (node == null)
            {
                return;
            }

            var gameObject = new GameObject(node.DisplayName);
            Undo.RegisterCreatedObjectUndo(gameObject, "Create UI Bridge Node");
            gameObject.transform.SetParent(parent, false);

            var rectTransform = gameObject.AddComponent<RectTransform>();
            ApplyRectTransform(rectTransform, node, parentRect);
            AddRoleComponents(spec, gameObject, node);
            if (options.ApplyLayoutGroups)
            {
                AddLayoutGroup(gameObject, node);
            }

            if (node.children != null)
            {
                foreach (var child in node.children)
                {
                    CreateNode(spec, child, gameObject.transform, node.rect, options);
                }
            }
        }

        private static void CreateDebugOverlay(
            UnityUiBridgeNode[] nodes,
            Transform sourceFrame,
            UnityUiBridgeRect sourceCanvasRect)
        {
            var overlay = new GameObject("Debug Overlay");
            Undo.RegisterCreatedObjectUndo(overlay, "Create UI Bridge Debug Overlay");
            overlay.transform.SetParent(sourceFrame, false);

            var overlayRect = overlay.AddComponent<RectTransform>();
            overlayRect.anchorMin = new Vector2(0f, 1f);
            overlayRect.anchorMax = new Vector2(0f, 1f);
            overlayRect.pivot = new Vector2(0f, 1f);
            overlayRect.anchoredPosition = Vector2.zero;
            overlayRect.sizeDelta = new Vector2(sourceCanvasRect.width, sourceCanvasRect.height);

            foreach (var node in nodes)
            {
                CreateDebugNode(node, overlay.transform, sourceCanvasRect);
            }
        }

        private static void CreateDebugNode(
            UnityUiBridgeNode node,
            Transform overlay,
            UnityUiBridgeRect sourceCanvasRect)
        {
            if (node == null)
            {
                return;
            }

            var gameObject = new GameObject($"Debug {node.id}");
            Undo.RegisterCreatedObjectUndo(gameObject, "Create UI Bridge Debug Node");
            gameObject.transform.SetParent(overlay, false);

            var rectTransform = gameObject.AddComponent<RectTransform>();
            ApplyRectTransform(rectTransform, node, sourceCanvasRect);

            var image = gameObject.AddComponent<Image>();
            image.color = DebugColorForRole(node.role);
            image.raycastTarget = false;

            CreateDebugLabel(node, gameObject.transform);

            if (node.children == null)
            {
                return;
            }

            foreach (var child in node.children)
            {
                CreateDebugNode(child, overlay, sourceCanvasRect);
            }
        }

        private static void CreateDebugLabel(UnityUiBridgeNode node, Transform parent)
        {
            var label = new GameObject("Label");
            Undo.RegisterCreatedObjectUndo(label, "Create UI Bridge Debug Label");
            label.transform.SetParent(parent, false);

            var rectTransform = label.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0f, 1f);
            rectTransform.anchorMax = new Vector2(0f, 1f);
            rectTransform.pivot = new Vector2(0f, 1f);
            rectTransform.anchoredPosition = new Vector2(4f, -4f);
            rectTransform.sizeDelta = new Vector2(Mathf.Max(180f, node.rect.width), 22f);

            var text = label.AddComponent<Text>();
            text.text = $"{node.id} ({node.role})";
            text.font = ResolveBuiltInFont();
            text.fontSize = 14;
            text.alignment = TextAnchor.MiddleLeft;
            text.color = Color.white;
            text.raycastTarget = false;
        }

        private static Color DebugColorForRole(string role)
        {
            return role switch
            {
                "button" => new Color(0.1f, 0.65f, 1f, 0.22f),
                "text" => new Color(0.15f, 1f, 0.35f, 0.22f),
                "panel" => new Color(1f, 0.7f, 0.1f, 0.18f),
                "icon" => new Color(1f, 0.2f, 0.8f, 0.22f),
                "input" => new Color(0.55f, 0.35f, 1f, 0.22f),
                _ => new Color(1f, 1f, 1f, 0.16f)
            };
        }

        private static void AddRoleComponents(UnityUiBridgeSpec spec, GameObject gameObject, UnityUiBridgeNode node)
        {
            switch (node.role)
            {
                case "button":
                    var buttonImage = EnsureImage(gameObject, ResolveButtonFallbackColor(spec, node));
                    var hasButtonSprite = TryApplyAssetSprite(spec, node, buttonImage);
                    if (!hasButtonSprite && HasSourceBackground(spec))
                    {
                        buttonImage.color = new Color(1f, 1f, 1f, 0f);
                    }

                    var button = gameObject.AddComponent<Button>();
                    button.targetGraphic = buttonImage;
                    break;
                case "text":
                    var text = gameObject.AddComponent<Text>();
                    text.text = node.text?.content ?? string.Empty;
                    text.font = ResolveBuiltInFont();
                    text.fontSize = ResolveFontSize(spec, node, 24);
                    text.alignment = ResolveAlignment(spec, node);
                    text.color = Color.white;
                    text.raycastTarget = false;
                    break;
                case "input":
                    var inputImage = EnsureImage(gameObject, new Color(0.08f, 0.09f, 0.1f, 0.85f));
                    TryApplyAssetSprite(spec, node, inputImage);
                    var input = gameObject.AddComponent<InputField>();
                    input.targetGraphic = inputImage;
                    break;
                case "toggle":
                    TryApplyAssetSprite(spec, node, EnsureImage(gameObject, new Color(0.1f, 0.16f, 0.2f, 0.85f)));
                    gameObject.AddComponent<Toggle>();
                    break;
                case "slider":
                    TryApplyAssetSprite(spec, node, EnsureImage(gameObject, new Color(0.1f, 0.16f, 0.2f, 0.85f)));
                    gameObject.AddComponent<Slider>();
                    break;
                case "group":
                    break;
                case "panel":
                    TryApplyAssetSprite(spec, node, EnsureImage(gameObject, new Color(0.06f, 0.1f, 0.16f, 0.7f)));
                    break;
                case "icon":
                    TryApplyAssetSprite(spec, node, EnsureImage(gameObject, new Color(0.35f, 0.85f, 1f, 0.9f)));
                    break;
                case "image":
                    TryApplyAssetSprite(spec, node, EnsureImage(gameObject, new Color(1f, 1f, 1f, 0.35f)));
                    break;
                default:
                    TryApplyAssetSprite(spec, node, EnsureImage(gameObject, new Color(1f, 0.85f, 0.25f, 0.35f)));
                    break;
            }
        }

        private static Image EnsureImage(GameObject gameObject, Color color)
        {
            var image = gameObject.GetComponent<Image>();
            if (image == null)
            {
                image = gameObject.AddComponent<Image>();
            }

            image.color = color;
            image.raycastTarget = true;
            return image;
        }

        private static Color ResolveButtonFallbackColor(UnityUiBridgeSpec spec, UnityUiBridgeNode node)
        {
            return HasSourceBackground(spec) && string.IsNullOrWhiteSpace(node.assetRef)
                ? new Color(1f, 1f, 1f, 0f)
                : new Color(0.15f, 0.25f, 0.35f, 0.85f);
        }

        private static bool HasSourceBackground(UnityUiBridgeSpec spec)
        {
            if (spec.assets == null)
            {
                return false;
            }

            foreach (var asset in spec.assets)
            {
                if (asset?.type == "background")
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryApplyAssetSprite(UnityUiBridgeSpec spec, UnityUiBridgeNode node, Image image)
        {
            var asset = spec.FindAsset(node.assetRef);
            if (asset == null || string.IsNullOrWhiteSpace(asset.uri))
            {
                return false;
            }

            var assetPath = ResolveAssetPath(asset.uri);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return false;
            }

            EnsureSpriteImportSettings(assetPath);
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                return false;
            }

            image.sprite = sprite;
            image.type = asset.nineSlice != null ? Image.Type.Sliced : Image.Type.Simple;
            image.color = Color.white;
            image.preserveAspect = false;
            return true;
        }

        private static string ResolveAssetPath(string uri)
        {
            var normalized = uri.Replace('\\', '/');
            if (normalized.StartsWith("Assets/"))
            {
                return normalized;
            }

            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..")).Replace('\\', '/');
            if (!Path.IsPathRooted(normalized))
            {
                var candidate = Path.GetFullPath(Path.Combine(projectRoot, normalized)).Replace('\\', '/');
                return candidate.StartsWith(projectRoot) ? candidate.Substring(projectRoot.Length + 1) : null;
            }

            var fullPath = Path.GetFullPath(normalized).Replace('\\', '/');
            return fullPath.StartsWith(projectRoot) ? fullPath.Substring(projectRoot.Length + 1) : null;
        }

        private static void EnsureSpriteImportSettings(string assetPath)
        {
            var textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (textureImporter == null)
            {
                return;
            }

            var changed = false;
            if (textureImporter.textureType != TextureImporterType.Sprite)
            {
                textureImporter.textureType = TextureImporterType.Sprite;
                changed = true;
            }

            if (textureImporter.spriteImportMode != SpriteImportMode.Single)
            {
                textureImporter.spriteImportMode = SpriteImportMode.Single;
                changed = true;
            }

            if (!textureImporter.alphaIsTransparency)
            {
                textureImporter.alphaIsTransparency = true;
                changed = true;
            }

            if (changed)
            {
                textureImporter.SaveAndReimport();
            }
        }

        private static void AddLayoutGroup(GameObject gameObject, UnityUiBridgeNode node)
        {
            var layout = node.layout;
            if (layout == null)
            {
                return;
            }

            HorizontalOrVerticalLayoutGroup group = null;
            if (layout.mode == "vertical")
            {
                group = gameObject.AddComponent<VerticalLayoutGroup>();
            }
            else if (layout.mode == "horizontal")
            {
                group = gameObject.AddComponent<HorizontalLayoutGroup>();
            }

            if (group == null)
            {
                return;
            }

            group.spacing = layout.spacing;
            group.childControlWidth = false;
            group.childControlHeight = false;
            group.childForceExpandWidth = false;
            group.childForceExpandHeight = false;

            if (layout.padding != null)
            {
                group.padding = new RectOffset(
                    Mathf.RoundToInt(layout.padding.left),
                    Mathf.RoundToInt(layout.padding.right),
                    Mathf.RoundToInt(layout.padding.top),
                    Mathf.RoundToInt(layout.padding.bottom));
            }
        }

        private static void ApplyRectTransform(
            RectTransform rectTransform,
            UnityUiBridgeNode node,
            UnityUiBridgeRect parentRect)
        {
            var rect = node.rect;
            rectTransform.anchorMin = node.anchors?.min != null
                ? new Vector2(node.anchors.min.x, node.anchors.min.y)
                : new Vector2(0f, 1f);
            rectTransform.anchorMax = node.anchors?.max != null
                ? new Vector2(node.anchors.max.x, node.anchors.max.y)
                : new Vector2(0f, 1f);
            rectTransform.pivot = node.pivot != null
                ? new Vector2(node.pivot.x, node.pivot.y)
                : new Vector2(0f, 1f);

            rectTransform.sizeDelta = new Vector2(rect.width, rect.height);

            var parentX = parentRect?.x ?? 0f;
            var parentY = parentRect?.y ?? 0f;
            rectTransform.anchoredPosition = new Vector2(
                rect.x - parentX,
                -(rect.y - parentY));
        }

        private static Vector2 ResolveReferenceResolution(UnityUiBridgeSpec spec, UnityUiBridgeNode canvasNode)
        {
            if (spec.document?.referenceResolution != null)
            {
                return new Vector2(
                    spec.document.referenceResolution.width,
                    spec.document.referenceResolution.height);
            }

            if (canvasNode?.rect != null)
            {
                return new Vector2(canvasNode.rect.width, canvasNode.rect.height);
            }

            return new Vector2(1920, 1080);
        }

        private static Vector2 ResolveSourceCanvasSize(UnityUiBridgeRect sourceCanvasRect, Vector2 fallback)
        {
            if (sourceCanvasRect != null && sourceCanvasRect.width > 0f && sourceCanvasRect.height > 0f)
            {
                return new Vector2(sourceCanvasRect.width, sourceCanvasRect.height);
            }

            return fallback;
        }

        private static Vector2 ResolveRootSize(GameObject root, Vector2 fallback)
        {
            var rectTransform = root.GetComponent<RectTransform>();
            if (rectTransform == null)
            {
                return fallback;
            }

            var size = rectTransform.sizeDelta;
            return size.x > 0f && size.y > 0f ? size : fallback;
        }

        private static Vector2 ResolveTargetCanvasSize(Canvas targetCanvas, Vector2 fallback)
        {
            var rectTransform = targetCanvas.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                var rectSize = rectTransform.rect.size;
                if (rectSize.x > 0f && rectSize.y > 0f)
                {
                    return rectSize;
                }

                if (rectTransform.sizeDelta.x > 0f && rectTransform.sizeDelta.y > 0f)
                {
                    return rectTransform.sizeDelta;
                }
            }

            var scaler = targetCanvas.GetComponent<CanvasScaler>();
            if (scaler != null && scaler.referenceResolution.x > 0f && scaler.referenceResolution.y > 0f)
            {
                return scaler.referenceResolution;
            }

            return fallback;
        }

        private static int ResolveFontSize(UnityUiBridgeSpec spec, UnityUiBridgeNode node, int fallback)
        {
            var style = spec.FindStyle(node.styleRef);
            return style?.typography != null && style.typography.fontSize > 0
                ? Mathf.RoundToInt(style.typography.fontSize)
                : fallback;
        }

        private static TextAnchor ResolveAlignment(UnityUiBridgeSpec spec, UnityUiBridgeNode node)
        {
            var alignment = spec.FindStyle(node.styleRef)?.typography?.alignment;
            return alignment switch
            {
                "left" => TextAnchor.MiddleLeft,
                "right" => TextAnchor.MiddleRight,
                _ => TextAnchor.MiddleCenter
            };
        }

        private static Font ResolveBuiltInFont()
        {
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        private static void EnsureEventSystem()
        {
            var existing = Object.FindFirstObjectByType<EventSystem>();
            if (existing != null)
            {
                EnsureCompatibleInputModule(existing.gameObject);
                return;
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            EnsureCompatibleInputModule(eventSystem);
            Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
        }

        private static void EnsureCompatibleInputModule(GameObject eventSystem)
        {
            var inputSystemUiModuleType = Type.GetType(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
            if (inputSystemUiModuleType != null)
            {
                var standaloneInputModule = eventSystem.GetComponent<StandaloneInputModule>();
                if (standaloneInputModule != null)
                {
                    Object.DestroyImmediate(standaloneInputModule);
                }

                if (eventSystem.GetComponent(inputSystemUiModuleType) == null)
                {
                    eventSystem.AddComponent(inputSystemUiModuleType);
                }

                return;
            }

            if (eventSystem.GetComponent<StandaloneInputModule>() == null)
            {
                eventSystem.AddComponent<StandaloneInputModule>();
            }
        }

        private static bool IsUnityUiBridgeImportCanvas(Canvas canvas)
        {
            if (canvas == null)
            {
                return false;
            }

            var current = canvas.transform;
            while (current != null)
            {
                var objectName = current.name;
                if (objectName.StartsWith("Unity UI Bridge", StringComparison.OrdinalIgnoreCase)
                    || objectName.StartsWith("Imported ", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private readonly struct UnityUiBridgeSourceFrameLayout
        {
            private UnityUiBridgeSourceFrameLayout(
                float scaleX,
                float scaleY,
                float offsetX,
                float offsetY)
            {
                ScaleX = scaleX;
                ScaleY = scaleY;
                OffsetX = offsetX;
                OffsetY = offsetY;
            }

            public float ScaleX { get; }
            public float ScaleY { get; }
            public float OffsetX { get; }
            public float OffsetY { get; }

            public static UnityUiBridgeSourceFrameLayout Create(
                Vector2 sourceResolution,
                Vector2 targetResolution,
                bool preserveAspectRatio,
                bool fillTargetCanvas)
            {
                var sourceWidth = sourceResolution.x;
                var sourceHeight = sourceResolution.y;
                var scaleX = targetResolution.x / Mathf.Max(sourceWidth, 1f);
                var scaleY = targetResolution.y / Mathf.Max(sourceHeight, 1f);

                if (!preserveAspectRatio)
                {
                    return new UnityUiBridgeSourceFrameLayout(scaleX, scaleY, 0f, 0f);
                }

                var uniformScale = fillTargetCanvas ? Mathf.Max(scaleX, scaleY) : Mathf.Min(scaleX, scaleY);
                var fittedWidth = sourceWidth * uniformScale;
                var fittedHeight = sourceHeight * uniformScale;
                return new UnityUiBridgeSourceFrameLayout(
                    uniformScale,
                    uniformScale,
                    (targetResolution.x - fittedWidth) * 0.5f,
                    (targetResolution.y - fittedHeight) * 0.5f);
            }
        }
    }
}
