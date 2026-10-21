using Order.Application;
using Order.Input;
using Order.Presentation;
using Order.Voice;
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
        private void Start()
        {
            app = new AppController(new PlayerPrefsSettingsStore());
            var input = new AppCommandInput(app, new RussianAppInterpreter());
            var root = GetComponent<UIDocument>().rootVisualElement;
            if (MenuStyle != null) root.styleSheets.Add(MenuStyle);
            view = new AppMenuView(root, app, input);
            app.Changed += ApplyAudio;
            ApplyAudio();
        }
        private void ApplyAudio() => AudioListener.volume = app.Settings.MasterVolume;
        private void OnDestroy()
        {
            if (app != null) app.Changed -= ApplyAudio;
            view?.Dispose();
        }
    }
}
