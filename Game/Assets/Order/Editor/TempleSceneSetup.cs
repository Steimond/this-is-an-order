using System.Linq;
using Order.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Order.Editor
{
    public static class TempleSceneSetup
    {
        public const string Path="Assets/Order/Scenes/TempleBasin.unity";
        [MenuItem("Order/Create Temple Basin Test Scene")]
        public static void Create()
        {
            if(AssetDatabase.LoadAssetAtPath<SceneAsset>(Path)!=null)return;
            var previous=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var root=new GameObject("Temple Basin Test",typeof(UIDocument),typeof(TemplePrototypeView));
            root.GetComponent<UIDocument>().panelSettings=AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/Order/Presentation/MenuPanelSettings.asset");
            EditorSceneManager.SaveScene(scene,Path);
            EditorBuildSettings.scenes=EditorBuildSettings.scenes.Where(s=>s.path!=Path).Concat(new[]{new EditorBuildSettingsScene(Path,true)}).ToArray();
            EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(previous);AssetDatabase.SaveAssets();
        }
    }
}
