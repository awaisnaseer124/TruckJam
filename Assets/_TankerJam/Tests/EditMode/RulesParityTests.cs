using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using TankerJam.Core;
using UnityEngine;

namespace TankerJam.Tests
{
    /// <summary>
    /// Replays tap sequences recorded from the Python solver (build_curve.py / make_traces.py) and checks that
    /// GameRules produces the same units in the same order. A &lt;name&gt;.traces.json file pairs with
    /// &lt;name&gt;.json next to it, or with Data/Levels/&lt;name&gt;.json.
    /// </summary>
    public class RulesParityTests
    {
        static string Root => Path.Combine(Application.dataPath, "_TankerJam");

        static string[] TraceFiles() =>
            Directory.GetFiles(Path.Combine(Root, "Tests/EditMode/Fixtures"), "*.traces.json", SearchOption.AllDirectories);

        [TestCaseSource(nameof(TraceFiles))]
        public void UnitsMatchPythonSolver(string tracePath)
        {
            string levelName = Path.GetFileName(tracePath).Replace(".traces.json", ".json");
            string levelPath = Path.Combine(Path.GetDirectoryName(tracePath), levelName);
            if (!File.Exists(levelPath)) levelPath = Path.Combine(Root, "Data/Levels", levelName);
            Assert.IsTrue(File.Exists(levelPath), $"Missing level file for trace: {levelName}");

            string levelJson = File.ReadAllText(levelPath);
            var traces = (List<object>)MiniJson.Parse(File.ReadAllText(tracePath));
            Assert.IsNotEmpty(traces);

            for (int k = 0; k < traces.Count; k++)
            {
                var trace = (Dictionary<string, object>)traces[k];
                var rules = new GameRules(LevelJson.Parse(levelJson));
                var actual = new List<string>();
                foreach (object tap in (List<object>)trace["taps"])
                {
                    int id = System.Convert.ToInt32(tap);
                    Assert.IsTrue(rules.CheckExit(id).Clear, $"Trace {k}: truck {id} should be able to exit.");
                    foreach (var u in rules.Assign(id)) actual.Add(u.ToString());
                }

                var expected = new List<string>();
                foreach (object u in (List<object>)trace["units"]) expected.Add((string)u);
                CollectionAssert.AreEqual(expected, actual, $"Trace {k}: unit order differs from Python.");
            }
        }
    }
}
