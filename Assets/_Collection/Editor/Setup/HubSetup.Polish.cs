using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static AllMightyOne.EditorTools.CollectionSceneUtil;

namespace AllMightyOne.EditorTools
{
    public static partial class HubSetup
    {
        const string MaterialsDir = "Assets/_Collection/Art/Materials";
        static readonly Color DarkInk = new(0.12f, 0.06f, 0.28f, 1f);

        /// <summary>
        /// Readability and look fixes on top of the per-game setups (idempotent): dark labels on the yellow ad buttons,
        /// outlined level headers in the runners, and restyled arenas and titles for Bull Dodge, Bomb Survival and Ninja.
        /// </summary>
        [MenuItem("All Mighty One/Hub/Polish Games")]
        public static void PolishGames()
        {
            Directory.CreateDirectory(MaterialsDir);
            foreach (var (name, path) in ScenePaths)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                DarkAdLabels();
                var header = Find("ScoreRecord/Current Level Text");
                if (header && KitWallet.Contains(name)) Outlined(header.GetComponent<Text>(), DarkInk, 5f);

                switch (name)
                {
                    case "Dodge": PolishDodge(); break;
                    case "Explosion": PolishBomb(); break;
                    case "Ninja": PolishNinja(); break;
                }
                Save(scene);
            }
        }

        // white on yellow was unreadable (BIG HEAD START, REVIVE, x3 GEMS...)
        static void DarkAdLabels()
        {
            foreach (var b in Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var img = b.GetComponent<Image>();
                if (!img || !Similar(img.color, AdButtonColor)) continue;
                foreach (var t in b.GetComponentsInChildren<Text>(true))
                {
                    t.color = AdTextColor;
                    var o = t.GetComponent<UnityEngine.UI.Outline>();
                    if (o) Object.DestroyImmediate(o);
                    EditorUtility.SetDirty(t);
                }
                foreach (var t in b.GetComponentsInChildren<TMP_Text>(true))
                {
                    t.color = AdTextColor;
                    EditorUtility.SetDirty(t);
                }
            }
        }

        static void PolishDodge()
        {
            // the kit's leaderboard crown and "MOST" name tag floated over the player
            var player = Find("Objects/Character_Dodge");
            if (player)
                foreach (var t in player.GetComponentsInChildren<Transform>(true).Where(t => t.name is "Name Text" or "Crown" or "CrownHolder"))
                    t.gameObject.SetActive(false);

            // western arena: warm sand inside, dusty brown outside (was grey)
            Recolor("Ground Sand/InSide/Plane", "Dodge Sand", new Color(1f, 0.83f, 0.58f));
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(r => r.name == "Plane OutSide"))
                r.sharedMaterial = Tinted(r.sharedMaterial, "Dodge Outside", new Color(0.62f, 0.42f, 0.26f));

            var title = Find("Menu UI/Title")?.GetComponent<TMP_Text>();
            if (title) OutlinedTmp(title, "Dodge Title", 0.3f);
            var best = Find("Menu UI/Best Score")?.GetComponent<TMP_Text>();
            if (best) OutlinedTmp(best, "Dodge Best", 0.25f);
        }

        static void PolishBomb()
        {
            // orange title on the orange board: yellow with a heavy dark edge, every menu line outlined
            var menu = Find("Canvas/Menu UI") ?? Find("Menu UI");
            if (!menu) return;
            foreach (var t in menu.GetComponentsInChildren<Text>(true))
            {
                if (t.name == "Title") t.color = new Color(1f, 0.88f, 0.28f);
                Outlined(t, new Color(0.25f, 0.07f, 0f, 1f), t.name == "Title" ? 7f : 5f);
            }
        }

        static void PolishNinja()
        {
            // the empty grey-purple floor becomes a teal dojo yard (the icon's colour), the title gets an edge
            Recolor("Objects/Enviroment/Cube", "Ninja Ground", new Color(0.42f, 0.7f, 0.66f));
            var menu = Find("Canvas/Menu UI");
            if (!menu) return;
            foreach (var t in menu.GetComponentsInChildren<Text>(true))
            {
                if (t.name == "Title") t.color = new Color(1f, 0.86f, 0.3f);
                Outlined(t, DarkInk, t.name == "Title" ? 7f : 5f);
            }
        }

        // ------------------------------------------------------------------ helpers

        static bool Similar(Color a, Color b) => Mathf.Abs(a.r - b.r) + Mathf.Abs(a.g - b.g) + Mathf.Abs(a.b - b.b) < 0.08f;

        static void Outlined(Text t, Color color, float distance)
        {
            if (!t) return;
            var outlines = t.GetComponents<UnityEngine.UI.Outline>();
            var o = outlines.FirstOrDefault() ?? t.gameObject.AddComponent<UnityEngine.UI.Outline>();
            o.effectColor = color;
            o.effectDistance = new Vector2(distance, -distance);
            EditorUtility.SetDirty(t);
        }

        /// <summary> Gives a kit TMP label a dark outline through its own material preset (never the font's shared one). </summary>
        static void OutlinedTmp(TMP_Text t, string name, float width)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var source = t.font.material;
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(source);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.CopyPropertiesFromMaterial(source);
            mat.shader = source.shader;
            mat.SetFloat("_OutlineWidth", width);
            mat.SetColor("_OutlineColor", DarkInk);
            mat.SetFloat("_FaceDilate", 0.15f);
            EditorUtility.SetDirty(mat);
            t.fontSharedMaterial = mat;
            EditorUtility.SetDirty(t);
        }

        static void Recolor(string objectPath, string materialName, Color color)
        {
            var go = Find(objectPath);
            var r = go ? go.GetComponent<Renderer>() : null;
            if (!r) { Debug.LogWarning($"[AMO] polish: no renderer at {objectPath}"); return; }
            r.sharedMaterial = Tinted(r.sharedMaterial, materialName, color);
        }

        /// <summary> A copy of the kit material with a new colour, so the kit asset itself stays untouched. </summary>
        static Material Tinted(Material source, string name, Color color)
        {
            string path = $"{MaterialsDir}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!mat)
            {
                mat = new Material(source);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat != source) // re-run: the renderer already uses our copy
            {
                mat.CopyPropertiesFromMaterial(source);
                mat.shader = source.shader;
            }
            mat.color = color;
            EditorUtility.SetDirty(mat);
            return mat;
        }
    }
}
