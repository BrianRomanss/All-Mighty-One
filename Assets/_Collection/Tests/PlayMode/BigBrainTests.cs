using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static AllMightyOne.Tests.GameDriver;

namespace AllMightyOne.Tests
{
    public class BigBrainTests : GameTestBase
    {
        const string Scene = "Assets/Most In One/Runner/Scenes/BigBrain.unity";
        const string Gems = "Currency_Gems";
        const string BrainKey = "AMO_BigBrain_brain";

        Component _gm, _runner;
        object _db;
        int _savedGems, _savedLevel, _savedBrain;
        bool _hadBrain;

        IEnumerator Setup(bool resetMeta = false)
        {
            _hadBrain = PlayerPrefs.HasKey(BrainKey);
            _savedBrain = PlayerPrefs.GetInt(BrainKey);
            if (resetMeta) PlayerPrefs.DeleteKey(BrainKey);
            yield return LoadScene(Scene);
            Bind();
            _db = Get<object>(_gm, "DatabaseHolder");
            _savedGems = GetInt(_db, Gems);
            _savedLevel = Get<int>(_gm, "CurrentLevel");
        }

        void Bind()
        {
            _gm = Find("Solo.MOST_IN_ONE.UniversalGameManager");
            _runner = Find("Solo.MOST_IN_ONE.CharacterControl_BigBrain");
        }

        void Restore()
        {
            SetInt(_db, Gems, _savedGems);
            SetInt(_db, "CurrentLevel_BigBrain", _savedLevel);
            Call(_db, "SaveToJson");
            if (_hadBrain) PlayerPrefs.SetInt(BrainKey, _savedBrain); else PlayerPrefs.DeleteKey(BrainKey);
            Time.timeScale = 1f;
        }

        float Head => Get<float>(_runner, "HeadScale");

        static readonly int[] Levels = { 1, 4, 15, 30 };

        [UnityTest]
        public IEnumerator BigHeadReachesStairsAndTriplesGems([ValueSource(nameof(Levels))] int level)
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
                Assert.IsNotNull(GameObject.Find($"BigBrain_Level_{level}(Clone)"), "Level prefab not instantiated");

                // the bot cannot steer: switch off the obstacles (tag Enemy) a player would dodge or pay for
                foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
                    if (c.CompareTag("Enemy")) c.enabled = false;
                Call(_runner, "SetHeadScale", 12f);
                Assert.AreEqual(12f, Head, 0.01f, "Head start not applied");
                Click("StartGame Button");
                Time.timeScale = 3f;
                float nextLog = 0f;
                yield return WaitUntil(() =>
                {
                    if (Time.time >= nextLog)
                    {
                        nextLog = Time.time + 1f;
                        Debug.Log($"[AMO] t={Time.time:0} z={_runner.transform.position.z:0.0} head={Head:0.##} end={Get<bool>(_runner, "ReachedEndLine")}");
                    }
                    return Get<bool>(_gm, "IsGameOver");
                }, 120f, "the run to end");
                Debug.Log($"[AMO] big brain level {level}: won={Get<int>(_gm, "CurrentLevel") == level + 1} z={_runner.transform.position.z:0.0}");
                Assert.AreEqual(level + 1, Get<int>(_gm, "CurrentLevel"), $"Level {level} not won");

                var end = Find("Solo.MOST_IN_ONE.EndGameUIHandler");
                yield return WaitUntil(() => Get<bool>(end, "CountFinished"), 40f, "gem count-up");
                int earned = Get<int>(end, "LastEarned");
                Debug.Log($"[AMO] big brain level {level} earned {earned} gems");
                Assert.Greater(earned, 0, "Stairs multiplier paid nothing");
                int before = GetInt(_db, Gems);
                Click("Triple Reward Button");
                Assert.AreEqual(before + 2 * earned, GetInt(_db, Gems));
            }
            finally { Restore(); }
        }

        [UnityTest]
        public IEnumerator HeadGoneOffersOneRevive()
        {
            yield return Setup();
            try
            {
                Click("StartGame Button");
                yield return new WaitForSeconds(1f);
                float z = _runner.transform.position.z;
                Call(_runner, "InstantAddScale", -1000f);
                Assert.IsTrue(Get<bool>(_runner, "IsDefeated"));
                yield return WaitUntil(() => FindObject("Lose UI").activeInHierarchy, 6f, "lose panel");
                Assert.IsTrue(FindObject("Revive Button").activeInHierarchy, "Revive not offered");

                Click("Revive Button");
                yield return null;
                yield return null;
                Assert.IsFalse(Get<bool>(_gm, "IsGameOver"));
                Assert.IsFalse(Get<bool>(_runner, "IsDefeated"));
                Assert.GreaterOrEqual(Head, 2.5f, "Revive did not give the head back");
                Assert.IsTrue(Get<bool>(_runner, "IsActive"));
                Assert.IsFalse(FindObject("Lose UI").activeInHierarchy);
                Assert.AreEqual(z, _runner.transform.position.z, 1.5f, "Revive should continue where the head ran out");

                yield return new WaitForSeconds(1f);
                Assert.Greater(_runner.transform.position.z, z + 2f, "Character does not run after the revive");
                Call(_runner, "InstantAddScale", -1000f);
                yield return WaitUntil(() => FindObject("Lose UI").activeInHierarchy, 6f, "second lose panel");
                Assert.IsFalse(FindObject("Revive Button").activeInHierarchy, "Revive offered twice");
            }
            finally { Restore(); }
        }

        [UnityTest]
        public IEnumerator BrainUpgradeAndRewardedBiggerStart()
        {
            yield return Setup(resetMeta: true);
            try
            {
                yield return null;
                Assert.AreEqual(2.5f, Head, 0.01f);
                SetInt(_db, Gems, 10000);
                Call(Find("AllMightyOne.Runner.RunnerUpgradeShop"), "Refresh");
                Click("Upgrade_Brain_Button");
                yield return null;
                Assert.AreEqual(10000 - 50, GetInt(_db, Gems), "Brain upgrade should cost 50");
                Assert.AreEqual(2.75f, Head, 0.01f, "Head did not grow on the menu");

                SetInt(_db, Gems, 0);
                Click("Upgrade_Brain_Button"); // free with the placeholder rewarded ad
                yield return null;
                Assert.AreEqual(3, PlayerPrefs.GetInt(BrainKey));
                Assert.AreEqual(3f, Head, 0.01f);

                Click("Bigger Start Button");
                yield return null;
                Assert.AreEqual(5f, Head, 0.01f, "Bigger start did not add 2");
                Assert.IsFalse(FindObject("Bigger Start Button").activeInHierarchy, "Bigger start should be one-time");

                yield return LoadScene(Scene);
                Bind();
                Assert.AreEqual(3f, Head, 0.01f, "Upgraded starting head not applied on load");
            }
            finally { Restore(); }
        }
    }
}
