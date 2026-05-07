using UnityEditor;
using UnityEngine;
using UnityUIBridge.Editor.Import;
using UnityUIBridge.Runtime.Spec;

namespace UnityUIBridge.Editor
{
    public sealed class UnityUiBridgeImporterWindow : EditorWindow
    {
        private string _specPath = "Assets/UnityUIBridge/Samples/Specs/main-menu.valid.json";
        private string _rootName = "Unity UI Bridge Import";
        private bool _createEventSystem = true;
        private bool _applyLayoutGroups = false;
        private Canvas _targetCanvas;
        private bool _fitToTargetCanvas = true;
        private bool _preserveAspectRatio = true;

        [MenuItem("Tools/Unity UI Bridge/Importer")]
        public static void Open()
        {
            GetWindow<UnityUiBridgeImporterWindow>("Unity UI Bridge");
        }

        [MenuItem("Tools/Unity UI Bridge/Import Main Menu Sample")]
        public static void ImportMainMenuSample()
        {
            ImportSpec(
                "Assets/UnityUIBridge/Samples/Specs/main-menu.valid.json",
                "Imported Main Menu",
                true,
                false,
                UnityUiBridgeImporter.FindSceneCanvasForImport(),
                true,
                true);
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Spec Import", EditorStyles.boldLabel);
            _specPath = EditorGUILayout.TextField("Spec Path", _specPath);
            _rootName = EditorGUILayout.TextField("Root Name", _rootName);
            _targetCanvas = (Canvas)EditorGUILayout.ObjectField("Target Canvas", _targetCanvas, typeof(Canvas), true);
            _createEventSystem = EditorGUILayout.Toggle("Create EventSystem", _createEventSystem);
            _fitToTargetCanvas = EditorGUILayout.Toggle("Fit To Target Canvas", _fitToTargetCanvas);
            _preserveAspectRatio = EditorGUILayout.Toggle("Preserve Aspect Ratio", _preserveAspectRatio);
            _applyLayoutGroups = EditorGUILayout.Toggle("Apply Layout Groups", _applyLayoutGroups);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Use Scene Canvas"))
                {
                    _targetCanvas = UnityUiBridgeImporter.FindSceneCanvasForImport();
                }

                if (GUILayout.Button("Browse"))
                {
                    var selected = EditorUtility.OpenFilePanel("Select Unity UI Bridge Spec", Application.dataPath, "json");
                    if (!string.IsNullOrWhiteSpace(selected))
                    {
                        _specPath = selected;
                    }
                }

                if (GUILayout.Button("Import"))
                {
                    ImportSpec(
                        _specPath,
                        _rootName,
                        _createEventSystem,
                        _applyLayoutGroups,
                        _targetCanvas,
                        _fitToTargetCanvas,
                        _preserveAspectRatio);
                }
            }
        }

        private static void ImportSpec(
            string specPath,
            string rootName,
            bool createEventSystem = true,
            bool applyLayoutGroups = false,
            Canvas targetCanvas = null,
            bool fitToTargetCanvas = true,
            bool preserveAspectRatio = true)
        {
            var spec = UnityUiBridgeSpecParser.LoadFromFile(specPath);
            UnityUiBridgeImporter.Import(spec, new UnityUiBridgeImportOptions
            {
                RootName = rootName,
                CreateEventSystem = createEventSystem,
                ApplyLayoutGroups = applyLayoutGroups,
                TargetCanvas = targetCanvas,
                FitToTargetCanvas = fitToTargetCanvas,
                PreserveAspectRatio = preserveAspectRatio
            });
        }
    }
}
