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
        private void OnEnable()
        {
            if (!UnityEngine.Application.isPlaying || view != null) return;
            app = new AppController(new PlayerPrefsSettingsStore());
            var directory = UnityEngine.Application.isEditor
                ? Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, "../..", ".local-ai"))
                : Path.Combine(Path.GetDirectoryName(UnityEngine.Application.dataPath), "LocalAI");
            interpreter = new LocalIntentInterpreter(directory, Resources.Load<TextAsset>("OrderIntentPrompt").text,
                Resources.Load<TextAsset>("OrderIntentSchema").text, Resources.Load<TextAsset>("OrderIntentExamples").text);
            var input = new AppCommandInput(app, interpreter);
            _ = interpreter.WarmupAsync();
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (MenuStyle != null) root.styleSheets.Add(MenuStyle);
            view = new AppMenuView(root, app, input);
            app.Changed += ApplyAudio;
            ApplyAudio();
        }
        private void ApplyAudio() => AudioListener.volume = app.Settings.MasterVolume;
        private void OnDisable() => Cleanup();
        private void OnDestroy() => Cleanup();
        private void Cleanup()
        {
            if (app != null) app.Changed -= ApplyAudio;
            view?.Dispose();
            interpreter?.Dispose();
            view = null;
            interpreter = null;
            app = null;
        }
    }
}
