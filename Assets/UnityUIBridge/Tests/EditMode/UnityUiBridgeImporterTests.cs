using System.IO;
using NUnit.Framework;
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
            AssertSize(importedRoot, 1280f, 720f);
            AssertSize(sourceFrame.gameObject, 1920f, 1080f);
            AssertScale(sourceFrame.gameObject, 0.666667f, 0.666667f);
            AssertPosition(sourceFrame.gameObject, 0f, 0f);
            Assert.That(menuPanel.transform.parent, Is.EqualTo(sourceFrame));
            AssertPosition(menuPanel, 560f, -180f);
            AssertSize(menuPanel, 800f, 720f);
        }

        [Test]
        public void ImporterLetterboxesSourceFrameWhenTargetAspectDiffers()
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
            AssertScale(sourceFrame.gameObject, 0.520833f, 0.520833f);
            AssertPosition(sourceFrame.gameObject, 0f, -218.75f);
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

        private static void AssertScale(GameObject gameObject, float expectedX, float expectedY)
        {
            Assert.That(gameObject.transform.localScale.x, Is.EqualTo(expectedX).Within(0.001f));
            Assert.That(gameObject.transform.localScale.y, Is.EqualTo(expectedY).Within(0.001f));
        }
    }
}
