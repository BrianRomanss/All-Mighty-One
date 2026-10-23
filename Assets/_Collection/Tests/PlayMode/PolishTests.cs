using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static AllMightyOne.Tests.GameDriver;

namespace AllMightyOne.Tests
{
    /// <summary> Phase B polish: first-play hints, shared click/win/lose feedback, gems flying into the hub wallet. </summary>
    public class PolishTests : GameTestBase
    {
        const string Hub = "Assets/_Collection/Scenes/Hub.unity";
        const string Roads = "Assets/Most In One/ThreeRoads/Scene/3Road.unity";
        const string Blocks = "Assets/Most In One/Grid/Scene/BlockBlast Classic.unity";
        const string Gems = "Currency_Gems";

        static Component Hint => Find("AllMightyOne.Hub.FirstPlayHint");
        static bool Showing => Get<bool>(Hint, "IsShowing");

        static IEnumerator WithPref(string key, System.Func<IEnumerator> body)
        {
            bool had = PlayerPrefs.HasKey(key);
            int value = PlayerPrefs.GetInt(key, 0);
            PlayerPrefs.DeleteKey(key);
            try { yield return body(); }
            finally
            {
                if (had) PlayerPrefs.SetInt(key, value); else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
        }

        [UnityTest]
        public IEnumerator HintShowsOnceWhenTheFirstRunStarts() => WithPref("AMO_Tut_roads", Roads_);

        IEnumerator Roads_()
        {
            yield return LoadScene(Roads);
            Assert.IsNotNull(Hint, "3 Roads has no first-play hint");
            Assert.IsFalse(Showing, "the hint showed on the menu, before the run");
            var gm = Find("Solo.MOST_IN_ONE.UniversalGameManager");
            Call(gm, "OnStart");
            yield return new WaitForSeconds(0.5f);
            Assert.IsTrue(Showing, "the hint did not show when the run started");
            Assert.Greater(Get<float>(Get<Component>(Hint, "Group"), "alpha"), 0.5f, "the hint stayed invisible");
            Assert.AreEqual("TAP TO SWITCH SIDES", Get<string>(Get<Component>(Hint, "Label"), "text"));

            yield return new WaitForSeconds(5f); // nobody touches the screen: it times out
            Assert.IsFalse(Hint.gameObject.activeSelf, "the hint never went away");
            Assert.AreEqual(1, PlayerPrefs.GetInt("AMO_Tut_roads"), "the hint was not remembered");

            yield return LoadScene(Roads);
            Call(Find("Solo.MOST_IN_ONE.UniversalGameManager"), "OnStart");
            yield return new WaitForSeconds(0.3f);
            Assert.IsFalse(Hint.gameObject.activeInHierarchy, "the hint showed a second time");
        }

        [UnityTest]
        public IEnumerator HintShowsRightAwayInBlockPuzzle() => WithPref("AMO_Tut_blocks", Blocks_);

        IEnumerator Blocks_()
        {
            yield return LoadScene(Blocks);
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(Showing, "Block Puzzle starts without a menu: the hint should show at once");
            var hand = Get<RectTransform>(Hint, "Hand");
            float y0 = hand.anchoredPosition.y;
            yield return new WaitForSeconds(0.4f);
            Assert.Greater(hand.anchoredPosition.y, y0 + 20f, "the hand does not drag up towards the board");
            foreach (var g in Hint.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                Assert.IsFalse(g.raycastTarget, $"{g.name} would block touches meant for the game");
        }

        [UnityTest]
        public IEnumerator ButtonsClickAndRunsEndWithACue()
        {
            yield return LoadScene(Hub);
            yield return null;
            yield return null; // buttons are hooked a frame after the scene starts
            var feedback = Find("AllMightyOne.CollectionFeedback");
            Assert.IsNotNull(feedback, "no feedback player next to the music");
            var sources = feedback.GetComponents<AudioSource>();
            var ui = sources.First(s => s.outputAudioMixerGroup && s.outputAudioMixerGroup.name == "UI");
            var sfx = sources.First(s => s.outputAudioMixerGroup && s.outputAudioMixerGroup.name == "SFX");

            Click("Settings Button");
            yield return null;
            Assert.IsTrue(ui.isPlaying, "a hub button made no click sound");

            var type = GameType("AllMightyOne.CollectionFeedback");
            type.GetMethod("RunEnded").Invoke(null, new object[] { false });
            yield return null;
            Assert.IsTrue(sfx.isPlaying, "losing a run made no sound");
        }

        [UnityTest]
        public IEnumerator GemsWonInAGameFlyIntoTheWallet()
        {
            yield return LoadScene(Hub);
            var hub = Find("AllMightyOne.Hub.HubController");
            var db = Get<object>(Get<object>(hub, "Catalog"), "Database");
            int gems = GetInt(db, Gems);
            bool had = PlayerPrefs.HasKey("AMO_HubGems");
            try
            {
                yield return LoadScene(Roads); // leaving the hub saves what it showed
                PlayerPrefs.SetInt("AMO_HubGems", gems - 40); // as if this game paid out 40 since the hub was last seen
                yield return LoadScene(Hub);
                hub = Find("AllMightyOne.Hub.HubController");
                var text = Get<Component>(hub, "GemText");
                Assert.AreEqual((gems - 40).ToString("N0"), Get<string>(text, "text"), "the wallet should start at the old balance");
                yield return new WaitForSeconds(0.6f);
                Assert.Greater(Resources.FindObjectsOfTypeAll<GameObject>().Count(g => g.scene.IsValid() && g.name == "Flying Gem"), 0, "no gems flew in");
                yield return new WaitForSeconds(2.5f);
                Assert.AreEqual(0, Resources.FindObjectsOfTypeAll<GameObject>().Count(g => g.scene.IsValid() && g.name == "Flying Gem"), "flying gems were left behind");
                Assert.AreEqual(gems.ToString("N0"), Get<string>(text, "text"), "the wallet did not count up to the balance");
                Assert.AreEqual(gems, PlayerPrefs.GetInt("AMO_HubGems"));

                yield return LoadScene(Hub); // nothing new: no gems fly the next time
                yield return new WaitForSeconds(0.6f);
                Assert.AreEqual(0, Resources.FindObjectsOfTypeAll<GameObject>().Count(g => g.scene.IsValid() && g.name == "Flying Gem"));
            }
            finally
            {
                if (!had) PlayerPrefs.DeleteKey("AMO_HubGems");
                PlayerPrefs.Save();
            }
        }
    }
}
