using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AllMightyOne.EditorTools
{
    /// <summary> Crowd Count levels: converts the scene-per-level levels to prefabs and generates new ones. </summary>
    public static partial class CountLevelTools
    {
        public const string Folder = "Assets/Most In One/Runner/Scenes/Levels/Count";

        /// <summary> Each Count_Level_N scene holds a single root "Count_Level_N"; it is saved as Count_Level_N.prefab. </summary>
        [MenuItem("All Mighty One/Crowd Count/Convert Level Scenes To Prefabs")]
        public static void ConvertScenesToPrefabs()
        {
            foreach (var scenePath in Directory.GetFiles(Folder, "Count_Level_*.unity").Select(p => p.Replace('\\', '/')))
            {
                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var name = Path.GetFileNameWithoutExtension(scenePath);
                var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);
                if (!root) { Debug.LogError($"[AMO] {scenePath} has no root '{name}'"); continue; }
                PrefabUtility.SaveAsPrefabAsset(root, $"{Folder}/{name}.prefab");
                Debug.Log($"[AMO] Converted {name} to prefab");
            }
            AssetDatabase.SaveAssets();
        }
    }
}
