// Menu: Tanker Jam > Tests > Run EditMode. Runs the project's EditMode tests and writes a plain-text
// summary to Temp/TankerJamTests.txt (also logged to the console), so results can be read without the
// Test Runner window (CI, MCP automation).
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace TankerJam.EditorTools
{
    public static class TestRunnerMenu
    {
        public const string ResultPath = "Temp/TankerJamTests.txt";
        const string Assembly = "TankerJam.Tests.EditMode";

        [MenuItem("Tanker Jam/Tests/Run EditMode")]
        public static void RunEditMode()
        {
            if (File.Exists(ResultPath)) File.Delete(ResultPath);
            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(new Callbacks());
            api.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, assemblyNames = new[] { Assembly } }));
        }

        sealed class Callbacks : ICallbacks
        {
            public void RunStarted(ITestAdaptor tests) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }

            public void RunFinished(ITestResultAdaptor result)
            {
                var sb = new StringBuilder();
                sb.AppendLine($"Tanker Jam EditMode: passed {result.PassCount}, failed {result.FailCount}, skipped {result.SkipCount}");
                Append(result, sb);
                File.WriteAllText(ResultPath, sb.ToString());
                if (result.FailCount > 0) Debug.LogError(sb.ToString());
                else Debug.Log(sb.ToString());
            }

            static void Append(ITestResultAdaptor r, StringBuilder sb)
            {
                if (r.HasChildren)
                {
                    foreach (var c in r.Children) Append(c, sb);
                    return;
                }
                sb.Append(r.TestStatus).Append("  ").Append(r.FullName);
                if (!string.IsNullOrEmpty(r.Message)) sb.Append("  — ").Append(r.Message.Trim());
                sb.AppendLine();
            }
        }
    }
}
