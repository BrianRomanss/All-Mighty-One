using UnityEditor;
using UnityEngine;

namespace AllMightyOne.EditorTools
{
    public static class ProjectFixes
    {
        /// <summary>
        /// The kit ships its own TMP resources (Most In One/Common/Third-Party/TextMesh Pro). Do not import Unity's
        /// TMP Essentials as well: two "TMP Settings" in Resources folders make TMP pick either one.
        /// </summary>
        /// <summary>
        /// The kit was imported without its ProjectSettings, so the Tag Manager has none of the tags its prefabs and code use
        /// (Gate, Enemy, Score Object, ...). The editor tolerates tags found on loaded objects, but a player build only knows
        /// the Tag Manager list: every CompareTag would fail on device. This registers every tag used by kit assets.
        /// </summary>
        [MenuItem("All Mighty One/Project/Add Missing Tags")]
        public static void AddMissingTags()
        {
            var used = new System.Collections.Generic.SortedSet<string>();
            var tagLine = new System.Text.RegularExpressions.Regex(@"^\s*m_TagString: (.+)$");
            var listKey = new System.Text.RegularExpressions.Regex(@"^(\s*)(\w*Tags):\s*$");
            var listItem = new System.Text.RegularExpressions.Regex(@"^\s*- (.+)$");
            foreach (var path in System.IO.Directory.GetFiles("Assets", "*.*", System.IO.SearchOption.AllDirectories))
            {
                if (!(path.EndsWith(".prefab") || path.EndsWith(".unity") || path.EndsWith(".asset"))) continue;
                var lines = System.IO.File.ReadAllLines(path);
                for (int i = 0; i < lines.Length; i++)
                {
                    var m = tagLine.Match(lines[i]);
                    if (m.Success) { used.Add(m.Groups[1].Value.Trim()); continue; }
                    var k = listKey.Match(lines[i]);
                    if (!k.Success) continue;
                    for (int j = i + 1; j < lines.Length; j++) // "DamageTags:" followed by "- Damage" items
                    {
                        var item = listItem.Match(lines[j]);
                        if (!item.Success || lines[j].Contains(":")) break;
                        used.Add(item.Groups[1].Value.Trim().Trim('\''));
                    }
                }
            }
            foreach (var codeTag in new[] { "Gate", "Enemy", "Score Object", "Character" }) used.Add(codeTag); // CompareTag/WithTag in kit scripts

            var builtIn = new System.Collections.Generic.HashSet<string>(UnityEditorInternal.InternalEditorUtility.tags);
            var manager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var tags = manager.FindProperty("tags");
            var added = new System.Collections.Generic.List<string>();
            foreach (var tag in used)
            {
                if (string.IsNullOrWhiteSpace(tag) || builtIn.Contains(tag)) continue;
                tags.InsertArrayElementAtIndex(tags.arraySize);
                tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = tag;
                added.Add(tag);
            }
            manager.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log($"[AMO] tags used by assets: {string.Join(", ", used)}\n[AMO] added to Tag Manager: {string.Join(", ", added)}");
        }

        public static void ListTags()
        {
            Debug.Log("[AMO] tags: " + string.Join(", ", UnityEditorInternal.InternalEditorUtility.tags));
            Debug.Log("[AMO] layers: " + string.Join(", ", UnityEditorInternal.InternalEditorUtility.layers));
        }

        [MenuItem("All Mighty One/Project/Check TMP Settings")]
        public static void CheckTmp()
        {
            var settings = Resources.LoadAll<TMPro.TMP_Settings>("");
            foreach (var s in settings)
                Debug.Log($"[AMO] TMP Settings at {AssetDatabase.GetAssetPath(s)}");
            Debug.Log($"[AMO] TMP settings count={settings.Length} (expected 1), default font={(TMPro.TMP_Settings.defaultFontAsset ? TMPro.TMP_Settings.defaultFontAsset.name : "null")}");
        }

        /// <summary>
        /// Quick Outline (kit Third-Party) bakes smooth normals from the mesh in Awake, which needs Read/Write on the model.
        /// Kit models used with an Outline in the build scenes or prefabs are switched to readable.
        /// </summary>
        [MenuItem("All Mighty One/Project/Make Outlined Meshes Readable")]
        public static void MakeOutlinedMeshesReadable()
        {
            var paths = new System.Collections.Generic.HashSet<string>();
            void Collect(GameObject root)
            {
                foreach (var outline in root.GetComponentsInChildren<Outline>(true))
                {
                    foreach (var mf in outline.GetComponentsInChildren<MeshFilter>(true)) Add(mf.sharedMesh);
                    foreach (var smr in outline.GetComponentsInChildren<SkinnedMeshRenderer>(true)) Add(smr.sharedMesh);
                }
            }
            void Add(Mesh mesh)
            {
                if (mesh && !mesh.isReadable) paths.Add(AssetDatabase.GetAssetPath(mesh));
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Most In One" }))
                Collect(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)));
            foreach (var s in EditorBuildSettings.scenes)
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(s.path, UnityEditor.SceneManagement.OpenSceneMode.Single);
                foreach (var root in scene.GetRootGameObjects()) Collect(root);
            }

            foreach (var path in paths)
            {
                if (AssetImporter.GetAtPath(path) is ModelImporter importer)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                    Debug.Log($"[AMO] Read/Write enabled on {path}");
                }
                else Debug.LogWarning($"[AMO] Outlined mesh is not an imported model: {path}");
            }
            Debug.Log($"[AMO] Outlined meshes made readable: {paths.Count}");
        }
        /// <summary>
        /// The kit's materials use the built-in Standard shader (841 of them) and render magenta under URP, which this
        /// project was created with. Switches every quality level to the built-in render pipeline the kit was made for.
        /// </summary>
        [MenuItem("All Mighty One/Project/Use Built-in Render Pipeline")]
        public static void UseBuiltInRenderPipeline()
        {
            UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline = null;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = null;
            }
            QualitySettings.SetQualityLevel(current, false);
            AssetDatabase.SaveAssets();
            Debug.Log($"[AMO] Render pipeline: built-in (quality levels: {QualitySettings.names.Length})");
        }
    }
}
