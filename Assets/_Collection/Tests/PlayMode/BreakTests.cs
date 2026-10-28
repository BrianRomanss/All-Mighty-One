using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static AllMightyOne.Tests.GameDriver;
using Object = UnityEngine.Object;

namespace AllMightyOne.Tests
{
    /// <summary>
    /// Adversarial pass: what a player (or a bad network, a cheater, a broken save) does that the happy-path tests do not.
    /// Every test states the behaviour a release build needs; a failure is a bug report.
    /// </summary>
    public class BreakTests : GameTestBase
    {
        const string Hub = "Assets/_Collection/Scenes/Hub.unity";
        const string Gems = "Currency_Gems";
        const BindingFlags Static = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        static readonly string[] PrefKeys =
        {
            "AMO_Daily_Last", "AMO_Daily_Streak", "AMO_Mis_Day", "AMO_Mis_Game", "AMO_Mis_Played", "AMO_Mis_Runs", "AMO_Mis_Wins",
            "AMO_Mis_Gems", "AMO_Mis_Claimed_0", "AMO_Mis_Claimed_1", "AMO_Mis_Claimed_2", "AMO_Feat_Day", "AMO_Feat_Id", "AMO_HubGems",
            "AMO_FreeGemsAt", "AMO_LastPlayed", "AMO_Ninja_Level",
            "AMO_Unlocked_pistol", "AMO_Unlocked_roads", "AMO_Unlocked_splat", "AMO_Unlocked_bomb", "AMO_Unlocked_adventure", "AMO_Unlocked_ninja",
            "AMO_Tut_squad", "AMO_Tut_crowd", "AMO_Tut_brain", "AMO_Tut_pistol", "AMO_Tut_dodge", "AMO_Tut_roads", "AMO_Tut_bomb",
            "AMO_Tut_ninja", "AMO_Tut_blocks", "AMO_Tut_adventure", "AMO_Tut_cubey", "AMO_Tut_splat",
        };

        static Type HubType => GameType("AllMightyOne.Hub.HubController");
        static Type Clock => GameType("AllMightyOne.Hub.CollectionClock");
        static Type Reward => GameType("AllMightyOne.Hub.DailyReward");
        static Type Ads => GameType("AllMightyOne.AdsBridge");
        static Type AdService => GameType("AllMightyOne.UnityAdsService");

        (string key, bool has, int i, string s)[] _prefs;
        object _db;
        readonly Dictionary<string, int> _dbInts = new();
        GameObject _fakeAds;

        // ---------------------------------------------------------------- state guard

        [UnitySetUp]
        public IEnumerator SaveState()
        {
            _prefs = PrefKeys.Select(k => (k, PlayerPrefs.HasKey(k), PlayerPrefs.GetInt(k, 0), PlayerPrefs.GetString(k, ""))).ToArray();
            foreach (var k in PrefKeys.Where(k => k.StartsWith("AMO_Tut_"))) PlayerPrefs.SetInt(k, 1); // no hint hands in the way
            HubType.GetField("s_dailyOffered", Static).SetValue(null, true);
            yield return LoadScene(Hub);
            _db = Get<object>(Catalog, "Database");
            foreach (var name in new[] { Gems, "CurrentLevel_Runner", "CurrentLevel_Count", "CurrentLevel_BigBrain", "CurrentLevel_Pistol",
                         "CurrentLevel_Cubey", "CurrentLevel_Splat", "CurrentLevel_BlockAdventure" })
                try { _dbInts[name] = GetInt(_db, name); } catch { /* not every game keeps its level in the database */ }
        }

        [UnityTearDown]
        public IEnumerator RestoreState()
        {
            RemoveFakeAds();
            Clock.GetField("Now").SetValue(null, (Func<DateTime>)(() => DateTime.Now));
            foreach (var (name, value) in _dbInts) SetInt(_db, name, value);
            Call(_db, "SaveToJson");
            foreach (var (key, has, i, s) in _prefs)
            {
                if (!has) PlayerPrefs.DeleteKey(key);
                else if (s != "") PlayerPrefs.SetString(key, s);
                else PlayerPrefs.SetInt(key, i);
            }
            PlayerPrefs.Save();
            yield return null;
        }

        static Component HubController => Find("AllMightyOne.Hub.HubController");
        static object Catalog => HubController ? Get<object>(HubController, "Catalog") : null;
        int GemCount { get => GetInt(_db, Gems); set => SetInt(_db, Gems, value); }

        static object Entry(string id) => Call(Catalog ?? FindCatalog(), "Find", id);
        static int Plays(object entry) => (int)GameType("AllMightyOne.Hub.GameCatalog").GetMethod("Plays").Invoke(null, new[] { entry });

        static int LevelEnds => (int)Ads.GetProperty("LevelEndCount").GetValue(null);

        void SetNow(DateTime t) => Clock.GetField("Now").SetValue(null, (Func<DateTime>)(() => t));

        /// <summary> Ad network with nothing to show (no fill / offline): rewarded and interstitials are never ready. </summary>
        void InstallNoFillAds()
        {
            // a configured build touches the SDK at launch, which creates its helper object then (not while a scene closes)
            GameType("UnityEngine.Advertisements.Advertisement").GetProperty("isSupported").GetValue(null);
            _fakeAds = new GameObject("Fake No-Fill Ads");
            Object.DontDestroyOnLoad(_fakeAds);
            var service = _fakeAds.AddComponent(AdService);
            AdService.GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { service });
        }

        void RemoveFakeAds()
        {
            if (!_fakeAds) return;
            AdService.GetProperty("Instance").GetSetMethod(true).Invoke(null, new object[] { null });
            Object.Destroy(_fakeAds);
        }

        static IEnumerator OpenGame(string scene)
        {
            yield return LoadScene(scene);
            yield return new WaitForSeconds(0.3f);
        }

        // the menu's start button, whatever the game calls it (Block Adventure has none: its level starts by itself)
        static UnityEngine.UI.Button StartButton => Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None)
            .FirstOrDefault(b => b.isActiveAndEnabled && b.name.Replace(" ", "").StartsWith("StartGame"));

        static void Start() { if (StartButton) StartButton.onClick.Invoke(); }

        // ================================================================ hub economy

        [UnityTest]
        public IEnumerator Hub_DoubleTapOnAGameCardStartsItOnce()
        {
            var squad = Entry("squad");
            int plays = Plays(squad);
            Call(HubController, "Play", squad);
            Call(HubController, "Play", squad);
            Assert.AreEqual(plays + 1, Plays(squad), "Two taps counted two plays");
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == "EpicRunner", 5f, "the game scene");
            yield return new WaitForSeconds(0.6f);
            Assert.AreEqual(1, Object.FindObjectsByType<Component>(FindObjectsSortMode.None).Count(c => c.GetType().Name == "SceneFader"), "Duplicate scene faders");
        }

        [UnityTest]
        public IEnumerator Hub_DoubleTapOnUnlockChargesOnce()
        {
            PlayerPrefs.DeleteKey("AMO_Unlocked_pistol");
            var pistol = Entry("pistol");
            int cost = Get<int>(pistol, "UnlockCost");
            GemCount = cost + 5;
            Call(HubController, "OnCardClicked", pistol);
            yield return null;
            var unlock = Get<UnityEngine.UI.Button>(HubController, "UnlockButton");
            unlock.onClick.Invoke();
            unlock.onClick.Invoke();
            yield return null;
            Assert.AreEqual(5, GemCount, "Unlock charged twice (or not at all)");
            Assert.IsTrue((bool)Call(Catalog, "IsUnlocked", pistol));
        }

        [UnityTest]
        public IEnumerator Hub_UnlockWithTooFewGemsIsRefused()
        {
            PlayerPrefs.DeleteKey("AMO_Unlocked_bomb");
            var bomb = Entry("bomb");
            GemCount = Get<int>(bomb, "UnlockCost") - 1;
            int before = GemCount;
            Call(HubController, "OnCardClicked", bomb);
            yield return null;
            Get<UnityEngine.UI.Button>(HubController, "UnlockButton").onClick.Invoke(); // a tap that slips through
            Assert.IsFalse((bool)Call(Catalog, "IsUnlocked", bomb), "Unlocked without enough gems");
            Assert.AreEqual(before, GemCount);
        }

        [UnityTest]
        public IEnumerator Hub_DailyRewardPaysOncePerDay()
        {
            SetNow(new DateTime(2026, 5, 1, 10, 0, 0));
            PlayerPrefs.DeleteKey("AMO_Daily_Last");
            PlayerPrefs.DeleteKey("AMO_Daily_Streak");
            GemCount = 0;
            Call(HubController, "ClaimDaily", 1);
            Call(HubController, "ClaimDaily", 1);
            Get<UnityEngine.UI.Button>(HubController, "DailyDoubleButton").onClick.Invoke(); // x2 after claiming
            yield return null;
            Assert.AreEqual(20, GemCount, "The day-1 reward was paid more than once");
        }

        [UnityTest]
        public IEnumerator Hub_TurningTheClockBackDoesNotPayTheDailyRewardAgain()
        {
            SetNow(new DateTime(2026, 5, 10, 10, 0, 0));
            PlayerPrefs.DeleteKey("AMO_Daily_Last");
            PlayerPrefs.DeleteKey("AMO_Daily_Streak");
            GemCount = 0;
            Call(HubController, "ClaimDaily", 1);
            int afterToday = GemCount;
            for (int d = 1; d <= 5; d++) // the player sets the phone's date back, one day at a time
            {
                SetNow(new DateTime(2026, 5, 10 - d, 10, 0, 0));
                Call(HubController, "ClaimDaily", 1);
            }
            yield return null;
            Assert.AreEqual(afterToday, GemCount, $"Moving the date back paid {GemCount - afterToday} extra gems");
        }

        [UnityTest]
        public IEnumerator Hub_FreeGemsCooldownSurvivesAClockChange()
        {
            // the player watched the free-gems ad with the phone's clock a day ahead, then fixed the clock
            PlayerPrefs.SetString("AMO_FreeGemsAt", DateTime.UtcNow.AddDays(1).Ticks.ToString());
            float wait = Get<float>(HubController, "FreeGemsWait");
            float cooldown = Get<float>(HubController, "FreeGemsCooldown");
            yield return null;
            Assert.LessOrEqual(wait, cooldown, $"Free gems locked for {wait / 3600f:0.0} hours (cooldown is {cooldown}s)");
        }

        [UnityTest]
        public IEnumerator Hub_FreeGemsDoubleTapPaysOnce()
        {
            PlayerPrefs.DeleteKey("AMO_FreeGemsAt");
            GemCount = 0;
            Call(HubController, "OpenFreeGems");
            var watch = Get<UnityEngine.UI.Button>(HubController, "FreeGemsWatchButton");
            watch.onClick.Invoke();
            watch.onClick.Invoke();
            yield return null;
            Assert.AreEqual(Get<int>(HubController, "FreeGemsAmount"), GemCount);
        }

        [UnityTest]
        public IEnumerator Hub_MissionRewardPaysOnce()
        {
            SetNow(new DateTime(2026, 5, 20, 10, 0, 0));
            Call(HubController, "OpenMissions"); // rolls the day
            PlayerPrefs.SetInt("AMO_Mis_Runs", 50);
            PlayerPrefs.SetInt("AMO_Mis_Wins", 50);
            PlayerPrefs.SetInt("AMO_Mis_Gems", 500);
            PlayerPrefs.SetString("AMO_Mis_Played", "squad,crowd,brain,cubey,blocks,dodge");
            Call(HubController, "OpenMissions");
            yield return null;
            GemCount = 0;
            var row = ((Array)Get<object>(HubController, "MissionRows")).GetValue(2);
            var claim = Get<UnityEngine.UI.Button>(row, "ClaimButton");
            claim.onClick.Invoke();
            claim.onClick.Invoke();
            yield return null;
            Assert.AreEqual(40, GemCount, "The earn-gems mission paid more than once");
        }

        [UnityTest]
        public IEnumerator Hub_HugeWalletDoesNotWrapNegative()
        {
            SetNow(new DateTime(2026, 6, 1, 10, 0, 0));
            PlayerPrefs.DeleteKey("AMO_Daily_Last");
            GemCount = int.MaxValue - 5;
            Call(HubController, "ClaimDaily", 1);
            yield return null;
            Assert.Greater(GemCount, 0, $"Wallet overflowed to {GemCount}");
        }

        // ================================================================ ads that do not show

        [UnityTest]
        public IEnumerator Ads_NoFillFreeGemsKeepsTheCooldownFree()
        {
            InstallNoFillAds();
            PlayerPrefs.DeleteKey("AMO_FreeGemsAt");
            GemCount = 0;
            Call(HubController, "OpenFreeGems");
            Get<UnityEngine.UI.Button>(HubController, "FreeGemsWatchButton").onClick.Invoke();
            yield return null;
            Assert.AreEqual(0, GemCount, "Gems paid without an ad");
            Assert.AreEqual(0f, Get<float>(HubController, "FreeGemsWait"), "Cooldown started although no ad was shown");
            Assert.IsTrue(Get<bool>(Get<object>(HubController, "FreeGemsPopup"), "IsOpen"));
        }

        [UnityTest]
        public IEnumerator Ads_NoFillReviveLeavesTheLoseScreenUsable()
        {
            InstallNoFillAds();
            yield return OpenGame("Assets/Most In One/Spawn/Scenes/Explosion.unity");
            var game = Find("AllMightyOne.Bomb.BombSurvival");
            var gm = Find("Solo.MOST_IN_ONE.UniversalGameManager");
            Start();
            yield return new WaitForSeconds(0.5f);
            Call(Get<Component>(game, "Player"), "OnDamage", 1000f);
            yield return WaitUntil(() => FindObject("Lose UI").activeInHierarchy, 6f, "lose panel");
            Click("Revive Button");
            yield return null;
            Assert.IsTrue(Get<bool>(gm, "IsGameOver"), "Revived without an ad");
            Assert.IsTrue(FindObject("Lose UI").activeInHierarchy, "The lose screen closed although nothing happened");
            Assert.IsTrue(FindObject("Retry Button").activeInHierarchy, "No way to carry on without the ad");
        }

        [UnityTest]
        public IEnumerator Ads_NoFillInterstitialDoesNotBlockLevelFlow()
        {
            InstallNoFillAds();
            Ads.GetField("FirstSessionGraceSeconds").SetValue(null, 0f);
            Ads.GetField("MinSecondsBetweenInterstitials").SetValue(null, 0f);
            try
            {
                yield return OpenGame("Assets/Most In One/Runner/Scenes/BigBrain.unity");
                var gm = Find("Solo.MOST_IN_ONE.UniversalGameManager");
                for (int i = 0; i < 4; i++) // four level ends: two interstitial attempts with nothing loaded
                {
                    Start();
                    yield return new WaitForSeconds(0.3f);
                    Call(Find("Solo.MOST_IN_ONE.CharacterControl_BigBrain"), "InstantAddScale", -1000f);
                    yield return WaitUntil(() => FindObject("Lose UI").activeInHierarchy, 6f, "lose panel " + i);
                    Click("Continue Button Lose"); // Big Brain's "tap to continue" back to the menu
                    yield return WaitUntil(() => FindObject("StartGame Button") && FindObject("StartGame Button").activeInHierarchy, 6f, "menu after retry " + i);
                }
            }
            finally
            {
                Ads.GetField("FirstSessionGraceSeconds").SetValue(null, 180f);
                Ads.GetField("MinSecondsBetweenInterstitials").SetValue(null, 45f);
            }
        }

        // ================================================================ in-game abuse

        static readonly (string id, string scene, string kill)[] Deaths =
        {
            ("squad", "Assets/Most In One/Runner/Scenes/EpicRunner.unity", "squad"),
            ("crowd", "Assets/Most In One/Runner/Scenes/Count.unity", "crowd"),
            ("brain", "Assets/Most In One/Runner/Scenes/BigBrain.unity", "brain"),
            ("pistol", "Assets/Most In One/Runner/Scenes/Pistol.unity", "pistol"),
            ("bomb", "Assets/Most In One/Spawn/Scenes/Explosion.unity", "bomb"),
            ("ninja", "Assets/Most In One/Controls/Scenes/Ninja.unity", "ninja"),
        };

        static IEnumerable<string> DeathIds => Deaths.Select(d => d.id);

        static void Kill(string how)
        {
            switch (how)
            {
                case "squad": KillHolder("Solo.MOST_IN_ONE.CharacterControl_ShootRunner", "Solo.MOST_IN_ONE.CharacterControl_ShootRunner_Child"); break;
                case "crowd": KillHolder("Solo.MOST_IN_ONE.CharacterControl_Count", "Solo.MOST_IN_ONE.CharacterControl_Count_Child"); break;
                case "brain": Call(Find("Solo.MOST_IN_ONE.CharacterControl_BigBrain"), "InstantAddScale", -1000f); break;
                case "pistol": Call(Get<Component>(Find("Solo.MOST_IN_ONE.CharacterControl_Gun"), "DamageSystem"), "OnDamage", 100000f); break;
                case "bomb": Call(Get<Component>(Find("AllMightyOne.Bomb.BombSurvival"), "Player"), "OnDamage", 1000f); break;
                case "ninja": Call(Find("AllMightyOne.Ninja.NinjaArena"), "TakeDamage", 1000f); break;
            }
        }

        static void KillHolder(string runner, string child)
        {
            var holder = Get<Transform>(Find(runner), "ChildHolder");
            for (int i = holder.childCount - 1; i >= 0; i--)
                Call(holder.GetChild(i).GetComponent(GameType(child)), "DestroyChild");
        }

        [UnityTest]
        public IEnumerator Game_DoubleTapOnReviveRevivesOnce([ValueSource(nameof(DeathIds))] string id)
        {
            var (_, scene, kill) = Deaths.First(d => d.id == id);
            yield return OpenGame(scene);
            var gm = Find("Solo.MOST_IN_ONE.UniversalGameManager");
            Start();
            yield return new WaitForSeconds(0.6f);
            int ends = LevelEnds;
            Kill(kill);
            yield return WaitUntil(() => FindObject("Lose UI").activeInHierarchy, 6f, "lose panel");
            Assert.Greater(LevelEnds, ends, "Dying did not reach the level-end ad");

            var revive = FindObject("Revive Button").GetComponent<UnityEngine.UI.Button>();
            revive.onClick.Invoke();
            revive.onClick.Invoke();
            yield return new WaitForSeconds(0.2f);
            Assert.IsFalse(Get<bool>(gm, "IsGameOver"), "Not revived");
            Assert.AreEqual(1, Get<int>(gm, "_revivesUsed"), "Two taps used two revives");
            Assert.IsFalse(FindObject("Lose UI").activeInHierarchy, "Lose screen still up after revive");
        }

        [UnityTest]
        public IEnumerator Game_DoubleTapOnStartStartsOneRun([ValueSource(nameof(DeathIds))] string id)
        {
            var (_, scene, _) = Deaths.First(d => d.id == id);
            yield return OpenGame(scene);
            int starts = 0;
            void Count(string m, string s, LogType t) { if (m.StartsWith("[Analytics] level_start")) starts++; }
            Application.logMessageReceived += Count;
            try
            {
                // a quick double tap: the second tap lands a frame later, if the button can still take it
                var start = StartButton;
                start.onClick.Invoke();
                yield return null;
                if (start && start.isActiveAndEnabled && start.interactable && start.GetComponentInParent<CanvasGroup>()?.interactable != false)
                    start.onClick.Invoke();
                yield return new WaitForSeconds(1f);
            }
            finally { Application.logMessageReceived -= Count; }
            Assert.AreEqual(1, starts, $"{starts} runs started from a double tap");
        }

        static IEnumerable<string> LevelGames => new[] { "squad", "crowd", "brain", "pistol", "cubey", "splat", "adventure", "ninja" };

        [UnityTest]
        public IEnumerator Game_CorruptLevelNumbersStillLoadALevel([ValueSource(nameof(LevelGames))] string id,
            [Values(0, -7, 5000, int.MaxValue)] int level)
        {
            var entry = Entry(id);
            string key = Get<string>(entry, "ProgressKey");
            bool prefs = Get<object>(entry, "Progress").ToString() == "PrefsLevel";
            if (prefs) PlayerPrefs.SetInt(key, level);
            else
            {
                if (!_dbInts.ContainsKey(key)) _dbInts[key] = GetInt(_db, key);
                SetInt(_db, key, level);
            }
            string scene = Get<string>(entry, "SceneName");
            SceneManager.LoadScene(scene);
            yield return new WaitForSeconds(1f);
            var gm = Find("Solo.MOST_IN_ONE.UniversalGameManager");
            Assert.GreaterOrEqual(Get<int>(gm, "CurrentLevel"), 1);
            Start();
            yield return new WaitForSeconds(1f);
            // the hub card must still read sensibly
            string text = (string)Call(FindCatalog(), "ProgressText", entry);
            Assert.IsFalse(text.Contains("-"), $"Hub card shows '{text}'");
        }

        static object FindCatalog() => Resources.FindObjectsOfTypeAll(GameType("AllMightyOne.Hub.GameCatalog")).First();

        // ================================================================ navigation

        [UnityTest]
        public IEnumerator Nav_HoppingThroughEveryGameLeavesNoDuplicates()
        {
            var games = (Array)Get<object>(Catalog, "Games");
            foreach (var g in games)
            {
                string id = Get<string>(g, "Id");
                PlayerPrefs.SetInt("AMO_Unlocked_" + id, 1);
                Call(HubController, "Play", g);
                string scene = Get<string>(g, "SceneName");
                yield return WaitUntil(() => SceneManager.GetActiveScene().name == scene && !SceneFaderBusy, 8f, "scene " + scene);
                yield return new WaitForSeconds(0.3f);
                Check($"in {id}");
                Call(Find("AllMightyOne.Hub.HubReturn"), "GoHome");
                yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Hub" && !SceneFaderBusy, 8f, "hub from " + id);
                yield return null;
                Check($"back from {id}");
            }

            static void Check(string where)
            {
                Assert.AreEqual(1f, Time.timeScale, $"timeScale {Time.timeScale} {where}");
                Assert.AreEqual(1, Count("CollectionMusic"), $"music players {where}");
                Assert.AreEqual(1, Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(l => l.isActiveAndEnabled), $"audio listeners {where}");
                Assert.LessOrEqual(Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None).Length, 1, $"event systems {where}");
            }
            static int Count(string type) => Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Count(c => c.GetType().Name == type);
        }

        static bool SceneFaderBusy => (bool)GameType("AllMightyOne.SceneFader").GetProperty("IsLoading").GetValue(null);

        [UnityTest]
        public IEnumerator Nav_PlayerCanLeaveARunInProgress([ValueSource(nameof(DeathIds))] string id)
        {
            var (_, scene, _) = Deaths.First(d => d.id == id);
            yield return OpenGame(scene);
            Start();
            yield return new WaitForSeconds(0.6f);
            var home = FindObject("Hub Home Button");
            bool homeShown = home && home.activeInHierarchy;
            bool pause = Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None)
                .Any(b => b.isActiveAndEnabled && b.name.IndexOf("Pause", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.IsTrue(homeShown || pause, "During a run there is no home or pause button, and the back key is ignored");
        }

        // ================================================================ saves

        [UnityTest]
        public IEnumerator Save_CorruptSaveFileDoesNotWipeProgress()
        {
            var store = _db.GetType().GetProperty("SavePath", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            bool internalStorage = Get<bool>(_db, "UseInternalStorage");
            string key = (string)_db.GetType().GetProperty("ValuesPrefsKey", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(_db);
            string path = store?.GetValue(_db) as string;
            GemCount = 4321;
            Call(_db, "SaveToJson");
            string backup = internalStorage ? PlayerPrefs.GetString(key) : System.IO.File.ReadAllText(path);
            try
            {
                // a write cut off half way (phone died, storage full)
                string broken = backup.Substring(0, backup.Length / 2);
                if (internalStorage) PlayerPrefs.SetString(key, broken); else System.IO.File.WriteAllText(path, broken);
                Call(_db, "LoadFromJson");
                yield return null;
                Assert.AreEqual(4321, GemCount, "A damaged save reset the wallet (and every level) to the starting values");
            }
            finally
            {
                if (internalStorage) PlayerPrefs.SetString(key, backup); else System.IO.File.WriteAllText(path, backup);
                Call(_db, "LoadFromJson");
            }
        }

        // ================================================================ screens

        /// <summary> Not a check: hub and game menus on a 3:4 tablet and a 20:9 phone, to Logs/BreakShots. </summary>
        [UnityTest]
        public IEnumerator Screens_TabletAndTallPhone()
        {
            string dir = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", "Logs", "BreakShots"));
            System.IO.Directory.CreateDirectory(dir);
#if UNITY_EDITOR
            UnityEditor.ShaderUtil.allowAsyncCompilation = false;
#endif
            foreach (var (w, h, tag) in new[] { (1536, 2048, "tablet"), (1080, 2400, "tall") })
            {
                yield return LoadScene(Hub);
                yield return new WaitForSeconds(1.5f);
                yield return HubSnapshots.CaptureView($"hub_{tag}", w, h, dir);
                foreach (var (id, scene) in new[] { ("squad", "Assets/Most In One/Runner/Scenes/EpicRunner.unity"),
                             ("blocks", "Assets/Most In One/Grid/Scene/BlockBlast Classic.unity"), ("dodge", "Assets/Most In One/Dodge/Scenes/Dodge.unity"),
                             ("cubey", "Assets/Most In One/Grid/Scene/Cubey.unity") })
                {
                    yield return LoadScene(scene);
                    yield return new WaitForSeconds(1f);
                    yield return HubSnapshots.CaptureView($"{id}_{tag}", w, h, dir);
                }
            }
        }
    }
}
