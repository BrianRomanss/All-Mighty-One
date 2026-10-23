using System.Collections.Generic;
using System.Linq;
using AllMightyOne.Hub;
using Solo.MOST_IN_ONE;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using static AllMightyOne.EditorTools.CollectionSceneUtil;

namespace AllMightyOne.EditorTools
{
    public static partial class HubSetup
    {
        // fraction of the canvas height below the top: lower where the kit's top row is taller
        static readonly Dictionary<string, float> HomeButtonDrop = new()
        {
            ["Ninja"] = 0.24f, ["Cubey"] = 0.25f, ["Splat"] = 0.25f, ["BlockBlast Adventure"] = 0.075f,
        };

        // scenes whose kit UI already shows the gem balance (top right)
        static readonly HashSet<string> KitWallet = new() { "EpicRunner", "Count", "BigBrain", "Pistol", "3Road" };

        // where our gem pill goes instead of the home button's row on the right: (anchor x, fraction below the top)
        static readonly Dictionary<string, (float x, float down)> GemPillSpot = new()
        {
            ["Ninja"] = (1f, 0.125f),                 // the title fills the home button's row
            ["BlockBlast Adventure"] = (0.5f, 0.075f), // between the home button and the settings gear
        };

        const string MusicPath = "Assets/Most In One/Common/Sounds/LoopMusic.wav";

        /// <summary>
        /// Every game gets a way back to the hub: a round home button on its menu (top-left, sized to the scene's
        /// canvas) and the Android back key while the menu is shown. Games without a kit wallet also get the gem
        /// balance on the same row, and every scene gets the collection music.
        /// </summary>
        [MenuItem("All Mighty One/Hub/Add Home Buttons To Games")]
        public static void AddHomeButtons()
        {
            PrepareText();
            foreach (var path in ScenePaths.Values)
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                var old = Object.FindObjectsByType<HubReturn>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (var r in old)
                {
                    if (r.HomeButton && r.HomeButton.name == "Hub Home Button") Object.DestroyImmediate(r.HomeButton.gameObject);
                    if (r.GemPill) Object.DestroyImmediate(r.GemPill);
                    Object.DestroyImmediate(r.gameObject);
                }

                var holder = new GameObject("Hub Return").AddComponent<HubReturn>();
                var menu = Find("Canvas/Menu UI") ?? Find("Menu UI");
                var canvas = menu ? menu.GetComponentInParent<Canvas>(true).rootCanvas : Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(c => c.renderMode != RenderMode.WorldSpace);
                var scaler = canvas.GetComponent<CanvasScaler>();
                var refSize = scaler ? scaler.referenceResolution : new Vector2(1080, 1920);
                float size = refSize.x * 0.11f;
                float down = HomeButtonDrop.TryGetValue(scene.name, out float d) ? d : 0.16f;

                // Block Adventure's kit arrow opened the old Block Puzzle menu scene: hide it, ours takes its place
                var kitArrow = scene.name == "BlockBlast Adventure" ? Find("Main Menu Button") : null;
                if (kitArrow)
                {
                    var b = kitArrow.GetComponent<Button>();
                    if (b) for (int i = b.onClick.GetPersistentEventCount() - 1; i >= 0; i--) UnityEventTools.RemovePersistentListener(b.onClick, i);
                    kitArrow.SetActive(false);
                }

                var btn = Button3D("Hub Home Button", canvas.transform, new Vector2(size, size), Violet, Ui("circle"), size * 0.09f);
                Place((RectTransform)btn.transform, new Vector2(0, 1), new Vector2(refSize.x * 0.1f, -refSize.y * down), new Vector2(size, size));
                Icon(btn, Ui("ic_home"), 0.56f);
                holder.HomeButton = btn;
                holder.MenuPanel = menu;

                if (!KitWallet.Contains(scene.name))
                {
                    var spot = GemPillSpot.TryGetValue(scene.name, out var s) ? s : (x: 1f, down);
                    holder.GemPill = GemPill(canvas.transform, refSize, spot.x, spot.down);
                }
                Debug.Log($"[AMO] {scene.name}: home button on '{canvas.name}' (ref {refSize}), menu={(menu ? menu.name : "none")}, gems={(holder.GemPill ? "ours" : "kit")}");

                AddMusic();
                EditorUtility.SetDirty(holder);
                Save(scene);
            }
        }

        /// <summary> Dark pill with the gem and the shared balance, sized like the kit's runner wallets. </summary>
        static GameObject GemPill(Transform canvas, Vector2 refSize, float anchorX, float down)
        {
            var size = new Vector2(refSize.x * 0.3f, refSize.x * 0.09f);
            var pill = Rect("Gem Pill", canvas);
            float x = anchorX switch { 0.5f => 0f, _ => -refSize.x * 0.05f - size.x * 0.5f };
            Place(pill, new Vector2(anchorX, 1), new Vector2(x, -refSize.y * down), size);
            Stretch(Img("Pill", pill, Ui("pill"), new Color(0.1f, 0.05f, 0.3f, 0.55f)).rectTransform);
            var gem = Img("Gem", pill, Kit("Gem"), Color.white);
            gem.preserveAspect = true;
            Place(gem.rectTransform, new Vector2(0, 0.5f), new Vector2(size.y * 0.35f, size.y * 0.02f), new Vector2(size.y * 1.15f, size.y));
            var text = Txt("Gems", pill, "0", size.y * 0.55f, Color.white);
            Stretch(text.rectTransform, size.y * 0.95f, 0, size.y * 0.3f, 0);
            var counter = pill.gameObject.AddComponent<GemCounter>();
            var catalog = AssetDatabase.LoadAssetAtPath<GameCatalog>(CatalogPath);
            counter.Database = catalog ? catalog.Database : AssetDatabase.LoadAssetAtPath<MOST_Database>(DatabasePath);
            if (catalog) counter.CurrencyName = catalog.CurrencyName;
            counter.Text = text;
            return pill.gameObject;
        }

        /// <summary> The persistent music player (one survives across scenes). Removes the kit's own scene music. </summary>
        static void AddMusic()
        {
            foreach (var m in Object.FindObjectsByType<CollectionMusic>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                Object.DestroyImmediate(m.gameObject);
            DisableKitMusic();
            var importer = (AudioImporter)AssetImporter.GetAtPath(MusicPath);
            var settings = importer.defaultSampleSettings;
            if (settings.loadType != AudioClipLoadType.Streaming)
            {
                settings.loadType = AudioClipLoadType.Streaming; // a minute of music: don't decompress it into memory
                importer.defaultSampleSettings = settings;
                importer.SaveAndReimport();
            }
            var music = new GameObject("Collection Music").AddComponent<CollectionMusic>();
            music.Clip = AssetDatabase.LoadAssetAtPath<AudioClip>(MusicPath);
            music.Audio = AssetDatabase.LoadAssetAtPath<MOST_AudioManager>(AudioPath);
        }

        /// <summary> Cubey and Splat had a "Background Music" audio action that kept playing into the hub: switch it off. </summary>
        static void DisableKitMusic()
        {
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!mb) continue;
                var so = new SerializedObject(mb);
                var it = so.GetIterator();
                bool changed = false;
                while (it.Next(true))
                {
                    if (it.propertyType != SerializedPropertyType.ManagedReference || it.managedReferenceValue is not MOSTAction_AudioSource) continue;
                    var clip = it.FindPropertyRelative("AudioClip");
                    var enabled = it.FindPropertyRelative("Enabled");
                    if (clip == null || enabled == null || AssetDatabase.GetAssetPath(clip.objectReferenceValue) != MusicPath || !enabled.boolValue) continue;
                    enabled.boolValue = false;
                    changed = true;
                }
                if (!changed) continue;
                so.ApplyModifiedPropertiesWithoutUndo();
                Debug.Log($"[AMO] kit music action switched off on {mb.name}");
            }
        }
    }
}
