using Order.Application;
using Order.Input;
using Order.Presentation;
using Order.Voice;
using Order.Voice.Local;
using System.IO;
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
        private void OnEnable()
        {
            if (!UnityEngine.Application.isPlaying || view != null) return;
            app = new AppController(new PlayerPrefsSettingsStore());
            var directory = UnityEngine.Application.isEditor
                ? Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../..", ".local-ai"))
                : Path.Combine(Path.GetDirectoryName(UnityEngine.Application.dataPath), "LocalAI");
            interpreter = new LocalIntentInterpreter(directory, Resources.Load<TextAsset>("OrderIntentPrompt").text,
                Resources.Load<TextAsset>("OrderIntentSchema").text, Resources.Load<TextAsset>("OrderIntentExamples").text);
            input = new AppCommandInput(app, interpreter);
            _ = interpreter.WarmupAsync();
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (MenuStyle != null) root.styleSheets.Add(MenuStyle);
            view = new AppMenuView(root, app, input, LaunchTestMap);
            app.Changed += ApplyAudio;
            ApplyAudio();
        }
        private void ApplyAudio() => AudioListener.volume = app.Settings.MasterVolume;
        private void LaunchTestMap()
        {
            if (testMap != null && testMap.enabled) return;
            view?.Dispose(); view = null;
            menuCamera = Camera.main;
            if (menuCamera == null) menuCamera = FindFirstObjectByType<Camera>();
            if (menuCamera != null) menuCamera.gameObject.SetActive(false);
            if (testMap == null) testMap = gameObject.AddComponent<TemplePrototypeView>();
            testMap.Initialize(GetComponent<UIDocument>().rootVisualElement, ReturnToMenu);
        }
        private void ReturnToMenu()
        {
            if (testMap != null) testMap.Shutdown();
            if (menuCamera != null) menuCamera.gameObject.SetActive(true);
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (MenuStyle != null) root.styleSheets.Add(MenuStyle);
            view = new AppMenuView(root, app, input, LaunchTestMap);
        }
        private void OnDisable() => Cleanup();
        private void OnDestroy() => Cleanup();
        private void Cleanup()
        {
            if (testMap != null) { testMap.Shutdown(); Destroy(testMap); }
            testMap = null;
            if (menuCamera != null) menuCamera.gameObject.SetActive(true);
            if (app != null) app.Changed -= ApplyAudio;
            view?.Dispose();
            interpreter?.Dispose();
            view = null;
            interpreter = null;
            app = null;
        }
    }
}
