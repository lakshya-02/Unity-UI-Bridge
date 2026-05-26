using System;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityUIBridge.Runtime.Spec;

namespace UnityUIBridge.Tests.EditMode
{
    public sealed class SpecFixtureValidationTests
    {
        [Test]
        public void PythonValidatorAcceptsValidFixturesAndRejectsInvalidFixtures()
        {
            var projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var validatorPath = Path.Combine(projectRoot, "Assets", "UnityUIBridge", "Tools", "Python", "validate_specs.py");

            Assert.That(File.Exists(validatorPath), Is.True, "Expected the shared Python validator to exist.");

            var result = RunPython(projectRoot, validatorPath);

            Assert.That(result.ExitCode, Is.EqualTo(0), result.Output);
            StringAssert.Contains("valid specs passed", result.Output);
            StringAssert.Contains("expected invalid specs failed", result.Output);
        }

        [Test]
        public void UnityParserKeepsFixtureHierarchyChildren()
        {
            var validFixtureDir = Path.Combine(Application.dataPath, "UnityUIBridge", "Samples", "Specs");
            var validFixturePaths = Directory.GetFiles(validFixtureDir, "*.valid.json");

            Assert.That(validFixturePaths, Is.Not.Empty);

            foreach (var fixturePath in validFixturePaths)
            {
                var spec = UnityUiBridgeSpecParser.LoadFromFile(fixturePath);

                Assert.That(spec.nodes, Is.Not.Empty, Path.GetFileName(fixturePath));
                Assert.That(spec.nodes[0].children, Is.Not.Empty, Path.GetFileName(fixturePath));
            }
        }

        private static ProcessResult RunPython(string projectRoot, string validatorPath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ResolvePythonExecutable(),
                Arguments = $"\"{validatorPath}\" --all --project-root \"{projectRoot}\"",
                WorkingDirectory = projectRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                throw new InvalidOperationException("Could not start Python validator process.");
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return new ProcessResult(process.ExitCode, output + error);
        }

        private static string ResolvePythonExecutable()
        {
            var configured = Environment.GetEnvironmentVariable("UNITY_UI_BRIDGE_PYTHON");
            return string.IsNullOrWhiteSpace(configured) ? "python" : configured;
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
