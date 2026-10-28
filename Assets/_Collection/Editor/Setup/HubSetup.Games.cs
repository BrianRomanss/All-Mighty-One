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
        const string SoundsDir = "Assets/Most In One/Common/Sounds";

        // first-play hint per scene: game id, gesture, the line shown, shown without a start menu, hand height (fraction
        // of the canvas height from the centre, + is up) and how far the hand travels (fraction of the canvas width)
        static readonly Dictionary<string, (string id, FirstPlayHint.Gesture gesture, string text, bool onStart, float y, float reach)> Hints = new()
        {
            ["EpicRunner"] = ("squad", FirstPlayHint.Gesture.SwipeSides, "DRAG TO STEER YOUR SQUAD", false, -0.22f, 0.24f),
            ["Count"] = ("crowd", FirstPlayHint.Gesture.SwipeSides, "DRAG THROUGH THE BEST GATES", false, -0.22f, 0.24f),
            ["BigBrain"] = ("brain", FirstPlayHint.Gesture.SwipeSides, "DRAG THROUGH THE GATES THAT GROW YOUR BRAIN", false, -0.22f, 0.24f),
            ["Pistol"] = ("pistol", FirstPlayHint.Gesture.SwipeSides, "DRAG TO AIM AND SHOOT THE BARRELS", false, -0.22f, 0.24f),
            ["Dodge"] = ("dodge", FirstPlayHint.Gesture.SwipeAround, "DRAG TO RUN AND DODGE THE BULLS", false, -0.2f, 0.22f),
            ["3Road"] = ("roads", FirstPlayHint.Gesture.Tap, "TAP TO SWITCH SIDES", false, -0.2f, 0f),
            ["Explosion"] = ("bomb", FirstPlayHint.Gesture.SwipeAround, "DRAG TO MOVE, STAY AWAY FROM THE BLASTS", false, -0.2f, 0.22f),
            ["Ninja"] = ("ninja", FirstPlayHint.Gesture.SwipeAround, "DRAG TO MOVE, YOUR NINJA STRIKES ZOMBIES NEARBY", false, -0.2f, 0.22f),
            ["BlockBlast Classic"] = ("blocks", FirstPlayHint.Gesture.DragUp, "DRAG PIECES ONTO THE BOARD AND FILL LINES", true, -0.33f, 0.2f),
            ["BlockBlast Adventure"] = ("adventure", FirstPlayHint.Gesture.DragUp, "DRAG PIECES ONTO THE BOARD TO CLEAR THE BLOCKS", true, -0.33f, 0.2f),
            ["Cubey"] = ("cubey", FirstPlayHint.Gesture.SwipeTurns, "SWIPE TO SLIDE AND COVER EVERY TILE", false, -0.08f, 0.24f),
            ["Splat"] = ("splat", FirstPlayHint.Gesture.SwipeTurns, "SWIPE TO ROLL AND PAINT EVERY TILE", false, -0.08f, 0.24f),
        };

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
                    if (r.PauseButton) Object.DestroyImmediate(r.PauseButton.gameObject);
                    if (r.PausePanel) Object.DestroyImmediate(r.PausePanel);
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

                AddHint(scene.name, canvas.transform, refSize);
                if (menu) AddPause(holder, canvas.transform, refSize, down); // games with a menu: runs can be paused and left
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

        /// <summary>
        /// Pause button in the home button's spot (shown only during a run) and the pause panel above everything:
        /// dimmed game, a white window with RESUME and HOME. Sized from the hub's 1080-wide popups.
        /// </summary>
        static void AddPause(HubReturn holder, Transform canvas, Vector2 refSize, float down)
        {
            float k = refSize.x / 1080f;
            float size = refSize.x * 0.11f;
            var pause = Button3D("Hub Pause Button", canvas, new Vector2(size, size), Violet, Ui("circle"), size * 0.09f);
            Place((RectTransform)pause.transform, new Vector2(0, 1), new Vector2(refSize.x * 0.1f, -refSize.y * down), new Vector2(size, size));
            var face = pause.transform.Find("Face");
            foreach (float x in new[] { -0.13f, 0.13f }) // the pause glyph: two rounded bars
            {
                var bar = Img("Bar", face, Ui("pill"), Color.white);
                Place(bar.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(size * x, size * 0.02f), new Vector2(size * 0.15f, size * 0.46f));
            }
            pause.gameObject.SetActive(false);

            var panel = Stretch(Rect("Hub Pause Panel", canvas));
            Stretch(Img("Dim", panel, null, new Color(0.04f, 0.02f, 0.15f, 0.7f), raycast: true).rectTransform);
            var window = Rect("Window", panel);
            Place(window, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, 560) * k);
            Stretch(Img("Shadow", window, Ui("shadow"), new Color(0, 0, 0, 0.45f)).rectTransform, -40 * k, -60 * k, -40 * k, -20 * k);
            Stretch(Img("Base", window, Ui("rr48"), new Color(0.78f, 0.76f, 0.9f)).rectTransform, 0, -14 * k, 0, 14 * k);
            Stretch(Img("Panel", window, Ui("rr48"), Color.white, raycast: true).rectTransform);
            var ribbon = Img("Ribbon", window, Ui("pill"), Violet);
            Place(ribbon.rectTransform, new Vector2(0.5f, 1), new Vector2(0, 6 * k), new Vector2(560, 124) * k);
            Stretch(Txt("Title", ribbon.transform, "PAUSED", 58 * k, Color.white).rectTransform, 30 * k, 0, 30 * k, 6 * k);

            Button Row(string name, string label, Color color, Sprite icon, float y)
            {
                var b = Button3D(name, window, new Vector2(600, 140) * k, color, Ui("pill"), 12 * k);
                Place((RectTransform)b.transform, new Vector2(0.5f, 0.5f), new Vector2(0, y * k), new Vector2(600, 140) * k);
                var f = b.transform.Find("Face");
                var ic = Img("Icon", f, icon, Color.white);
                ic.preserveAspect = true;
                Place(ic.rectTransform, new Vector2(0, 0.5f), new Vector2(60 * k, 4 * k), new Vector2(76, 76) * k, new Vector2(0, 0.5f));
                Stretch(Txt("Label", f, label, 50 * k, Color.white).rectTransform, 120 * k, 0, 40 * k, 4 * k);
                return b;
            }
            holder.ResumeButton = Row("Hub Resume Button", "RESUME", Green, Ui("ic_play"), 30);
            holder.PauseHomeButton = Row("Hub Pause Home Button", "HOME", Violet, Ui("ic_home"), -150);

            panel.SetAsLastSibling(); // above the first-play hint
            panel.gameObject.SetActive(false);
            holder.PauseButton = pause;
            holder.PausePanel = panel.gameObject;
        }

        static void AddHint(string sceneName, Transform canvas, Vector2 refSize)
        {
            foreach (var old in canvas.GetComponentsInChildren<FirstPlayHint>(true)) Object.DestroyImmediate(old.gameObject);
            if (!Hints.TryGetValue(sceneName, out var h)) return;

            var root = Stretch(Rect("First Play Hint", canvas));
            root.SetAsLastSibling();
            var hint = root.gameObject.AddComponent<FirstPlayHint>();
            hint.Group = root.gameObject.AddComponent<CanvasGroup>();
            hint.GameId = h.id;
            hint.Motion = h.gesture;
            hint.ShowOnStart = h.onStart;
            hint.Reach = refSize.x * h.reach;

            Vector2 touch = new(0, refSize.y * h.y);
            float ripple = refSize.x * 0.2f;
            var ring = Img("Ripple", root, Ui("ring"), new Color(1, 1, 1, 0.8f));
            Place(ring.rectTransform, new Vector2(0.5f, 0.5f), touch, new Vector2(ripple, ripple));
            hint.Ripple = ring.rectTransform;

            // the fingertip (20% from the left, at the top of the art) is the touch point
            var handArt = Ui("hand");
            float w = refSize.x * 0.17f;
            var hand = Img("Hand", root, handArt, Color.white);
            Place(hand.rectTransform, new Vector2(0.5f, 0.5f), touch, new Vector2(w, w * handArt.rect.height / handArt.rect.width), new Vector2(0.2f, 1f));
            hand.rectTransform.localEulerAngles = new Vector3(0, 0, 12f);
            hint.Hand = hand.rectTransform;

            // the line sits above the hand, or below it when the hand starts on the block tray (the board is above)
            float side = h.gesture == FirstPlayHint.Gesture.DragUp ? -0.24f : 0.22f;
            var plate = Img("Plate", root, Ui("pill"), new Color(0.08f, 0.03f, 0.25f, 0.6f));
            var label = Txt("Label", plate.transform, h.text, refSize.x * 0.05f, Color.white);
            label.textWrappingMode = TMPro.TextWrappingModes.Normal;
            label.enableAutoSizing = false; // the plate is sized to the text, so it never needs to shrink
            label.fontSize = refSize.x * 0.05f;
            float maxW = refSize.x * 0.8f;
            var size = label.GetPreferredValues(h.text, maxW, 0f);
            var plateSize = new Vector2(Mathf.Min(size.x, maxW) + refSize.x * 0.1f, size.y + refSize.x * 0.07f);
            Place(plate.rectTransform, new Vector2(0.5f, 0.5f), touch + new Vector2(0, refSize.x * side), plateSize);
            Stretch(label.rectTransform, refSize.x * 0.05f, 0, refSize.x * 0.05f, 0);
            hint.Label = label;
            foreach (var g in root.GetComponentsInChildren<Graphic>(true)) g.raycastTarget = false;
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
            var feedback = music.gameObject.AddComponent<CollectionFeedback>();
            feedback.Click = Sound("Click");
            feedback.Win = Sound("Atchive");
            feedback.Lose = Sound("Wrong");
            feedback.Unlock = Sound("Upgrade_1");
            feedback.Gems = Sound("Coin");
        }

        static AudioClip Sound(string name) => AssetDatabase.LoadAssetAtPath<AudioClip>($"{SoundsDir}/{name}.wav");

        // kit sound actions that the shared music / CollectionFeedback now play for every game: left on, they double up
        static readonly HashSet<string> KitSoundsReplaced = new() { "Background Music", "Button Click Sound", "OnLose Sound", "OnWin Sound Main" };

        /// <summary> Switches off the kit's own music, click and win/lose sounds (Cubey, Splat, the block puzzles). </summary>
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
                    var name = it.FindPropertyRelative("ActionName");
                    var enabled = it.FindPropertyRelative("Enabled");
                    if (name == null || enabled == null || !KitSoundsReplaced.Contains(name.stringValue) || !enabled.boolValue) continue;
                    enabled.boolValue = false;
                    changed = true;
                    Debug.Log($"[AMO] kit sound '{name.stringValue}' switched off on {mb.name}");
                }
                if (changed) so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
