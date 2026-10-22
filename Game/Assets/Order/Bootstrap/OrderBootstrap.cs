using Order.Application;
using Order.Contracts;
using Order.Input;
using Order.Presentation;
using Order.Voice;
using Order.Voice.Local;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UIElements;

namespace Order.Bootstrap
{
    [RequireComponent(typeof(UIDocument))]
    public sealed class OrderBootstrap : MonoBehaviour
    {
        public StyleSheet MenuStyle;
        private AppController app;
        private AppMenuView view;
        private LocalIntentInterpreter interpreter;
        private AppCommandInput input;
        private TemplePrototypeView testMap;
        private Camera menuCamera;
        private Task<bool> warmup;
        private Phrasebook phrasebook;
        private void OnEnable()
        {
            if (!UnityEngine.Application.isPlaying || view != null) return;
            app = new AppController(new PlayerPrefsSettingsStore());
            var directory = UnityEngine.Application.isEditor
                ? Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../..", ".local-ai"))
                : Path.Combine(Path.GetDirectoryName(UnityEngine.Application.dataPath), "LocalAI");
            interpreter = new LocalIntentInterpreter(directory, Resources.Load<TextAsset>("OrderIntentPrompt").text,
                Resources.Load<TextAsset>("OrderIntentSchema").text, Resources.Load<TextAsset>("OrderIntentExamples").text);
            phrasebook = new Phrasebook(new FilePhrasebookStore(UnityEngine.Application.persistentDataPath));
            input = new AppCommandInput(app, new PhrasebookInterpreter(new MenuCommandInterpreter(interpreter), phrasebook));
            warmup = interpreter.WarmupAsync();
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (MenuStyle != null) root.styleSheets.Add(MenuStyle);
            view = new AppMenuView(root, app, input, warmup, phrasebook);
            app.Changed += OnAppChanged;
            app.Requested += OnRequested;
            ApplyAudio();
        }
        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }
        private void OnRequested(AppRequest request)
        {
            if (request == AppRequest.LaunchTestMap) LaunchTestMap();
            else if (request == AppRequest.Quit) Quit();
        }
        private void ApplyAudio() => AudioListener.volume = app.Settings.MasterVolume;
        // Leaving the test map is a screen change (e.g. "в штаб"), so menu, HUD and assistant commands agree.
        private void OnAppChanged()
        {
            ApplyAudio();
            if (testMap != null && testMap.enabled && app.Screen != AppScreen.TestMap && !app.Screen.IsSettings()) ReturnToMenu();
        }
        private void LaunchTestMap()
        {
            if (testMap != null && testMap.enabled) return;
            view?.Dispose(); view = null;
            menuCamera = Camera.main;
            if (menuCamera == null) menuCamera = FindFirstObjectByType<Camera>();
            if (menuCamera != null) menuCamera.gameObject.SetActive(false);
            if (testMap == null) testMap = gameObject.AddComponent<TemplePrototypeView>();
            testMap.Initialize(GetComponent<UIDocument>().rootVisualElement, ReturnToMenu, new MatchServices { App = app, Input = input, Phrasebook = phrasebook });
        }
        private void ReturnToMenu()
        {
            if (testMap == null || !testMap.enabled) return;
            if (app.Screen == AppScreen.TestMap) { input.SubmitManual(AppCommand.Navigate(AppScreen.MainMenu)); return; }
            testMap.Shutdown();
            if (menuCamera != null) menuCamera.gameObject.SetActive(true);
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (MenuStyle != null) root.styleSheets.Add(MenuStyle);
            view = new AppMenuView(root, app, input, warmup, phrasebook);
        }
        private void OnDisable() => Cleanup();
        private void OnDestroy() => Cleanup();
        private void Cleanup()
        {
            if (testMap != null) { testMap.Shutdown(); Destroy(testMap); }
            testMap = null;
            if (menuCamera != null) menuCamera.gameObject.SetActive(true);
            if (app != null) { app.Changed -= OnAppChanged; app.Requested -= OnRequested; }
            view?.Dispose();
            interpreter?.Dispose();
            view = null;
            interpreter = null;
            app = null;
        }
    }
}
