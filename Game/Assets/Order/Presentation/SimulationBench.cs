using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using Order.Balance;
using Order.Content;
using Order.Simulation;
using UnityEngine;

namespace Order.Presentation
{
    // The simulation alone under load, without the view: the stress battle (StressBattle) of every size in turn, each
    // played for a number of steps with the step time and its phases measured. Runs in the editor (Order → Benchmark
    // Simulation) and in a Player build of the stress lab (StressLab.exe -batchmode -nographics -benchmark), where the
    // numbers are the game's own; the report names where it ran.
    public static class SimulationBench
    {
        public static readonly int[] Sizes = { 500, 1000, 2000, 3000 };
        public const int Steps = 600;
        public static string Where => (UnityEngine.Application.isEditor ? "Editor" : "Player") +
#if ENABLE_IL2CPP
            ", IL2CPP";
#else
            ", Mono";
#endif

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

        public static List<Result> Run(int[] sizes, int steps)
        {
            var catalog = Resources.Load<BalanceCatalog>("PrototypeBalanceCatalog");
            var results = new List<Result>();
            foreach (int size in sizes) results.Add(Play(catalog != null ? catalog.Snapshot() : null, size, steps));
            return results;
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

        public static string Text(List<Result> results)
        {
            var text = new StringBuilder();
            foreach (var r in results) text.AppendLine(r.ToString());
            return text.ToString();
        }

        public static string Json(List<Result> results, int steps)
        {
            var json = new StringBuilder();
            json.Append("{\n  \"what\": \"Simulation stress battle without the view: 8 sides meet in the middle of Temple Basin (StressBattle)\",\n");
            json.Append($"  \"date\": \"{DateTime.Now.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture)}\",\n  \"unity\": \"{UnityEngine.Application.unityVersion}\",\n");
            json.Append($"  \"machine\": \"{SystemInfo.processorType.Trim()} ({SystemInfo.processorCount} threads), {SystemInfo.operatingSystem}\",\n");
            json.Append($"  \"where\": \"{Where}, main thread\",\n  \"steps\": {steps},\n  \"stepSeconds\": 0.05,\n  \"results\": [\n");
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
