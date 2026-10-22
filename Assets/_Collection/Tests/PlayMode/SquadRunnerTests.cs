using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static AllMightyOne.Tests.GameDriver;

namespace AllMightyOne.Tests
{
    public class SquadRunnerTests : GameTestBase
    {
        const string Scene = "Assets/Most In One/Runner/Scenes/EpicRunner.unity";
        const string Gems = "Currency_Gems";
        static readonly string[] MetaKeys = { "AMO_Squad_squad", "AMO_Squad_tier" };

        Component _gm, _runner;
        object _db;
        int _savedGems, _savedLevel;
        readonly int[] _savedMeta = new int[2];
        readonly bool[] _hadMeta = new bool[2];

        IEnumerator Setup(bool resetMeta = false)
        {
            for (int i = 0; i < 2; i++) { _hadMeta[i] = PlayerPrefs.HasKey(MetaKeys[i]); _savedMeta[i] = PlayerPrefs.GetInt(MetaKeys[i]); }
            if (resetMeta) foreach (var k in MetaKeys) PlayerPrefs.DeleteKey(k);
            yield return LoadScene(Scene);
            Bind();
            _db = Get<object>(_gm, "DatabaseHolder");
            _savedGems = GetInt(_db, Gems);
            _savedLevel = Get<int>(_gm, "CurrentLevel");
        }

        void Bind()
        {
            _gm = Find("Solo.MOST_IN_ONE.UniversalGameManager");
            _runner = Find("Solo.MOST_IN_ONE.CharacterControl_ShootRunner");
        }

        void Restore()
        {
            SetInt(_db, Gems, _savedGems);
            SetInt(_db, "CurrentLevel_EpicRunner", _savedLevel);
            Call(_db, "SaveToJson");
            for (int i = 0; i < 2; i++)
                if (_hadMeta[i]) PlayerPrefs.SetInt(MetaKeys[i], _savedMeta[i]); else PlayerPrefs.DeleteKey(MetaKeys[i]);
            Time.timeScale = 1f;
        }

        Transform Holder => Get<Transform>(_runner, "ChildHolder");

        // keeps every soldier immune to damage (soldiers spawn during the run, so this repeats)
        IEnumerator KeepInvincible()
        {
            while (true)
            {
                if (_runner)
                    foreach (Transform c in Holder)
                    {
                        var d = c.GetComponent(GameType("Solo.MOST_IN_ONE.MOST_Damage"));
                        if (d) { Set(d, "UseOnTrigger", false); Set(d, "UseOnCollision", false); }
                    }
                yield return null;
            }
        }

        static readonly int[] Levels = { 1, 4, 15, 30 };

        [UnityTest]
        public IEnumerator InvincibleSquadFinishesLevelAndTriplesGems([ValueSource(nameof(Levels))] int level)
        {
            yield return Setup();
            try
            {
                if (level != _savedLevel)
                {
                    Call(_gm, "LoadLevel", level);
                    yield return new WaitForSeconds(1f);
                    Bind();
                }
                Assert.AreEqual(level, Get<int>(_gm, "CurrentLevel"));
                Click("StartGame Button");
                var guard = new GameObject("guard").AddComponent<GuardRunner>();
                guard.StartCoroutine(KeepInvincible());
                Time.timeScale = 3f;

                yield return WaitUntil(() => Get<bool>(_gm, "IsGameOver"), 120f, "the run to end");
                Debug.Log($"[AMO] squad level {level}: soldiers={Holder.childCount} z={_runner.transform.position.z:0.0} endline={Get<bool>(_runner, "ReachedEndLine")}");
                Assert.AreEqual(level + 1, Get<int>(_gm, "CurrentLevel"), $"Level {level} not won");

                var end = Find("Solo.MOST_IN_ONE.EndGameUIHandler");
                yield return WaitUntil(() => Get<bool>(end, "CountFinished"), 30f, "gem count-up");
                int earned = Get<int>(end, "LastEarned");
                Debug.Log($"[AMO] squad level {level} earned {earned} gems");
                if (earned > 0)
                {
                    int before = GetInt(_db, Gems);
                    Click("Triple Reward Button");
                    Assert.AreEqual(before + 2 * earned, GetInt(_db, Gems), "x3 did not add twice the earned gems");
                }
                Object.Destroy(guard.gameObject);
            }
            finally { Restore(); }
        }

        [UnityTest]
        public IEnumerator SquadWipedOffersOneRevive()
        {
            yield return Setup();
            try
            {
                Click("StartGame Button");
                yield return new WaitForSeconds(0.5f);
                KillSquad();
                yield return WaitUntil(() => FindObject("Lose UI").activeInHierarchy, 6f, "lose panel");
                Assert.IsTrue(FindObject("Revive Button").activeInHierarchy, "Revive not offered");

                Click("Revive Button");
                yield return null;
                yield return null;
                Assert.IsFalse(Get<bool>(_gm, "IsGameOver"), "Still game over after revive");
                Assert.GreaterOrEqual(Holder.childCount, 3, "Revive did not respawn the squad");
                Assert.IsTrue(Get<bool>(_runner, "IsActive"), "Squad not running after revive");
                Assert.IsFalse(FindObject("Lose UI").activeInHierarchy);

                yield return new WaitForSeconds(2.5f);
                KillSquad();
                yield return WaitUntil(() => FindObject("Lose UI").activeInHierarchy, 6f, "second lose panel");
                Assert.IsFalse(FindObject("Revive Button").activeInHierarchy, "Revive offered twice");
            }
            finally { Restore(); }
        }

        void KillSquad()
        {
            var holder = Holder;
            for (int i = holder.childCount - 1; i >= 0; i--)
                Call(holder.GetChild(i).GetComponent(GameType("Solo.MOST_IN_ONE.CharacterControl_ShootRunner_Child")), "DestroyChild");
        }

        [UnityTest]
        public IEnumerator UpgradeShopBuysSquadAndPowerOrGivesThemForAnAd()
        {
            yield return Setup(resetMeta: true);
            try
            {
                SetInt(_db, Gems, 10000);
                var shop = Find("AllMightyOne.Runner.RunnerUpgradeShop");
                Call(shop, "Refresh");
                int soldiers = Holder.childCount;

                Click("Upgrade_Squad_Button");
                yield return null;
                Assert.AreEqual(10000 - 40, GetInt(_db, Gems), "Squad upgrade should cost 40");
                Assert.AreEqual(soldiers + 1, Holder.childCount, "Squad did not grow on the menu");

                Click("Upgrade_Power_Button");
                yield return null;
                Assert.AreEqual(2, Get<int>(_runner, "CurrentLevel"), "Power did not raise the starting tier");
                Assert.AreEqual(10000 - 40 - 300, GetInt(_db, Gems));

                SetInt(_db, Gems, 0);
                Click("Upgrade_Squad_Button"); // placeholder rewarded ad grants it
                yield return null;
                Assert.AreEqual(3, PlayerPrefs.GetInt("AMO_Squad_squad"), "Free upgrade not applied");
                Assert.AreEqual(0, GetInt(_db, Gems));

                // the next run starts with the upgraded squad
                yield return LoadScene(Scene);
                Bind();
                Assert.AreEqual(3, Holder.childCount, "Upgraded starting squad not applied on load");
                Assert.AreEqual(2, Get<int>(_runner, "CurrentLevel"), "Upgraded tier not applied on load");
            }
            finally { Restore(); }
        }
    }

    public class GuardRunner : MonoBehaviour { }
}
