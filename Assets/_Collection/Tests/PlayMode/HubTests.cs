using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static AllMightyOne.Tests.GameDriver;

namespace AllMightyOne.Tests
{
    public class HubTests : GameTestBase
    {
        const string Scene = "Assets/_Collection/Scenes/Hub.unity";
        const string Gems = "Currency_Gems";
        static readonly string[] Ids = { "squad", "crowd", "brain", "pistol", "dodge", "roads", "bomb", "ninja", "blocks", "adventure", "cubey", "splat" };

        Component _hub;
        object _catalog, _db;
        int _savedGems;
        (string key, bool has, int i, string s)[] _prefs;

        IEnumerator Setup()
        {
            // keys the hub writes: saved here and restored after each test
            var keys = Ids.SelectMany(id => new[] { "AMO_Unlocked_" + id, "AMO_Played_" + id })
                .Concat(new[] { "AMO_LastPlayed", "AMO_FreeGemsAt", "AMO_NoAds" }).ToArray();
            _prefs = keys.Select(k => (k, PlayerPrefs.HasKey(k), PlayerPrefs.GetInt(k, 0), PlayerPrefs.GetString(k, ""))).ToArray();
            foreach (var id in Ids) PlayerPrefs.DeleteKey("AMO_Unlocked_" + id);
            PlayerPrefs.DeleteKey("AMO_FreeGemsAt");
            PlayerPrefs.SetInt("AMO_NoAds", 0);
            yield return LoadScene(Scene);
            Bind();
            _db = Get<object>(_catalog, "Database");
            _savedGems = GetInt(_db, Gems);
        }

        void Bind()
        {
            _hub = Find("AllMightyOne.Hub.HubController");
            _catalog = Get<object>(_hub, "Catalog");
        }

        void Restore()
        {
            SetInt(_db, Gems, _savedGems);
            Call(_db, "SaveToJson");
            foreach (var (key, has, i, s) in _prefs)
            {
                if (!has) PlayerPrefs.DeleteKey(key);
                else if (key == "AMO_LastPlayed" || key == "AMO_FreeGemsAt") PlayerPrefs.SetString(key, s);
                else PlayerPrefs.SetInt(key, i);
            }
            PlayerPrefs.Save();
        }

        Component Card(string id) => FindObject("Card " + id).GetComponent(GameType("AllMightyOne.Hub.HubCard"));
        bool Locked(string id) => Get<GameObject>(Card(id), "LockRoot").activeSelf;
        bool PopupOpen(string field) => Get<bool>(Get<Component>(_hub, field), "IsOpen");
        string Text(Component c, string field) => Get<string>(Get<Component>(c, field), "text");

        [UnityTest]
        public IEnumerator ShowsEveryGameWithProgressLocksAndTabs()
        {
            yield return Setup();
            try
            {
                SetInt(_db, Gems, 1234);
                yield return new WaitForSeconds(1.5f); // wallet roll-up
                Assert.AreEqual("1,234", Text(_hub, "GemText"));
                foreach (var id in Ids) Assert.IsNotNull(FindObject("Card " + id), $"No card for {id}");
                var lockedIds = Ids.Where(Locked).ToArray();
                Assert.AreEqual(new[] { "pistol", "roads", "bomb", "ninja", "adventure", "splat" }, lockedIds);
                Assert.AreEqual("150", Text(Card("roads"), "CostText"));
                StringAssert.StartsWith("LEVEL ", Text(Card("cubey"), "Progress"));

                int Visible() => Ids.Count(id => FindObject("Card " + id).activeSelf);
                Call(_hub, "SelectTab", 1);
                Assert.AreEqual(4, Visible(), "Runner tab");
                Assert.IsTrue(FindObject("Card squad").activeSelf);
                Call(_hub, "SelectTab", 3);
                Assert.AreEqual(4, Visible(), "Puzzle tab");
                Assert.IsTrue(FindObject("Card splat").activeSelf);
                Assert.IsFalse(FindObject("Card squad").activeSelf);
                Call(_hub, "SelectTab", 0);
                Assert.AreEqual(12, Visible());
            }
            finally { Restore(); }
        }

        [UnityTest]
        public IEnumerator UnlockWithGemsOrGetFreeGemsFirst()
        {
            yield return Setup();
            try
            {
                SetInt(_db, Gems, 60);
                Click("Card roads"); // 150 gems
                yield return null;
                Assert.IsTrue(PopupOpen("UnlockPopup"));
                Assert.IsFalse(Get<UnityEngine.UI.Button>(_hub, "UnlockButton").interactable, "Cannot afford it yet");
                Assert.AreEqual("You need 90 more gems", Text(_hub, "UnlockMessage"));
                Assert.IsTrue(FindObject("Get Gems Button").activeInHierarchy);

                Click("Get Gems Button");
                yield return null;
                Assert.IsTrue(PopupOpen("FreeGemsPopup"));
                Click("Watch Button"); // placeholder rewarded ad
                Assert.AreEqual(85, GetInt(_db, Gems), "+25 free gems");
                Assert.Greater(Get<float>(_hub, "FreeGemsWait"), 170f, "Free gems should have a cooldown");
                Call(_hub, "OpenFreeGems");
                yield return null;
                Click("Watch Button");
                Assert.AreEqual(85, GetInt(_db, Gems), "No second reward during the cooldown");
                Call(_hub, "CloseTopPopup");

                SetInt(_db, Gems, 1000);
                Click("Card roads");
                yield return null;
                Assert.IsTrue(Get<UnityEngine.UI.Button>(_hub, "UnlockButton").interactable);
                Click("Unlock Button");
                yield return new WaitForSeconds(0.4f);
                Assert.AreEqual(850, GetInt(_db, Gems));
                Assert.IsFalse(Locked("roads"), "Card still locked");
                Assert.AreEqual(1, PlayerPrefs.GetInt("AMO_Unlocked_roads"));
                Assert.IsFalse(PopupOpen("UnlockPopup"));
                Assert.IsTrue(Get<GameObject>(Card("roads"), "NewBadge").activeSelf, "Freshly unlocked game should be marked NEW");
            }
            finally { Restore(); }
        }

        [UnityTest]
        public IEnumerator PlayAGameAndComeBack()
        {
            yield return Setup();
            try
            {
                Click("Card dodge");
                yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Dodge", 10f, "Bull Dodge to load");
                Assert.AreEqual("dodge", PlayerPrefs.GetString("AMO_LastPlayed"));
                yield return new WaitForSeconds(0.6f);
                Assert.IsTrue(FindObject("Hub Home Button").activeInHierarchy, "Home button missing on the game menu");

                Click("Hub Home Button");
                yield return WaitUntil(() => SceneManager.GetActiveScene().name == "Hub", 10f, "the hub to load");
                yield return new WaitForSeconds(0.6f);
                Bind();
                var featured = Get<Component>(_hub, "Featured");
                Assert.AreEqual("BULL DODGE", Text(featured, "Title"));
                Assert.AreEqual("CONTINUE PLAYING", Text(_hub, "FeaturedLabel"));
                Assert.IsFalse(Get<GameObject>(Card("dodge"), "NewBadge").activeSelf, "A played game is not NEW any more");
            }
            finally { Restore(); }
        }

        [UnityTest]
        public IEnumerator HomeButtonHidesDuringARun()
        {
            yield return Setup();
            try
            {
                yield return LoadScene("Assets/Most In One/Dodge/Scenes/Dodge.unity");
                yield return null;
                Assert.IsTrue(FindObject("Hub Home Button").activeInHierarchy);
                Click("StartGameButton");
                yield return null;
                yield return null;
                Assert.IsFalse(FindObject("Hub Home Button").activeInHierarchy, "Home button shown while playing");
            }
            finally { Restore(); }
        }

        [UnityTest]
        public IEnumerator RemoveAdsAndSettings()
        {
            yield return Setup();
            try
            {
                Assert.IsTrue(FindObject("No Ads Button").activeInHierarchy);
                Assert.IsTrue(FindObject("Banner Space").activeInHierarchy);
                Click("No Ads Button");
                yield return null;
                Assert.IsTrue(PopupOpen("NoAdsPopup"));
                Click("Buy Button"); // placeholder purchase
                yield return new WaitForSeconds(0.4f);
                Assert.AreEqual(1, PlayerPrefs.GetInt("AMO_NoAds"));
                Assert.IsFalse(FindObject("No Ads Button").activeInHierarchy, "Remove Ads offered after buying it");
                Assert.IsFalse(FindObject("Banner Space").activeInHierarchy, "Banner space kept after Remove Ads");

                var audio = Get<object>(_hub, "Audio");
                bool music = Get<bool>(audio, "Music_IsOn");
                Click("Settings Button");
                yield return null;
                Assert.IsTrue(PopupOpen("SettingsPopup"));
                Call(Get<Component>(_hub, "MusicSwitch"), "Toggle");
                Assert.AreEqual(!music, Get<bool>(audio, "Music_IsOn"), "Music switch did not reach the audio settings");
                Call(Get<Component>(_hub, "MusicSwitch"), "Toggle");
                Assert.AreEqual(music, Get<bool>(audio, "Music_IsOn"));
            }
            finally { Restore(); }
        }

        [UnityTest]
        public IEnumerator MusicCarriesIntoGamesAndBack()
        {
            var type = GameType("AllMightyOne.CollectionMusic");
            yield return LoadScene(Scene);
            var music = Object.FindObjectsByType(type, FindObjectsSortMode.None);
            Assert.AreEqual(1, music.Length, "hub should have one music player");
            var source = ((Component)music[0]).GetComponent<AudioSource>();
            Assert.IsTrue(source && source.isPlaying && source.clip, "hub music not playing");
            Assert.IsNotNull(source.outputAudioMixerGroup, "music bypasses the mixer: the Music switch could not mute it");

            // Cubey had its own copy of the loop: entering it must neither restart nor double the music
            yield return new WaitForSeconds(0.5f);
            float time = source.time;
            yield return LoadScene("Assets/Most In One/Grid/Scene/Cubey.unity");
            yield return new WaitForSeconds(0.3f);
            music = Object.FindObjectsByType(type, FindObjectsSortMode.None);
            Assert.AreEqual(1, music.Length, "music player duplicated in a game");
            Assert.AreSame(source, ((Component)music[0]).GetComponent<AudioSource>(), "the hub's player did not survive the scene load");
            Assert.IsTrue(source.isPlaying && source.time > time, "music restarted or stopped on entering a game");
            var playing = Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Count(a => a.isPlaying && a.loop && a.clip == source.clip);
            Assert.AreEqual(1, playing, "the loop is playing twice");

            yield return LoadScene(Scene);
            Assert.AreEqual(1, Object.FindObjectsByType(type, FindObjectsSortMode.None).Length, "music player duplicated back in the hub");
            Assert.IsTrue(source.isPlaying);
        }
    }
}
