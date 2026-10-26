using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Order.Editor
{
    // A Windows Player of the stress lab alone, to measure the game's own numbers (StressLab): in Game/Builds, which git
    // ignores. The scripting backend is switched only for the build and set back after it, so the project settings stay
    // as they were.
    //   StressLab.exe -batchmode -nographics -benchmark            — the simulation benchmark, JSON in the data folder
    //   StressLab.exe -units 2000 -sides 8 -quit-after 60           — the battle drawn, CSV in the data folder
    public static class StressLabBuild
    {
        [MenuItem("Order/Build Stress Lab Player (IL2CPP)")]
        public static void BuildIl2Cpp() => Debug.Log(Build(true));
        [MenuItem("Order/Build Stress Lab Player (Mono)")]
        public static void BuildMono() => Debug.Log(Build(false));

        public static string Build(bool il2cpp)
        {
            var target = NamedBuildTarget.Standalone;
            var previous = PlayerSettings.GetScriptingBackend(target);
            // A build saves the project settings with what it adds for the Player (the Input System's actions among the
            // preloaded assets); they are put back and saved again after it.
            var preloaded = PlayerSettings.GetPreloadedAssets();
            var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Builds", il2cpp ? "StressLab-IL2CPP" : "StressLab-Mono"));
            var wanted = il2cpp ? ScriptingImplementation.IL2CPP : ScriptingImplementation.Mono2x;
            try
            {
                if (previous != wanted) PlayerSettings.SetScriptingBackend(target, wanted);
                var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { StressLabSetup.ScenePath },
                    locationPathName = Path.Combine(folder, "StressLab.exe"),
                    target = BuildTarget.StandaloneWindows64,
                    options = BuildOptions.None,
                });
                return $"{report.summary.result}: {report.summary.outputPath}, {report.summary.totalTime.TotalSeconds:0} s, {report.summary.totalErrors} errors";
            }
            finally
            {
                if (previous != wanted) PlayerSettings.SetScriptingBackend(target, previous);
                PlayerSettings.SetPreloadedAssets(preloaded);
                AssetDatabase.SaveAssets();
            }
        }
    }
}
