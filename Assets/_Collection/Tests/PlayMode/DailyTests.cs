using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static AllMightyOne.Tests.GameDriver;

namespace AllMightyOne.Tests
{
    /// <summary> Phase C: the 7-day reward, the daily missions and the game of the day, with the clock moved by hand. </summary>
    public class DailyTests : GameTestBase
    {
        const string Hub = "Assets/_Collection/Scenes/Hub.unity";
        const string Gems = "Currency_Gems";

        static readonly string[] Keys =
        {
            "AMO_Daily_Last", "AMO_Daily_Streak", "AMO_Mis_Day", "AMO_Mis_Game", "AMO_Mis_Played", "AMO_Mis_Runs", "AMO_Mis_Wins",
            "AMO_Mis_Gems", "AMO_Mis_Claimed_0", "AMO_Mis_Claimed_1", "AMO_Mis_Claimed_2", "AMO_Feat_Day", "AMO_Feat_Id", "AMO_HubGems",
        };

        static Type Clock => GameType("AllMightyOne.Hub.CollectionClock");
        static Type Reward => GameType("AllMightyOne.Hub.DailyReward");
        static Type Missions => GameType("AllMightyOne.Hub.DailyMissions");
        static Type HubType => GameType("AllMightyOne.Hub.HubController");

        (string key, bool has, int i, string s)[] _saved;
        object _db;
        int _savedGems;
        DateTime _now;

        [SetUp]
        public void SaveState()
        {
            _saved = Keys.Select(k => (k, PlayerPrefs.HasKey(k), PlayerPrefs.GetInt(k, 0), PlayerPrefs.GetString(k, ""))).ToArray();
            foreach (var k in Keys) PlayerPrefs.DeleteKey(k);
            SetDay(new DateTime(2026, 3, 10, 15, 0, 0));
            // a fresh app session: no game played yet, the daily popup not offered yet
            HubType.GetField("s_playedThisSession", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, false);
            HubType.GetField("s_dailyOffered", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, false);
        }

        [TearDown]
        public void RestoreState()
        {
            Clock.GetField("Now").SetValue(null, (Func<DateTime>)(() => DateTime.Now));
            if (_db != null)
            {
                SetInt(_db, Gems, _savedGems);
                Call(_db, "SaveToJson");
            }
            foreach (var (key, has, i, s) in _saved)
            {
                if (!has) PlayerPrefs.DeleteKey(key);
                else if (key is "AMO_Mis_Game" or "AMO_Mis_Played" or "AMO_Feat_Id") PlayerPrefs.SetString(key, s);
                else PlayerPrefs.SetInt(key, i);
            }
            PlayerPrefs.Save();
        }

        void SetDay(DateTime when)
        {
            _now = when;
            Clock.GetField("Now").SetValue(null, (Func<DateTime>)(() => _now));
        }

        void NextDay(int days = 1) => SetDay(_now.AddDays(days));

        static bool Available => (bool)Reward.GetProperty("Available").GetValue(null);
        static int Claim(int multiplier = 1) => (int)Reward.GetMethod("Claim").Invoke(null, new object[] { multiplier });

        IEnumerator OpenHub()
        {
            yield return LoadScene(Hub);
            var hub = Find("AllMightyOne.Hub.HubController");
            if (_db == null)
            {
                _db = Get<object>(Get<object>(hub, "Catalog"), "Database");
                _savedGems = GetInt(_db, Gems);
            }
        }

        [Test]
        public void RewardWalksSevenDaysAndResetsAfterAMissedDay()
        {
            Assert.IsTrue(Available);
            Assert.AreEqual(20, Claim());
            Assert.IsFalse(Available, "claimable twice in one day");
            Assert.AreEqual(0, Claim());

            int[] expected = { 30, 40, 50, 60, 80, 150, 20 }; // days 2-7, then the cycle starts again
            foreach (int amount in expected)
            {
                NextDay();
                Assert.AreEqual(amount, Claim());
            }

            NextDay(2); // skipped a day: back to day 1
            Assert.AreEqual(0, (int)Reward.GetProperty("Day").GetValue(null));
            Assert.AreEqual(40, Claim(2), "watching the ad doubles the reward");
        }

        [UnityTest]
        public IEnumerator DailyPopupOpensOnceAndPaysOut()
        {
            yield return OpenHub();
            var hub = Find("AllMightyOne.Hub.HubController");
            int gems = GetInt(_db, Gems);
            yield return new WaitForSeconds(1.2f);
            Assert.IsTrue(Get<bool>(Get<Component>(hub, "DailyPopup"), "IsOpen"), "the daily reward did not open at the start of the session");
            Assert.IsTrue(Get<GameObject>(hub, "DailyBadge").activeSelf);

            Click("Daily Claim Button");
            yield return null;
            Assert.AreEqual(gems + 20, GetInt(_db, Gems), "day 1 should pay 20 gems");
            Assert.IsFalse(Available);
            Assert.IsFalse(Get<GameObject>(hub, "DailyBadge").activeSelf, "the red dot stayed after claiming");
            Assert.IsTrue(Get<Component>(hub, "DailyNextText").gameObject.activeSelf, "no 'come back tomorrow' after claiming");

            yield return OpenHub(); // same session, same day: no popup again
            yield return new WaitForSeconds(1.2f);
            Assert.IsFalse(Get<bool>(Get<Component>(Find("AllMightyOne.Hub.HubController"), "DailyPopup"), "IsOpen"));
        }

        [UnityTest]
        public IEnumerator MissionsFillUpPayOnceAndRenewTheNextDay()
        {
            yield return OpenHub();
            var hub = Find("AllMightyOne.Hub.HubController");
            var catalog = Get<object>(hub, "Catalog");
            var today = (Array)Missions.GetMethod("Today").Invoke(null, new[] { catalog });
            Assert.AreEqual(3, today.Length);
            Assert.IsTrue(today.Cast<object>().All(m => Get<int>(m, "Progress") == 0), "missions start at zero");
            var texts = today.Cast<object>().Select(m => Get<string>(m, "Text")).ToArray();
            var again = ((Array)Missions.GetMethod("Today").Invoke(null, new[] { catalog })).Cast<object>().Select(m => Get<string>(m, "Text"));
            CollectionAssert.AreEqual(texts, again, "missions changed within the same day");

            // do everything any mission could ask for
            foreach (var m in today.Cast<object>().Where(m => Get<object>(m, "Kind").ToString() == "PlayGame"))
                Missions.GetMethod("GameStarted").Invoke(null, new object[] { Get<string>(m, "GameId") });
            foreach (var id in new[] { "squad", "crowd", "brain" }) Missions.GetMethod("GameStarted").Invoke(null, new object[] { id });
            for (int i = 0; i < 5; i++) Missions.GetMethod("RunEnded").Invoke(null, new object[] { true });
            Missions.GetMethod("GemsEarned").Invoke(null, new object[] { 60 });

            int gems = GetInt(_db, Gems);
            Call(hub, "OpenMissions");
            yield return new WaitForSeconds(0.4f);
            var rows = (Array)Get<object>(hub, "MissionRows");
            int expected = 0;
            foreach (Component row in rows)
            {
                var m = Get<object>(row, "Mission");
                Assert.IsTrue(Get<bool>(m, "Done"), $"'{Get<string>(m, "Text")}' not finished");
                Assert.IsTrue(Get<Component>(row, "ClaimButton").GetComponent<UnityEngine.UI.Button>().interactable);
                expected += Get<int>(m, "Reward");
                Call(hub, "ClaimMission", row);
            }
            yield return null;
            Assert.AreEqual(gems + expected, GetInt(_db, Gems), "mission rewards not paid");
            foreach (Component row in rows)
            {
                Assert.IsTrue(Get<GameObject>(row, "DoneMark").activeSelf, "claimed mission shows no tick");
                Call(hub, "ClaimMission", row); // a second claim pays nothing
            }
            Assert.AreEqual(gems + expected, GetInt(_db, Gems), "a mission paid twice");
            Assert.IsFalse(Get<GameObject>(hub, "MissionsBadge").activeSelf, "red dot left with nothing to claim");

            NextDay();
            var tomorrow = (Array)Missions.GetMethod("Today").Invoke(null, new[] { catalog });
            Assert.IsTrue(tomorrow.Cast<object>().All(m => Get<int>(m, "Progress") == 0 && !Get<bool>(m, "Claimed")), "yesterday's progress carried over");
        }

        [UnityTest]
        public IEnumerator GameOfTheDayStaysForTheDayAndRotates()
        {
            yield return OpenHub();
            var hub = Find("AllMightyOne.Hub.HubController");
            var catalog = Get<object>(hub, "Catalog");
            Assert.AreEqual("GAME OF THE DAY", Get<string>(Get<Component>(hub, "FeaturedLabel"), "text"));
            var pick = Get<object>(Get<Component>(hub, "Featured"), "Entry");
            Assert.IsTrue((bool)Call(catalog, "IsUnlocked", pick), "the game of the day is locked");

            yield return OpenHub(); // same day: same game
            Assert.AreSame(pick, Get<object>(Get<Component>(Find("AllMightyOne.Hub.HubController"), "Featured"), "Entry"));

            var picks = new System.Collections.Generic.HashSet<string>();
            for (int d = 0; d < 6; d++)
            {
                NextDay();
                picks.Add(Get<string>(Call(catalog, "GameOfTheDay"), "Id"));
            }
            Assert.Greater(picks.Count, 1, "the game of the day never changes");
        }
    }
}
