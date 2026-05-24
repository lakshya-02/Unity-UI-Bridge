using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityUIBridge.Editor.Import;
using UnityUIBridge.Runtime.Spec;

namespace UnityUIBridge.Tests.EditMode
{
    public sealed class UnityUiBridgeImporterTests
    {
        private GameObject _root;

        [TearDown]
        public void TearDown()
        {
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
            }

            var eventSystem = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (eventSystem != null)
            {
                Object.DestroyImmediate(eventSystem.gameObject);
            }

            Selection.activeGameObject = null;
        }

        [Test]
        public void SpecParserLoadsNestedNodesAndUnknownRoles()
        {
            var spec = UnityUiBridgeSpecParser.LoadFromFile(SamplePath("main-menu.valid.json"));

            Assert.That(spec.schemaVersion, Is.EqualTo("1.0.0"));
            Assert.That(spec.nodes, Has.Length.EqualTo(1));

            var unknown = spec.FindNode("node.low-confidence-badge");
            Assert.That(unknown, Is.Not.Null);
            Assert.That(unknown.role, Is.EqualTo("unknown"));
            Assert.That(unknown.confidence, Is.EqualTo(0.38f).Within(0.001f));
        }

        [Test]
        public void ImporterBuildsCanvasButtonsAndTextFromMainMenuSpec()
        {
            var spec = UnityUiBridgeSpecParser.LoadFromFile(SamplePath("main-menu.valid.json"));

            _root = UnityUiBridgeImporter.Import(spec, new UnityUiBridgeImportOptions
            {
                RootName = "Imported Main Menu Test",
                CreateEventSystem = false
            });

            Assert.That(_root.GetComponent<Canvas>(), Is.Not.Null);
            Assert.That(_root.GetComponent<CanvasScaler>(), Is.Not.Null);
            Assert.That(_root.GetComponent<GraphicRaycaster>(), Is.Not.Null);
            Assert.That(_root.transform.localScale, Is.EqualTo(Vector3.one));

            var playButton = GameObject.Find("Play Button");
            Assert.That(playButton, Is.Not.Null);
            Assert.That(playButton.GetComponent<Button>(), Is.Not.Null);

            var playLabel = GameObject.Find("node.play-label");
            Assert.That(playLabel, Is.Not.Null);
            Assert.That(playLabel.GetComponent<Text>().text, Is.EqualTo("PLAY"));
        }

        [Test]
        public void ImporterPreservesAbsolutePixelPositionsByDefault()
        {
            var spec = UnityUiBridgeSpecParser.LoadFromFile(SamplePath("main-menu.valid.json"));

            _root = UnityUiBridgeImporter.Import(spec, new UnityUiBridgeImportOptions
            {
                RootName = "Imported Position Test",
                CreateEventSystem = false
            });

            var menuPanel = GameObject.Find("Menu Panel");
            var title = GameObject.Find("Title");
            var playButton = GameObject.Find("Play Button");

            Assert.That(menuPanel.GetComponent<VerticalLayoutGroup>(), Is.Null);
            AssertPosition(menuPanel, 560f, -180f);
            AssertPosition(title, 130f, -80f);
            AssertPosition(playButton, 100f, -260f);
        }

        [Test]
        public void ImporterFitsSpecIntoTargetCanvas()
        {
            var spec = UnityUiBridgeSpecParser.LoadFromFile(SamplePath("main-menu.valid.json"));
            var targetCanvasObject = new GameObject("Scene Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            _root = targetCanvasObject;

            var targetRect = targetCanvasObject.GetComponent<RectTransform>();
            targetRect.sizeDelta = new Vector2(1280f, 720f);
            targetCanvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1280f, 720f);

            var importedRoot = UnityUiBridgeImporter.Import(spec, new UnityUiBridgeImportOptions
            {
                RootName = "Imported Into Scene Canvas",
                CreateEventSystem = false,
                TargetCanvas = targetCanvasObject.GetComponent<Canvas>(),
                FitToTargetCanvas = true
            });

            Assert.That(importedRoot.GetComponent<Canvas>(), Is.Null);
            Assert.That(importedRoot.transform.parent, Is.EqualTo(targetCanvasObject.transform));

            var sourceFrame = importedRoot.transform.Find("Source Frame");
            Assert.That(sourceFrame, Is.Not.Null);

            var menuPanel = GameObject.Find("Menu Panel");
            AssertRectSize(importedRoot, 1280f, 720f);
            AssertSize(sourceFrame.gameObject, 1920f, 1080f);
            AssertScale(sourceFrame.gameObject, 0.666667f, 0.666667f);
            AssertPosition(sourceFrame.gameObject, 0f, 0f);
            AssertCentered(sourceFrame.gameObject);
            Assert.That(menuPanel.transform.parent, Is.EqualTo(sourceFrame));
            AssertPosition(menuPanel, 560f, -180f);
            AssertSize(menuPanel, 800f, 720f);
        }

        [Test]
        public void ImporterFillsTargetCanvasByDefaultWhenAspectDiffers()
        {
            var spec = UnityUiBridgeSpecParser.LoadFromFile(SamplePath("main-menu.valid.json"));
            var targetCanvasObject = new GameObject("Square Scene Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            _root = targetCanvasObject;

            var targetRect = targetCanvasObject.GetComponent<RectTransform>();
            targetRect.sizeDelta = new Vector2(1000f, 1000f);
            targetCanvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1000f, 1000f);

            var importedRoot = UnityUiBridgeImporter.Import(spec, new UnityUiBridgeImportOptions
            {
                RootName = "Imported Letterbox Test",
                CreateEventSystem = false,
                TargetCanvas = targetCanvasObject.GetComponent<Canvas>(),
                FitToTargetCanvas = true,
                PreserveAspectRatio = true
            });

            var sourceFrame = importedRoot.transform.Find("Source Frame");
            Assert.That(sourceFrame, Is.Not.Null);
            AssertScale(sourceFrame.gameObject, 0.925926f, 0.925926f);
            AssertPosition(sourceFrame.gameObject, 0f, 0f);
            AssertCentered(sourceFrame.gameObject);
        }

        [Test]
        public void ImporterCanLetterboxSourceFrameWhenRequested()
        {
            var spec = UnityUiBridgeSpecParser.LoadFromFile(SamplePath("main-menu.valid.json"));
            var targetCanvasObject = new GameObject("Square Letterbox Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            _root = targetCanvasObject;

            var targetRect = targetCanvasObject.GetComponent<RectTransform>();
            targetRect.sizeDelta = new Vector2(1000f, 1000f);
            targetCanvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1000f, 1000f);

            var importedRoot = UnityUiBridgeImporter.Import(spec, new UnityUiBridgeImportOptions
            {
                RootName = "Imported Letterbox Test",
                CreateEventSystem = false,
                TargetCanvas = targetCanvasObject.GetComponent<Canvas>(),
                FitToTargetCanvas = true,
                PreserveAspectRatio = true,
                FillTargetCanvas = false
            });

            var sourceFrame = importedRoot.transform.Find("Source Frame");
            Assert.That(sourceFrame, Is.Not.Null);
            AssertScale(sourceFrame.gameObject, 0.520833f, 0.520833f);
            AssertPosition(sourceFrame.gameObject, 0f, 0f);
            AssertCentered(sourceFrame.gameObject);
        }

        [Test]
        public void ImporterStretchesRootInsideTargetCanvas()
        {
            var spec = UnityUiBridgeSpecParser.LoadFromFile(SamplePath("main-menu.valid.json"));
            var targetCanvasObject = new GameObject("Stretch Scene Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            _root = targetCanvasObject;

            var targetRect = targetCanvasObject.GetComponent<RectTransform>();
            targetRect.sizeDelta = new Vector2(1280f, 720f);
            targetCanvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1280f, 720f);

            var importedRoot = UnityUiBridgeImporter.Import(spec, new UnityUiBridgeImportOptions
            {
                RootName = "Imported Stretch Test",
                CreateEventSystem = false,
                TargetCanvas = targetCanvasObject.GetComponent<Canvas>(),
                FitToTargetCanvas = true
            });

            var rectTransform = importedRoot.GetComponent<RectTransform>();
            Assert.That(rectTransform.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(rectTransform.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(rectTransform.offsetMin, Is.EqualTo(Vector2.zero));
            Assert.That(rectTransform.offsetMax, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void ImporterCentersSourceFrameInsideCreatedCanvas()
        {
            var spec = UnityUiBridgeSpecParser.LoadFromFile(SamplePath("main-menu.valid.json"));

            _root = UnityUiBridgeImporter.Import(spec, new UnityUiBridgeImportOptions
            {
                RootName = "Imported Centered Canvas Test",
                CreateEventSystem = false,
                TargetCanvas = null,
                FitToTargetCanvas = true
            });

            var sourceFrame = _root.transform.Find("Source Frame");
            Assert.That(sourceFrame, Is.Not.Null);
            AssertCentered(sourceFrame.gameObject);
            AssertPosition(sourceFrame.gameObject, 0f, 0f);
        }

        [Test]
        public void ImporterIgnoresStaleGeneratedTargetCanvas()
        {
            var spec = UnityUiBridgeSpecParser.LoadFromFile(SamplePath("main-menu.valid.json"));
            var staleImportCanvasObject = new GameObject("Unity UI Bridge Import", typeof(RectTransform), typeof(Canvas));
            var mainCanvasObject = new GameObject("Main Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            _root = mainCanvasObject;

            staleImportCanvasObject.GetComponent<RectTransform>().sizeDelta = new Vector2(100f, 100f);
            mainCanvasObject.GetComponent<RectTransform>().sizeDelta = new Vector2(1280f, 720f);
            mainCanvasObject.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1280f, 720f);

            var importedRoot = UnityUiBridgeImporter.Import(spec, new UnityUiBridgeImportOptions
            {
                RootName = "Imported Stale Target Test",
                CreateEventSystem = false,
                TargetCanvas = staleImportCanvasObject.GetComponent<Canvas>(),
                FitToTargetCanvas = true
            });

            Assert.That(importedRoot.transform.parent, Is.EqualTo(mainCanvasObject.transform));
            AssertRectSize(importedRoot, 1280f, 720f);
            Object.DestroyImmediate(staleImportCanvasObject);
        }

        [Test]
        public void FindSceneCanvasForImportPrefersUserCanvasOverPreviousImports()
        {
            _root = new GameObject("Canvas Test Root");
            var importCanvasObject = new GameObject("Unity UI Bridge Import", typeof(RectTransform), typeof(Canvas));
            var mainCanvasObject = new GameObject("Main Canvas", typeof(RectTransform), typeof(Canvas));
            importCanvasObject.transform.SetParent(_root.transform);
            mainCanvasObject.transform.SetParent(_root.transform);
            Selection.activeGameObject = null;

            var canvas = UnityUiBridgeImporter.FindSceneCanvasForImport();

            Assert.That(canvas, Is.EqualTo(mainCanvasObject.GetComponent<Canvas>()));
        }

        [Test]
        public void ClearGeneratedImportsRemovesStaleImportRootsOnly()
        {
            _root = new GameObject("Canvas Test Root");
            var staleImportCanvasObject = new GameObject("Unity UI Bridge Import", typeof(RectTransform), typeof(Canvas));
            var mainCanvasObject = new GameObject("Main Canvas", typeof(RectTransform), typeof(Canvas));
            mainCanvasObject.transform.SetParent(_root.transform);
            staleImportCanvasObject.transform.localScale = Vector3.zero;

            var deletedCount = UnityUiBridgeImporter.ClearGeneratedImports();

            Assert.That(deletedCount, Is.EqualTo(1));
            Assert.That(staleImportCanvasObject == null, Is.True);
            Assert.That(mainCanvasObject, Is.Not.Null);
        }

        [Test]
        public void ImporterUsesInputSystemUiModuleWhenPackageIsAvailable()
        {
            var spec = UnityUiBridgeSpecParser.LoadFromFile(SamplePath("main-menu.valid.json"));

            _root = UnityUiBridgeImporter.Import(spec, new UnityUiBridgeImportOptions
            {
                RootName = "Imported EventSystem Test",
                CreateEventSystem = true
            });

            var eventSystem = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            Assert.That(eventSystem, Is.Not.Null);
            Assert.That(eventSystem.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>(), Is.Null);
            Assert.That(eventSystem.GetComponent("InputSystemUIInputModule"), Is.Not.Null);
        }

        [Test]
        public void ImporterAssignsSpriteAssetsToVisualNodes()
        {
            var assetPath = CreateTemporarySpriteAsset();
            var json = @"{
  ""schemaVersion"": ""1.0.0"",
  ""document"": {
    ""id"": ""doc.sprite-import"",
    ""title"": ""Sprite Import"",
    ""source"": { ""type"": ""screenshot"", ""uri"": ""sprite-import.png"" },
    ""referenceResolution"": { ""width"": 64, ""height"": 64 },
    ""coordinateSystem"": { ""origin"": ""top-left"", ""unit"": ""pixel"", ""yAxis"": ""down"" },
    ""target"": { ""engine"": ""Unity"", ""uiSystem"": ""uGUI"", ""canvasMode"": ""screen-space-overlay"" }
  },
  ""assets"": [
    {
      ""id"": ""asset.panel"",
      ""type"": ""panel"",
      ""uri"": """ + assetPath.Replace("\\", "/") + @""",
      ""rect"": { ""x"": 0, ""y"": 0, ""width"": 64, ""height"": 64 },
      ""sourceNodeId"": ""node.panel""
    }
  ],
  ""styles"": [],
  ""nodes"": [
    {
      ""id"": ""node.canvas"",
      ""role"": ""canvas"",
      ""rect"": { ""x"": 0, ""y"": 0, ""width"": 64, ""height"": 64 },
      ""children"": [
        {
          ""id"": ""node.panel"",
          ""role"": ""panel"",
          ""name"": ""Sprite Panel"",
          ""rect"": { ""x"": 4, ""y"": 6, ""width"": 32, ""height"": 24 },
          ""assetRef"": ""asset.panel""
        }
      ]
    }
  ],
  ""interactions"": [],
  ""extensions"": {}
}";
            var spec = UnityUiBridgeSpecParser.FromJson(json);

            _root = UnityUiBridgeImporter.Import(spec, new UnityUiBridgeImportOptions
            {
                RootName = "Imported Sprite Test",
                CreateEventSystem = false
            });

            var panel = GameObject.Find("Sprite Panel");
            var image = panel.GetComponent<Image>();

            Assert.That(image.sprite, Is.Not.Null);
            Assert.That(image.color, Is.EqualTo(Color.white));
            Assert.That(AssetImporter.GetAtPath(assetPath), Is.TypeOf<TextureImporter>());
            Assert.That(((TextureImporter)AssetImporter.GetAtPath(assetPath)).textureType, Is.EqualTo(TextureImporterType.Sprite));
        }

        [Test]
        public void ImporterUsesTransparentHotspotsWhenBackgroundCoversVisuals()
        {
            var assetPath = CreateTemporarySpriteAsset();
            var json = @"{
  ""schemaVersion"": ""1.0.0"",
  ""document"": {
    ""id"": ""doc.hotspot-import"",
    ""title"": ""Hotspot Import"",
    ""source"": { ""type"": ""screenshot"", ""uri"": ""hotspot-import.png"" },
    ""referenceResolution"": { ""width"": 64, ""height"": 64 },
    ""coordinateSystem"": { ""origin"": ""top-left"", ""unit"": ""pixel"", ""yAxis"": ""down"" },
    ""target"": { ""engine"": ""Unity"", ""uiSystem"": ""uGUI"", ""canvasMode"": ""screen-space-overlay"" }
  },
  ""assets"": [
    {
      ""id"": ""asset.background"",
      ""type"": ""background"",
      ""uri"": """ + assetPath.Replace("\\", "/") + @""",
      ""rect"": { ""x"": 0, ""y"": 0, ""width"": 64, ""height"": 64 },
      ""sourceNodeId"": ""node.background""
    }
  ],
  ""styles"": [],
  ""nodes"": [
    {
      ""id"": ""node.canvas"",
      ""role"": ""canvas"",
      ""rect"": { ""x"": 0, ""y"": 0, ""width"": 64, ""height"": 64 },
      ""children"": [
        {
          ""id"": ""node.background"",
          ""role"": ""image"",
          ""name"": ""Source Background"",
          ""rect"": { ""x"": 0, ""y"": 0, ""width"": 64, ""height"": 64 },
          ""assetRef"": ""asset.background""
        },
        {
          ""id"": ""node.play-button"",
          ""role"": ""button"",
          ""name"": ""Play Hotspot"",
          ""rect"": { ""x"": 20, ""y"": 20, ""width"": 24, ""height"": 24 },
          ""interactionRef"": ""interaction.play""
        }
      ]
    }
  ],
  ""interactions"": [
    { ""id"": ""interaction.play"", ""nodeId"": ""node.play-button"", ""type"": ""button"" }
  ],
  ""extensions"": {}
}";
            var spec = UnityUiBridgeSpecParser.FromJson(json);

            _root = UnityUiBridgeImporter.Import(spec, new UnityUiBridgeImportOptions
            {
                RootName = "Imported Hotspot Test",
                CreateEventSystem = false
            });

            var background = GameObject.Find("Source Background");
            var hotspot = GameObject.Find("Play Hotspot");
            var backgroundRect = background.GetComponent<RectTransform>();

            Assert.That(background.GetComponent<Image>().sprite, Is.Not.Null);
            Assert.That(backgroundRect.anchorMin, Is.EqualTo(Vector2.zero));
            Assert.That(backgroundRect.anchorMax, Is.EqualTo(Vector2.one));
            Assert.That(backgroundRect.offsetMin, Is.EqualTo(Vector2.zero));
            Assert.That(backgroundRect.offsetMax, Is.EqualTo(Vector2.zero));
            Assert.That(hotspot.GetComponent<Button>(), Is.Not.Null);
            Assert.That(hotspot.GetComponent<Image>().sprite, Is.Null);
            Assert.That(hotspot.GetComponent<Image>().color.a, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void ImporterCreatesDebugOverlayInsideSourceFrame()
        {
            var spec = UnityUiBridgeSpecParser.LoadFromFile(SamplePath("main-menu.valid.json"));

            _root = UnityUiBridgeImporter.Import(spec, new UnityUiBridgeImportOptions
            {
                RootName = "Imported Debug Overlay Test",
                CreateEventSystem = false,
                CreateDebugOverlay = true
            });

            var sourceFrame = _root.transform.Find("Source Frame");
            Assert.That(sourceFrame, Is.Not.Null);

            var debugOverlay = sourceFrame.Find("Debug Overlay");
            Assert.That(debugOverlay, Is.Not.Null);

            var playButtonDebug = debugOverlay.Find("Debug node.play-button");
            Assert.That(playButtonDebug, Is.Not.Null);
            AssertPosition(playButtonDebug.gameObject, 660f, -440f);
            AssertSize(playButtonDebug.gameObject, 600f, 88f);

            var label = playButtonDebug.Find("Label");
            Assert.That(label, Is.Not.Null);
            Assert.That(label.GetComponent<Text>().text, Is.EqualTo("node.play-button (button)"));
            Assert.That(playButtonDebug.GetComponent<Image>().raycastTarget, Is.False);
        }

        private static string SamplePath(string fileName)
        {
            return Path.Combine(Application.dataPath, "UnityUIBridge", "Samples", "Specs", fileName);
        }

        private static string CreateTemporarySpriteAsset()
        {
            const string assetPath = "Assets/UnityUIBridge/Generated/TestAssets/importer-sprite-test.png";
            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));

            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false);
            for (var y = 0; y < texture.height; y++)
            {
                for (var x = 0; x < texture.width; x++)
                {
                    texture.SetPixel(x, y, new Color(0.1f, 0.8f, 1f, 1f));
                }
            }

            texture.Apply();
            File.WriteAllBytes(assetPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            return assetPath;
        }

        private static void AssertPosition(GameObject gameObject, float expectedX, float expectedY)
        {
            var rectTransform = gameObject.GetComponent<RectTransform>();
            Assert.That(rectTransform.anchoredPosition.x, Is.EqualTo(expectedX).Within(0.001f));
            Assert.That(rectTransform.anchoredPosition.y, Is.EqualTo(expectedY).Within(0.001f));
        }

        private static void AssertSize(GameObject gameObject, float expectedWidth, float expectedHeight)
        {
            var rectTransform = gameObject.GetComponent<RectTransform>();
            Assert.That(rectTransform.sizeDelta.x, Is.EqualTo(expectedWidth).Within(0.001f));
            Assert.That(rectTransform.sizeDelta.y, Is.EqualTo(expectedHeight).Within(0.001f));
        }

        private static void AssertRectSize(GameObject gameObject, float expectedWidth, float expectedHeight)
        {
            var rectTransform = gameObject.GetComponent<RectTransform>();
            Assert.That(rectTransform.rect.width, Is.EqualTo(expectedWidth).Within(0.001f));
            Assert.That(rectTransform.rect.height, Is.EqualTo(expectedHeight).Within(0.001f));
        }

        private static void AssertScale(GameObject gameObject, float expectedX, float expectedY)
        {
            Assert.That(gameObject.transform.localScale.x, Is.EqualTo(expectedX).Within(0.001f));
            Assert.That(gameObject.transform.localScale.y, Is.EqualTo(expectedY).Within(0.001f));
        }

        private static void AssertCentered(GameObject gameObject)
        {
            var rectTransform = gameObject.GetComponent<RectTransform>();
            Assert.That(rectTransform.anchorMin, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(rectTransform.anchorMax, Is.EqualTo(new Vector2(0.5f, 0.5f)));
            Assert.That(rectTransform.pivot, Is.EqualTo(new Vector2(0.5f, 0.5f)));
        }
    }
}
