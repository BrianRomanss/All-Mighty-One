using System.Linq;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static AllMightyOne.Tests.GameDriver;

namespace AllMightyOne.Tests
{
    /// <summary>
    /// Not a check: renders the hub screens to Logs/HubShots/*.png (phone resolution) for visual review.
    /// Run explicitly: -testFilter AllMightyOne.Tests.HubSnapshots
    /// </summary>
    [Explicit]
    public class HubSnapshots
    {
        const string Scene = "Assets/_Collection/Scenes/Hub.unity";
        static string Dir => Path.Combine(Application.dataPath, "..", "Logs", "HubShots"); // Temp is wiped when Unity quits

        [UnityTest]
        public IEnumerator Capture()
        {
            Directory.CreateDirectory(Dir);
            // today's reward counts as claimed for the first shots, so the daily popup doesn't open over the hub
            const string last = "AMO_Daily_Last", streak = "AMO_Daily_Streak";
            bool hadLast = PlayerPrefs.HasKey(last), hadStreak = PlayerPrefs.HasKey(streak);
            int oldLast = PlayerPrefs.GetInt(last), oldStreak = PlayerPrefs.GetInt(streak);
            int today = (int)GameType("AllMightyOne.Hub.CollectionClock").GetProperty("Today").GetValue(null);
            PlayerPrefs.SetInt(last, today);
            PlayerPrefs.SetInt(streak, 1);
            yield return LoadScene(Scene);
            yield return new WaitForSeconds(1.5f);
            var hub = Find("AllMightyOne.Hub.HubController");
            // diagnostics: atlas state of the first label
            var tmpType = GameType("TMPro.TMP_Text");
            var label = (Component)Object.FindFirstObjectByType(tmpType);
            var font = Get<object>(label, "font");
            var atlas = Get<Texture2D>(font, "atlasTexture");
            var mat = Get<Material>(label, "fontSharedMaterial");
            Debug.Log($"[AMO] text '{Get<string>(label, "text")}' atlas={atlas?.width}x{atlas?.height} matTex={mat.mainTexture?.width} same={mat.mainTexture == atlas} shader={mat.shader.name} glyphs={((System.Collections.ICollection)Get<object>(font, "glyphTable")).Count}");
            if (atlas && atlas.isReadable) File.WriteAllBytes(Path.Combine(Dir, "atlas.png"), atlas.EncodeToPNG());
            Debug.Log($"[AMO] atlas format={atlas?.format} readable={atlas?.isReadable} spread={Get<object>(font, "atlasPadding")} renderMode={Get<object>(font, "atlasRenderMode")}");
            yield return Shot("1_hub");

            Call(hub, "SelectTab", 3);
            yield return new WaitForSeconds(0.8f);
            yield return Shot("2_tab_puzzle");
            Call(hub, "SelectTab", 0);

            var catalog = Get<object>(hub, "Catalog");
            var games = (System.Array)Get<object>(catalog, "Games");
            object locked = null;
            foreach (var g in games) if (!(bool)Call(catalog, "IsUnlocked", g)) { locked = g; break; }
            Call(hub, "OnCardClicked", locked);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("3_unlock");
            Call(hub, "CloseTopPopup");
            yield return new WaitForSeconds(0.5f);

            foreach (var (method, name) in new[] { ("OpenFreeGems", "4_free_gems"), ("OpenSettings", "5_settings"), ("OpenNoAds", "6_no_ads") })
            {
                Call(hub, method);
                yield return new WaitForSeconds(0.6f);
                yield return Shot(name);
                Call(hub, "CloseTopPopup");
                yield return new WaitForSeconds(0.5f);
            }
            // day 3 of a streak, ready to claim; then the missions
            PlayerPrefs.SetInt(last, today - 1);
            PlayerPrefs.SetInt(streak, 2);
            Call(hub, "OpenDaily");
            yield return new WaitForSeconds(0.6f);
            yield return Shot("7_daily");
            Call(hub, "CloseTopPopup");
            yield return new WaitForSeconds(0.5f);
            Call(hub, "OpenMissions");
            yield return new WaitForSeconds(0.6f);
            yield return Shot("8_missions");
            Call(hub, "CloseTopPopup");

            if (hadLast) PlayerPrefs.SetInt(last, oldLast); else PlayerPrefs.DeleteKey(last);
            if (hadStreak) PlayerPrefs.SetInt(streak, oldStreak); else PlayerPrefs.DeleteKey(streak);
            PlayerPrefs.Save();
            Assert.Pass($"Screens in {Path.GetFullPath(Dir)}");
        }

        static readonly string[] GameScenes =
        {
            "Assets/Most In One/Dodge/Scenes/Dodge.unity", "Assets/Most In One/Runner/Scenes/Count.unity",
            "Assets/Most In One/Controls/Scenes/Ninja.unity", "Assets/Most In One/ThreeRoads/Scene/3Road.unity",
        };

        static readonly (string id, string scene)[] Thumbs =
        {
            ("squad", "Assets/Most In One/Runner/Scenes/EpicRunner.unity"), ("crowd", "Assets/Most In One/Runner/Scenes/Count.unity"),
            ("brain", "Assets/Most In One/Runner/Scenes/BigBrain.unity"), ("pistol", "Assets/Most In One/Runner/Scenes/Pistol.unity"),
            ("dodge", "Assets/Most In One/Dodge/Scenes/Dodge.unity"), ("roads", "Assets/Most In One/ThreeRoads/Scene/3Road.unity"),
            ("bomb", "Assets/Most In One/Spawn/Scenes/Explosion.unity"), ("ninja", "Assets/Most In One/Controls/Scenes/Ninja.unity"),
            ("blocks", "Assets/Most In One/Grid/Scene/BlockBlast Classic.unity"), ("adventure", "Assets/Most In One/Grid/Scene/BlockBlast Adventure.unity"),
            ("cubey", "Assets/Most In One/Grid/Scene/Cubey.unity"), ("splat", "Assets/Most In One/Grid/Scene/Splat.unity"),
        };

        /// <summary> Square card art captured from every game's own camera into Art/Hub/Thumbs (placeholder until real icons). </summary>
        [UnityTest]
        public IEnumerator CaptureThumbnails()
        {
            string dir = Path.Combine(Application.dataPath, "_Collection", "Art", "Hub", "Thumbs");
            Directory.CreateDirectory(dir);
#if UNITY_EDITOR
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
#endif
            foreach (var (id, path) in Thumbs)
            {
                yield return LoadScene(path);
                yield return new WaitForSeconds(1.2f);
                var cam = Camera.main;
                var rt = new RenderTexture(720, 720, 24) { antiAliasing = 4 };
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                RenderTexture.active = null;
                cam.targetTexture = null;
                File.WriteAllBytes(Path.Combine(dir, id + ".png"), tex.EncodeToPNG());
            }
        }

        /// <summary> The first-play hint a moment after the run starts, in a few games with different gestures. </summary>
        [UnityTest]
        public IEnumerator CaptureFirstPlayHints()
        {
            Directory.CreateDirectory(Dir);
#if UNITY_EDITOR
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
#endif
            foreach (var (id, path) in Thumbs.Where(t => t.Item1 is "roads" or "cubey" or "dodge" or "pistol" or "adventure"))
            {
                string key = "AMO_Tut_" + id;
                bool had = PlayerPrefs.HasKey(key);
                int value = PlayerPrefs.GetInt(key);
                PlayerPrefs.DeleteKey(key);
                yield return LoadScene(path);
                yield return new WaitForSeconds(0.5f);
                // press the menu's start button like a player (it also closes the menu); no menu: the hint is already up
                var start = Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None)
                    .FirstOrDefault(b => b.isActiveAndEnabled && b.name.Replace(" ", "").StartsWith("StartGame"));
                if (start) start.onClick.Invoke();
                yield return new WaitForSeconds(0.7f);
                yield return CaptureView("hint_" + id);
                if (had) PlayerPrefs.SetInt(key, value); else PlayerPrefs.DeleteKey(key);
            }
            PlayerPrefs.Save();
        }

        /// <summary> Each game's menu as the player sees it (camera + UI), to check the hub home button placement. </summary>
        [UnityTest]
        public IEnumerator CaptureGameMenus()
        {
            Directory.CreateDirectory(Dir);
#if UNITY_EDITOR
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
#endif
            foreach (var (id, path) in Thumbs)
            {
                yield return LoadScene(path);
                yield return new WaitForSeconds(1.2f);
                yield return CaptureView("menu_" + id);
            }
        }

        /// <summary> What each game's own main camera shows (3D only), to spot broken materials. </summary>
        [UnityTest]
        public IEnumerator CaptureGameCameras()
        {
            Directory.CreateDirectory(Dir);
#if UNITY_EDITOR
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
#endif
            foreach (var path in GameScenes)
            {
                yield return LoadScene(path);
                yield return new WaitForSeconds(1f);
                var cam = Camera.main;
                var rt = new RenderTexture(540, 1170, 24);
                cam.targetTexture = rt;
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                RenderTexture.active = null;
                cam.targetTexture = null;
                File.WriteAllBytes(Path.Combine(Dir, "cam_" + Path.GetFileNameWithoutExtension(path) + ".png"), tex.EncodeToPNG());
            }
        }

        static readonly string StoreDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Logs", "StoreShots"));

        /// <summary>
        /// Raw Google Play screenshots (1080x1920): the hub, then a few seconds into a run of each showcase game.
        /// ArtPrompts/store_frames.py adds the captions.
        /// </summary>
        [UnityTest]
        public IEnumerator CaptureStoreShots()
        {
            Directory.CreateDirectory(StoreDir);
#if UNITY_EDITOR
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
#endif
            // the hub as a returning player sees it: no daily-reward popup on top
            GameType("AllMightyOne.Hub.HubController").GetField("s_dailyOffered", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic).SetValue(null, true);
            yield return LoadScene(Scene);
            yield return new WaitForSeconds(2.5f); // hub intro animations
            yield return Shot("1_hub", 1080, 1920, StoreDir);

            // seconds of play before the shot: short enough that the unsteered runners have not hit a gate yet
            var games = new[] { ("2_squad", "squad", 3.5f), ("3_blocks", "blocks", 0.5f), ("4_cubey", "cubey", 1.5f), ("5_crowd", "crowd", 0.3f), ("6_dodge", "dodge", 3f), ("7_roads", "roads", 3f), ("8_bomb", "bomb", 3f) };
            foreach (var (file, id, play) in games)
            {
                string key = "AMO_Tut_" + id;
                bool had = PlayerPrefs.HasKey(key);
                int value = PlayerPrefs.GetInt(key);
                PlayerPrefs.SetInt(key, 1); // no first-play hand over the gameplay
                yield return LoadScene(Thumbs.First(t => t.id == id).scene);
                yield return new WaitForSeconds(0.5f);
                var start = Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None)
                    .FirstOrDefault(b => b.isActiveAndEnabled && b.name.Replace(" ", "").StartsWith("StartGame"));
                if (start) start.onClick.Invoke();
                if (id == "blocks") // a board with some pieces on it
                {
                    var game = Find("Solo.MOST_IN_ONE.BlockBlastGame");
                    for (int i = 0; i < 9; i++) { Call(game, "AutoPlaceBest"); yield return new WaitForSeconds(0.35f); }
                }
                yield return new WaitForSeconds(play);
                yield return CaptureView(file, 1080, 1920, StoreDir);
                if (had) PlayerPrefs.SetInt(key, value); else PlayerPrefs.DeleteKey(key);
            }
            PlayerPrefs.Save();
            Assert.Pass($"Store shots in {StoreDir}");
        }

        // the main camera's view with every overlay canvas drawn on top, as on the device
        internal static IEnumerator CaptureView(string file, int w = 540, int h = 1170, string dir = null)
        {
            var cam = Camera.main;
            var rt = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            var overlays = new System.Collections.Generic.List<(Canvas canvas, int order)>();
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    overlays.Add((c, c.sortingOrder));
                    c.renderMode = RenderMode.ScreenSpaceCamera;
                    c.worldCamera = cam;
                    c.planeDistance = cam.nearClipPlane + 0.05f;
                    c.sortingOrder = 1000; // an overlay draws above everything: keep transparent particles (3 Roads' clouds) under it
                }
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            yield return null;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            RenderTexture.active = null;
            cam.targetTexture = null;
            foreach (var (c, order) in overlays) { c.renderMode = RenderMode.ScreenSpaceOverlay; c.worldCamera = null; c.sortingOrder = order; }
            File.WriteAllBytes(Path.Combine(dir ?? Dir, file + ".png"), tex.EncodeToPNG());
        }

        // renders the overlay canvas through a camera into a 1080x2340 (19.5:9) texture
        internal static IEnumerator Shot(string name, int w = 1080, int h = 2340, string dir = null)
        {
#if UNITY_EDITOR
            UnityEditor.ShaderUtil.allowAsyncCompilation = false; // otherwise unready shaders render as blocks / magenta
#endif
            yield return null;
            var canvas = Object.FindFirstObjectByType<Canvas>();
            var rt = new RenderTexture(w, h, 24);
            var cam = new GameObject("Snapshot Camera").AddComponent<Camera>();
            cam.targetTexture = rt;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            var mode = canvas.renderMode;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 10;
            Canvas.ForceUpdateCanvases();
            yield return null;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(Path.Combine(dir ?? Dir, name + ".png"), tex.EncodeToPNG());
            canvas.renderMode = mode;
            canvas.worldCamera = null;
            Object.Destroy(cam.gameObject);
            Object.Destroy(rt);
            Object.Destroy(tex);
        }
    }
}
