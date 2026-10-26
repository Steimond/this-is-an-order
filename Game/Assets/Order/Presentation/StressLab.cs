using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Order.Simulation;
using UnityEngine;
using UnityEngine.Profiling;

namespace Order.Presentation
{
    // A scene to measure the whole game under load (Assets/Order/Scenes/StressLab.unity, Order → Create Stress Lab): the
    // stress battle (StressBattle) of Units units on Sides sides, drawn by the usual view. Every Window seconds it shows and
    // writes to a CSV file in the player's data folder: the match step (mean and worst, from the step profile), the frame
    // (mean and worst), the units alive and the memory. Run it in a Player build for numbers to compare with budgets; in
    // the editor it only compares runs with each other. Restart Play Mode after changing Units or Sides.
    // First of all scripts each frame, so the time from its Update to its LateUpdate is every script's Update, the view's
    // (and the match steps it plays) included.
    [DefaultExecutionOrder(-10000)]
    [RequireComponent(typeof(TemplePrototypeView))]
    public sealed class StressLab : MonoBehaviour
    {
        [Tooltip("Units of all sides together at the start; they all meet in the middle of the map.")]
        [Min(2)] public int Units = 1000;
        [Range(2, StressBattle.MaxSides)] public int Sides = StressBattle.MaxSides;
        [Tooltip("Seconds of each measurement.")]
        [Min(.5f)] public float Window = 2;

        TemplePrototypeView view;
        readonly StepProfile profile = new StepProfile();
        bool aimed;
        float windowStart, frameWorst, stepWorst;
        int frames, lastSteps;
        long lastTotal;
        double frameSum, scriptSum, stepSum;
        long updateStart;
        void Update() => updateStart = Stopwatch.GetTimestamp();
        string report = "", csv;

        // In a Player build the command line sets the lab: -units N, -sides N, -quit-after seconds; -benchmark runs the
        // simulation benchmark instead (SimulationBench) and quits. Reports go to the player's data folder.
        float quitAfter;
        void Awake()
        {
            view = GetComponent<TemplePrototypeView>();
            if (!UnityEngine.Application.isEditor)
            {
                Units = Argument("-units", Units); Sides = Mathf.Clamp(Argument("-sides", Sides), 2, StressBattle.MaxSides); quitAfter = Argument("-quit-after", 0);
                if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-benchmark") >= 0) { Benchmark(); return; }
            }
            view.Create = balance => StressBattle.Create(balance, Units, Sides);
            view.Setup = match => match.Profile = profile;
        }
        static int Argument(string name, int fallback)
        {
            var args = System.Environment.GetCommandLineArgs();
            int at = System.Array.IndexOf(args, name);
            return at >= 0 && at + 1 < args.Length && int.TryParse(args[at + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;
        }
        void Benchmark()
        {
            int steps = Argument("-steps", SimulationBench.Steps);
            var results = SimulationBench.Run(SimulationBench.Sizes, steps);
            var path = Path.Combine(UnityEngine.Application.persistentDataPath, $"simulation-stress-{System.DateTime.Now:yyyyMMdd-HHmmss}.json");
            File.WriteAllText(path, SimulationBench.Json(results, steps));
            UnityEngine.Debug.Log(SimulationBench.Text(results) + "Report: " + path);
            UnityEngine.Application.Quit();
        }

        void LateUpdate()
        {
            var match = view.Match;
            if (match == null) return;
            if (!aimed) { aimed = true; view.LookAt(new Vector2(match.Map.Size * .5f, match.Map.Size * .5f), 30); windowStart = Time.unscaledTime; }
            if (quitAfter > 0 && Time.unscaledTime >= quitAfter) UnityEngine.Application.Quit();
            float frame = Time.unscaledDeltaTime * 1000;
            if (updateStart != 0) scriptSum += (Stopwatch.GetTimestamp() - updateStart) * 1000.0 / Stopwatch.Frequency;
            stepSum += (profile.Total - lastTotal) * 1000.0 / Stopwatch.Frequency;
            frames++; frameSum += frame; frameWorst = Mathf.Max(frameWorst, frame);
            // The worst step of this frame: the frame's steps together over their count.
            int steps = profile.Steps - lastSteps;
            if (steps > 0) stepWorst = Mathf.Max(stepWorst, (float)((profile.Total - lastTotal) * 1000.0 / Stopwatch.Frequency / steps));
            lastSteps = profile.Steps; lastTotal = profile.Total;
            if (Time.unscaledTime - windowStart >= Window) Measure(match);
        }

        void Measure(PrototypeMatch match)
        {
            double stepMean = profile.Steps > 0 ? profile.Total * 1000.0 / Stopwatch.Frequency / profile.Steps : 0;
            double frameMean = frames > 0 ? frameSum / frames : 0;
            long memory = Profiler.GetTotalAllocatedMemoryLong();
            var text = new StringBuilder();
            text.AppendLine(string.Format(CultureInfo.InvariantCulture, "Units {0} of {1}, sides {2}, frame {3}", match.Units.Count, Units, match.Sides, match.Frame));
            text.AppendLine(string.Format(CultureInfo.InvariantCulture, "Step {0:0.0} ms, worst {1:0.0} · {2} steps", stepMean, stepWorst, profile.Steps));
            text.AppendLine(string.Format(CultureInfo.InvariantCulture, "Frame {0:0.0} ms ({1:0} fps), worst {2:0.0}", frameMean, frameMean > 0 ? 1000 / frameMean : 0, frameWorst));
            double scripts = frames > 0 ? scriptSum / frames : 0, stepsPerFrame = frames > 0 ? stepSum / frames : 0;
            text.AppendLine(string.Format(CultureInfo.InvariantCulture, "  scripts {0:0.0} (match {1:0.0}, view {2:0.0}), rest {3:0.0}", scripts, stepsPerFrame, scripts - stepsPerFrame, frameMean - scripts));
            text.AppendLine(string.Format(CultureInfo.InvariantCulture, "Memory {0:0} MB", memory / 1048576.0));
            for (int i = 0; i < StepProfile.Names.Length; i++)
            {
                double ms = profile.Steps > 0 ? profile.Ticks[i] * 1000.0 / Stopwatch.Frequency / profile.Steps : 0;
                if (ms >= .1) text.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0} {1:0.0}", StepProfile.Names[i], ms));
            }
            report = text.ToString();
            Write(match, stepMean, frameMean, scripts - stepsPerFrame, frameMean - scripts, memory);
            profile.Clear(); lastSteps = 0; lastTotal = 0;
            frames = 0; frameSum = 0; scriptSum = 0; stepSum = 0; frameWorst = 0; stepWorst = 0; windowStart = Time.unscaledTime;
        }

        void Write(PrototypeMatch match, double stepMean, double frameMean, double view, double rest, long memory)
        {
            try
            {
                if (csv == null)
                {
                    csv = Path.Combine(UnityEngine.Application.persistentDataPath, $"stress-{System.DateTime.Now:yyyyMMdd-HHmmss}.csv");
                    File.WriteAllText(csv, $"# {SystemInfo.processorType.Trim()}, {SystemInfo.graphicsDeviceName}, {SystemInfo.operatingSystem}, {(UnityEngine.Application.isEditor ? "Editor" : "Player")}\nframe,units,step_ms,step_worst_ms,frame_ms,frame_worst_ms,view_ms,rest_ms,memory_mb\n");
                    UnityEngine.Debug.Log("Stress lab writes to " + csv);
                }
                File.AppendAllText(csv, string.Format(CultureInfo.InvariantCulture, "{0},{1},{2:0.00},{3:0.00},{4:0.00},{5:0.00},{6:0.00},{7:0.00},{8:0}\n", match.Frame, match.Units.Count, stepMean, stepWorst, frameMean, frameWorst, view, rest, memory / 1048576.0));
            }
            catch (IOException) { }
        }

        void OnGUI()
        {
            if (report.Length == 0) return;
            GUI.Box(new Rect(12, 60, 300, 24 + report.Split('\n').Length * 15), "");
            GUI.Label(new Rect(20, 66, 290, 400), report);
        }
    }
}
