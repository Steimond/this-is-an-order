using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Order.Balance;
using Order.Content;
using Order.Presentation;
using Order.Simulation;
using UnityEditor;
using UnityEngine;

namespace Order.Editor
{
    // The simulation alone under load, without the view: the stress battle (StressBattle) of every size in turn, each
    // played for a number of steps with the step time and its phases measured. The report goes to the console and to
    // docs/performance as JSON. Editor numbers, not a Player build: compare runs with each other, not with budgets alone.
    public static class SimulationBenchmark
    {
        public static readonly int[] Sizes = { 500, 1000, 2000, 3000 };
        public const int Steps = 600;

        [MenuItem("Order/Benchmark Simulation")]
        public static void RunAll() => UnityEngine.Debug.Log(Run(Sizes, Steps, true));

        // One line per size, and the JSON report when asked.
        public static string Run(int[] sizes, int steps, bool write = false)
        {
            var catalog = Resources.Load<BalanceCatalog>("PrototypeBalanceCatalog");
            var results = new List<Result>();
            var text = new StringBuilder();
            foreach (int size in sizes)
            {
                var result = Play(catalog != null ? catalog.Snapshot() : null, size, steps);
                results.Add(result); text.AppendLine(result.ToString());
            }
            if (write)
            {
                var path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "docs", "performance", DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "-simulation-stress.json"));
                File.WriteAllText(path, Json(results, steps));
                text.AppendLine("Report: " + path);
            }
            return text.ToString();
        }

        public sealed class Result
        {
            public int Units, Alive, Steps;
            public double Mean, Median, P95, Max, SetupMs;
            public double[] Phases;
            public override string ToString()
            {
                var phases = new StringBuilder();
                for (int i = 0; i < Phases.Length; i++) if (Phases[i] >= .05) phases.Append($" {StepProfile.Names[i]} {Phases[i]:0.0}");
                return string.Format(CultureInfo.InvariantCulture, "{0} units: step mean {1:0.0} ms, median {2:0.0}, p95 {3:0.0}, max {4:0.0}; alive at the end {5}; setup {6:0} ms; phases (mean ms):{7}",
                    Units, Mean, Median, P95, Max, Alive, SetupMs, phases);
            }
        }

        static Result Play(MatchBalance balance, int units, int steps)
        {
            var clock = Stopwatch.StartNew();
            var match = StressBattle.Create(balance, units);
            double setup = clock.Elapsed.TotalMilliseconds;
            match.Profile = new StepProfile();
            var times = new double[steps];
            for (int i = 0; i < steps; i++)
            {
                long start = Stopwatch.GetTimestamp();
                match.Advance();
                times[i] = (Stopwatch.GetTimestamp() - start) * 1000.0 / Stopwatch.Frequency;
            }
            var sorted = (double[])times.Clone(); Array.Sort(sorted);
            double sum = 0; foreach (var t in times) sum += t;
            var phases = new double[StepProfile.Names.Length];
            for (int i = 0; i < phases.Length; i++) phases[i] = match.Profile.Ticks[i] * 1000.0 / Stopwatch.Frequency / Math.Max(1, match.Profile.Steps);
            return new Result { Units = units, Alive = match.Units.Count, Steps = steps, Mean = sum / steps, Median = sorted[steps / 2],
                P95 = sorted[Math.Min(steps - 1, (int)(steps * .95))], Max = sorted[steps - 1], SetupMs = setup, Phases = phases };
        }

        static string Json(List<Result> results, int steps)
        {
            var json = new StringBuilder();
            json.Append("{\n  \"what\": \"Simulation stress battle without the view: 8 sides meet in the middle of Temple Basin (StressBattle)\",\n");
            json.Append($"  \"date\": \"{DateTime.Now.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture)}\",\n  \"unity\": \"{Application.unityVersion}\",\n");
            json.Append($"  \"machine\": \"{SystemInfo.processorType.Trim()} ({SystemInfo.processorCount} threads), {SystemInfo.operatingSystem}\",\n");
            json.Append($"  \"where\": \"Editor, main thread, Mono\",\n  \"steps\": {steps},\n  \"stepSeconds\": 0.05,\n  \"results\": [\n");
            for (int r = 0; r < results.Count; r++)
            {
                var x = results[r];
                json.Append(string.Format(CultureInfo.InvariantCulture, "    {{ \"units\": {0}, \"aliveAtEnd\": {1}, \"setupMs\": {2:0.0}, \"stepMs\": {{ \"mean\": {3:0.00}, \"median\": {4:0.00}, \"p95\": {5:0.00}, \"max\": {6:0.00} }}, \"phasesMeanMs\": {{ ",
                    x.Units, x.Alive, x.SetupMs, x.Mean, x.Median, x.P95, x.Max));
                for (int i = 0; i < x.Phases.Length; i++) json.Append(string.Format(CultureInfo.InvariantCulture, "\"{0}\": {1:0.000}{2}", StepProfile.Names[i], x.Phases[i], i + 1 < x.Phases.Length ? ", " : ""));
                json.Append(r + 1 < results.Count ? " } },\n" : " } }\n");
            }
            json.Append("  ]\n}\n");
            return json.ToString();
        }
    }
}
