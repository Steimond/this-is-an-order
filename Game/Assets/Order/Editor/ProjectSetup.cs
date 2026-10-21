using System.Linq;
using Order.Bootstrap;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Order.Editor
{
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/Order/Scenes/MainMenu.unity";

        [MenuItem("Order/Create Main Menu Scene")]
        public static void CreateMainMenu()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) return;
            if (!AssetDatabase.IsValidFolder("Assets/Order/Scenes")) AssetDatabase.CreateFolder("Assets/Order", "Scenes");
            const string panelPath = "Assets/Order/Presentation/MenuPanelSettings.asset";
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>(panelPath);
            if (panel == null)
            {
                panel = ScriptableObject.CreateInstance<PanelSettings>();
                panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panel.referenceResolution = new Vector2Int(1280, 800);
                panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
                panel.match = 0.5f;
                panel.themeStyleSheet = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>("Assets/Order/Presentation/OrderTheme.tss");
                AssetDatabase.CreateAsset(panel, panelPath);
            }
            var current = SceneManager.GetActiveScene();
            var replaceUntitled = SceneManager.sceneCount == 1 && string.IsNullOrEmpty(current.path) && !current.isDirty;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
                replaceUntitled ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var camera = new GameObject("Menu Camera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.055f, 0.086f, 0.125f);
            // URP also culls the screen-space UI layer with the camera mask.
            camera.cullingMask = -1;
            var root = new GameObject("Order Application");
            var document = root.AddComponent<UIDocument>();
            var bootstrap = root.AddComponent<OrderBootstrap>();
            bootstrap.MenuStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Order/Presentation/OrderMenu.uss");
            document.panelSettings = panel;
            var serializedDocument = new SerializedObject(document);
            serializedDocument.FindProperty("m_PanelSettings").objectReferenceValue = panel;
            serializedDocument.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(document);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) }
                .Concat(EditorBuildSettings.scenes.Where(s => s.path != ScenePath)).ToArray();
            AssetDatabase.SaveAssets();
        }
    }
}
