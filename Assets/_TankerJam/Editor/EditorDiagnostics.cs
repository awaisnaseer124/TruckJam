// Writes compile results and shader errors to plain-text files under Temp/, so automation (CI, MCP)
// can read them without scraping the console.
//   Temp/TankerJamCompile.txt  - rewritten after every script compilation
//   Temp/TankerJamShaders.txt  - menu: Tanker Jam > Diagnostics > Check Shaders
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace TankerJam.EditorTools
{
    [InitializeOnLoad]
    public static class EditorDiagnostics
    {
        const string CompilePath = "Temp/TankerJamCompile.txt";
        const string ShaderPath = "Temp/TankerJamShaders.txt";
        const string ShaderFolder = "Assets/_TankerJam/Art/Shaders";

        static readonly StringBuilder compileLog = new StringBuilder();
        static int errorCount;

        static EditorDiagnostics()
        {
            CompilationPipeline.compilationStarted -= OnStarted;
            CompilationPipeline.compilationStarted += OnStarted;
            CompilationPipeline.assemblyCompilationFinished -= OnAssembly;
            CompilationPipeline.assemblyCompilationFinished += OnAssembly;
            CompilationPipeline.compilationFinished -= OnFinished;
            CompilationPipeline.compilationFinished += OnFinished;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            EditorApplication.playModeStateChanged += OnPlayMode;
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }

        const string LogPath = "Temp/TankerJamLog.txt";

        static void OnPlayMode(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
                File.WriteAllText(LogPath, $"{System.DateTime.Now:HH:mm:ss} entered play mode\n");
        }

        /// <summary>Mirrors errors, exceptions and "Tanker Jam" logs to Temp/TankerJamLog.txt.</summary>
        static void OnLog(string message, string stack, LogType type)
        {
            bool error = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
            if (!error && !message.StartsWith("Tanker Jam")) return;
            if (message.Contains("Windows signature")) return;
            string top = error && !string.IsNullOrEmpty(stack) ? "\n    " + stack.Split('\n')[0] : "";
            File.AppendAllText(LogPath, $"{System.DateTime.Now:HH:mm:ss} [{type}] {message}{top}\n");
        }

        static void OnStarted(object _)
        {
            compileLog.Clear();
            errorCount = 0;
        }

        static void OnAssembly(string assembly, CompilerMessage[] messages)
        {
            foreach (var m in messages)
            {
                if (m.type != CompilerMessageType.Error) continue;
                errorCount++;
                compileLog.AppendLine($"{m.file}({m.line},{m.column}): {m.message}");
            }
        }

        static void OnFinished(object _)
        {
            string header = errorCount == 0 ? "OK: no compile errors" : $"FAILED: {errorCount} compile error(s)";
            File.WriteAllText(CompilePath, $"{System.DateTime.Now:HH:mm:ss} {header}\n{compileLog}");
        }

        [MenuItem("Tanker Jam/Diagnostics/Check Shaders")]
        public static void CheckShaders()
        {
            var sb = new StringBuilder();
            int errors = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Shader", new[] { ShaderFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                var messages = ShaderUtil.GetShaderMessages(shader);
                bool hasError = ShaderUtil.ShaderHasError(shader);
                if (hasError) errors++;
                sb.AppendLine($"{(hasError ? "ERROR" : "ok   ")} {shader.name} ({path}) supported={shader.isSupported}");
                foreach (var m in messages)
                    sb.AppendLine($"    [{m.severity}] line {m.line}: {m.message} {m.messageDetails}".TrimEnd());
            }
            File.WriteAllText(ShaderPath, $"{System.DateTime.Now:HH:mm:ss} shaders with errors: {errors}\n{sb}");
            Debug.Log($"Tanker Jam shader check: {errors} with errors. See {ShaderPath}");
        }
    }
}
