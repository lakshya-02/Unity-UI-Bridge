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

            var menuPanel = GameObject.Find("Menu Panel");
            AssertSize(importedRoot, 1280f, 720f);
            AssertPosition(menuPanel, 373.333f, -120f);
            AssertSize(menuPanel, 533.333f, 480f);
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
    }
}
