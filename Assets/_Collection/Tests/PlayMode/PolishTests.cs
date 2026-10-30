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

        const string Dodge = "Assets/Most In One/Dodge/Scenes/Dodge.unity";
        static System.Type Juice => GameType("AllMightyOne.CollectionJuice");
        static System.Type Feedback => GameType("AllMightyOne.CollectionFeedback");

        static IEnumerator WithMotion(bool reduced, System.Func<IEnumerator> body)
        {
            bool was = (bool)Feedback.GetProperty("ReducedMotion").GetValue(null);
            Feedback.GetProperty("ReducedMotion").SetValue(null, reduced);
            try { yield return body(); }
            finally { Feedback.GetProperty("ReducedMotion").SetValue(null, was); }
        }

        [UnityTest]
        public IEnumerator InGameButtonsSquashWhenHeld()
        {
            yield return LoadScene(Dodge);
            yield return null;
            yield return null; // buttons are hooked a frame after the scene starts
            var press = GameType("AllMightyOne.UiPress");
            int squashing = 0;
            foreach (var b in Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (b.transition == UnityEngine.UI.Selectable.Transition.Animation || b.GetComponentInParent<Animator>(true)) continue;
                Assert.IsNotNull(b.GetComponentInParent(press, true), $"{b.name} does not squash when held");
                squashing++;
            }
            Assert.Greater(squashing, 0, "Bull Dodge has no plain buttons to check");
        }

        [UnityTest]
        public IEnumerator WinConfettiFliesAndClearsUp() => WithMotion(false, Confetti_);

        IEnumerator Confetti_()
        {
            yield return LoadScene(Hub);
            var juice = Find("AllMightyOne.CollectionJuice");
            Assert.IsNotNull(juice, "no juice layer next to the feedback player");
            Juice.GetMethod("Confetti").Invoke(null, null);
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(Get<int>(juice, "ActiveConfetti"), 15, "no confetti in the air after a win");
            yield return new WaitForSeconds(3f);
            Assert.AreEqual(0, Get<int>(juice, "ActiveConfetti"), "confetti was left on screen");
            foreach (var g in juice.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                Assert.IsFalse(g.raycastTarget, $"{g.name} would block touches meant for the game");
        }

        [UnityTest]
        public IEnumerator ReducedMotionStopsConfettiAndSoftensTheFlash() => WithMotion(true, Reduced_);

        IEnumerator Reduced_()
        {
            yield return LoadScene(Hub);
            var juice = Find("AllMightyOne.CollectionJuice");
            Juice.GetMethod("Confetti").Invoke(null, null);
            Feedback.GetMethod("RunEnded").Invoke(null, new object[] { false });
            float peak = 0f;
            for (float t = 0; t < 0.4f; t += Time.unscaledDeltaTime)
            {
                peak = Mathf.Max(peak, Get<float>(juice, "FlashLevel"));
                yield return null;
            }
            Assert.AreEqual(0, Get<int>(juice, "ActiveConfetti"), "confetti flew with reduced motion on");
            Assert.Greater(peak, 0.05f, "losing gave no flash at all");
            Assert.LessOrEqual(peak, Get<float>(juice, "FlashAlpha") * 0.5f + 0.01f, "the flash was not softened");
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(0f, Get<float>(juice, "FlashLevel"), "the flash did not fade out");
        }

        [UnityTest]
        public IEnumerator ReduceMotionSwitchInSettingsSavesTheSetting() => WithMotion(false, Switch_);

        IEnumerator Switch_()
        {
            yield return LoadScene(Hub);
            var hub = Find("AllMightyOne.Hub.HubController");
            var sw = Get<Component>(hub, "ReducedMotionSwitch");
            Assert.IsNotNull(sw, "Settings has no Reduce Motion switch");
            Call(hub, "OpenSettings");
            yield return null;
            Assert.IsFalse(Get<bool>(sw, "IsOn"), "the switch should show the saved setting (off)");
            Call(sw, "Toggle");
            Assert.IsTrue((bool)Feedback.GetProperty("ReducedMotion").GetValue(null), "turning the switch on did not save reduced motion");
            Call(sw, "Toggle");
            Assert.IsFalse((bool)Feedback.GetProperty("ReducedMotion").GetValue(null), "turning the switch off did not clear reduced motion");
        }

        [UnityTest]
        public IEnumerator EndScreenPanelsPopInButBackdropsStay() => WithMotion(false, Pop_);

        IEnumerator Pop_()
        {
            yield return LoadScene(Hub);
            var menu = new GameObject("Test End Screen", typeof(RectTransform), typeof(Canvas));
            var backdrop = new GameObject("Backdrop", typeof(RectTransform)).GetComponent<RectTransform>();
            backdrop.SetParent(menu.transform, false);
            backdrop.anchorMin = Vector2.zero;
            backdrop.anchorMax = Vector2.one;
            var panel = new GameObject("Panel", typeof(RectTransform)).GetComponent<RectTransform>();
            panel.SetParent(backdrop, false);
            var title = new GameObject("Title", typeof(RectTransform)).GetComponent<RectTransform>();
            title.SetParent(menu.transform, false);

            Juice.GetMethod("MenuShown").Invoke(null, new object[] { menu, false });
            yield return null;
            Assert.AreEqual(Vector3.one, backdrop.localScale, "the full-screen backdrop was scaled");
            Assert.Less(panel.localScale.x, 0.99f, "the panel inside the backdrop did not pop");
            Assert.Less(title.localScale.x, 0.99f, "the title did not pop");
            yield return new WaitForSeconds(0.8f);
            Assert.AreEqual(Vector3.one, panel.localScale, "the panel did not settle at its own size");
            Assert.AreEqual(Vector3.one, title.localScale, "the title did not settle at its own size");
            Object.Destroy(menu);
        }

        [UnityTest]
        public IEnumerator GemCounterCountsUpAndBumps() => WithMotion(false, Counter_);

        IEnumerator Counter_()
        {
            yield return LoadScene(Dodge);
            var counter = Find("AllMightyOne.Hub.GemCounter");
            Assert.IsNotNull(counter, "Bull Dodge has no gem pill");
            var db = Get<object>(counter, "Database");
            int gems = GetInt(db, Gems);
            try
            {
                yield return null;
                var text = Get<Component>(counter, "Text");
                Assert.AreEqual(gems.ToString("N0"), Get<string>(text, "text"), "the opening balance should just appear");
                SetInt(db, Gems, gems + 100);
                yield return new WaitForSeconds(0.15f);
                int mid = int.Parse(Get<string>(text, "text").Replace(",", ""));
                Assert.Greater(mid, gems, "the balance did not start counting");
                Assert.Less(mid, gems + 100, "the balance jumped instead of counting up");
                Assert.Greater(counter.transform.localScale.x, 1.01f, "the pill did not bump");
                yield return new WaitForSeconds(0.6f);
                Assert.AreEqual((gems + 100).ToString("N0"), Get<string>(text, "text"));
                Assert.AreEqual(1f, counter.transform.localScale.x, 1e-4f, "the pill stayed big");
            }
            finally
            {
                SetInt(db, Gems, gems);
                Call(db, "SaveToJson");
            }
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
