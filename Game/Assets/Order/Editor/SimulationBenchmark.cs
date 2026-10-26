using System;
using System.Globalization;
using System.IO;
using Order.Presentation;
using UnityEditor;
using UnityEngine;

namespace Order.Editor
{
    // The simulation benchmark (SimulationBench) from the editor: the report goes to the console and to docs/performance
    // as JSON. Editor numbers: compare runs with each other; a Player build of the stress lab gives the game's own
    // (StressLabBuild).
    public static class SimulationBenchmark
    {
        [MenuItem("Order/Benchmark Simulation")]
        public static void RunAll() => UnityEngine.Debug.Log(Run(SimulationBench.Sizes, SimulationBench.Steps, true));

        // One line per size, and the JSON report when asked.
        public static string Run(int[] sizes, int steps, bool write = false)
        {
            var results = SimulationBench.Run(sizes, steps);
            var text = SimulationBench.Text(results);
            if (!write) return text;
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "performance", DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "-simulation-stress.json"));
            File.WriteAllText(path, SimulationBench.Json(results, steps));
            return text + "Report: " + path;
        }
    }
}
