using UnityEditor;
using UnityEngine;
using UnityUIBridge.Editor.Import;
using UnityUIBridge.Runtime.Spec;
using System.Diagnostics;
using System.IO;

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
        private string _imagePath = "Assets/UnityUIBridge/Generated/SmokeTests/synthetic-ui.png";
        private string _generatedSpecPath = "Assets/UnityUIBridge/Generated/Specs/generated-ui.json";
        private bool _runOcr = true;
        private bool _importAfterGenerate = true;

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

            EditorGUILayout.Space(12);
            EditorGUILayout.LabelField("Image To Spec", EditorStyles.boldLabel);
            _imagePath = EditorGUILayout.TextField("Image Path", _imagePath);
            _generatedSpecPath = EditorGUILayout.TextField("Output Spec", _generatedSpecPath);
            _runOcr = EditorGUILayout.Toggle("Run OCR", _runOcr);
            _importAfterGenerate = EditorGUILayout.Toggle("Import After Generate", _importAfterGenerate);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Browse Image"))
                {
                    var selected = EditorUtility.OpenFilePanel("Select UI Image", Application.dataPath, "png,jpg,jpeg");
                    if (!string.IsNullOrWhiteSpace(selected))
                    {
                        _imagePath = selected;
                    }
                }

                if (GUILayout.Button("Generate Spec"))
                {
                    GenerateSpecFromImage();
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

        private void GenerateSpecFromImage()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var scriptPath = Path.Combine(projectRoot, "Assets", "UnityUIBridge", "Tools", "Python", "image_to_ui_spec.py");
            var imagePath = ResolveProjectRelativePath(projectRoot, _imagePath);
            var outputPath = ResolveProjectRelativePath(projectRoot, _generatedSpecPath);

            Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

            var arguments =
                Quote(scriptPath) +
                " " + Quote(imagePath) +
                " --output " + Quote(outputPath) +
                " --title " + Quote(Path.GetFileNameWithoutExtension(imagePath)) +
                " --project-root " + Quote(projectRoot);

            if (!_runOcr)
            {
                arguments += " --no-ocr";
            }

            var result = RunPython(projectRoot, arguments);
            if (result.ExitCode != 0)
            {
                UnityEngine.Debug.LogError(result.Output);
                EditorUtility.DisplayDialog("Unity UI Bridge", "Image-to-spec generation failed. Check the Console for details.", "OK");
                return;
            }

            UnityEngine.Debug.Log(result.Output);
            AssetDatabase.Refresh();
            _specPath = outputPath;

            if (_importAfterGenerate)
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

        private static ProcessResult RunPython(string workingDirectory, string arguments)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ResolvePythonExecutable(),
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                return new ProcessResult(1, "Could not start Python process.");
            }

            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit();
            return new ProcessResult(process.ExitCode, output);
        }

        private static string ResolvePythonExecutable()
        {
            var configured = System.Environment.GetEnvironmentVariable("UNITY_UI_BRIDGE_PYTHON");
            return string.IsNullOrWhiteSpace(configured) ? "python" : configured;
        }

        private static string ResolveProjectRelativePath(string projectRoot, string path)
        {
            return Path.IsPathRooted(path) ? path : Path.GetFullPath(Path.Combine(projectRoot, path));
        }

        private static string Quote(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private readonly struct ProcessResult
        {
            public ProcessResult(int exitCode, string output)
            {
                ExitCode = exitCode;
                Output = output;
            }

            public int ExitCode { get; }
            public string Output { get; }
        }
    }
}
