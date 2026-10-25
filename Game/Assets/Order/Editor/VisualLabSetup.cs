using Order.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Order.Editor
{
    // The visual lab (VisualLab): a scene with every building and unit of the game laid out, and the nanite liquid's
    // material as an asset to tune. Makes whichever of the two is missing and leaves an existing one as it is.
    public static class VisualLabSetup
    {
        public const string ScenePath = "Assets/Order/Scenes/VisualLab.unity", MaterialPath = "Assets/Order/Presentation/Resources/NaniteLiquid.mat";
        [MenuItem("Order/Create Visual Lab")]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) == null)
                AssetDatabase.CreateAsset(new Material(Shader.Find("Order/NaniteLiquid")) { name = "NaniteLiquid" }, MaterialPath);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                var previous = SceneManager.GetActiveScene();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                var root = new GameObject("Visual Lab", typeof(UIDocument), typeof(TemplePrototypeView), typeof(VisualLab));
                root.GetComponent<UIDocument>().panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Order/Presentation/MenuPanelSettings.asset");
                EditorSceneManager.SaveScene(scene, ScenePath);
                EditorSceneManager.CloseScene(scene, true); SceneManager.SetActiveScene(previous);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
