using Order.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Order.Editor
{
    // The stress lab (StressLab): the stress battle drawn by the usual view, with the step, frame and memory measured.
    // Makes the scene when it is missing and leaves an existing one as it is.
    public static class StressLabSetup
    {
        public const string ScenePath = "Assets/Order/Scenes/StressLab.unity";
        [MenuItem("Order/Create Stress Lab")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) return;
            var previous = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var root = new GameObject("Stress Lab", typeof(UIDocument), typeof(TemplePrototypeView), typeof(StressLab));
            root.GetComponent<UIDocument>().panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Order/Presentation/MenuPanelSettings.asset");
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorSceneManager.CloseScene(scene, true); SceneManager.SetActiveScene(previous);
            AssetDatabase.SaveAssets();
        }
    }
}
