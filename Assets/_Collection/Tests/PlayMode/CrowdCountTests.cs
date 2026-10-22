using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static AllMightyOne.Tests.GameDriver;

namespace AllMightyOne.Tests
{
    public class CrowdCountTests : GameTestBase
    {
        const string Scene = "Assets/Most In One/Runner/Scenes/Count.unity";
        const string Gems = "Currency_Gems";
        const string CrowdKey = "AMO_Count_crowd";

        Component _gm, _runner;
        object _db;
        int _savedGems, _savedLevel, _savedCrowd;
        bool _hadCrowd;

        IEnumerator Setup(bool resetMeta = false)
        {
            _hadCrowd = PlayerPrefs.HasKey(CrowdKey);
            _savedCrowd = PlayerPrefs.GetInt(CrowdKey);
            if (resetMeta) PlayerPrefs.DeleteKey(CrowdKey);
            yield return LoadScene(Scene);
            Bind();
            _db = Get<object>(_gm, "DatabaseHolder");
            _savedGems = GetInt(_db, Gems);
            _savedLevel = Get<int>(_gm, "CurrentLevel");
        }

        void Bind()
        {
            _gm = Find("Solo.MOST_IN_ONE.UniversalGameManager");
            _runner = Find("Solo.MOST_IN_ONE.CharacterControl_Count");
        }

        void Restore()
        {
            SetInt(_db, Gems, _savedGems);
            SetInt(_db, "CurrentLevel_Count", _savedLevel);
            Call(_db, "SaveToJson");
            if (_hadCrowd) PlayerPrefs.SetInt(CrowdKey, _savedCrowd); else PlayerPrefs.DeleteKey(CrowdKey);
            Time.timeScale = 1f;
        }

        Transform Holder => Get<Transform>(_runner, "ChildHolder");

        static readonly int[] Levels = { 1, 4, 15, 30 };

        [UnityTest]
        public IEnumerator BigCrowdClimbsStairsAndTriplesGems([ValueSource(nameof(Levels))] int level)
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
                Assert.IsNotNull(GameObject.Find($"Count_Level_{(level == 1 ? 1 : level)}(Clone)") ?? GameObject.Find($"Count_Level_{level}(Clone)"), "Level prefab not instantiated");

                // the bot cannot steer: switch off the lane dead zones (layer 9) a player would dodge
                foreach (var c in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
                    if (c.gameObject.layer == 9 && c.name.StartsWith("DeadZone")) c.enabled = false;
                Assert.AreEqual(120, Get<int>(_runner, "MaxCrowd"), "Crowd cap not applied");
                Call(_runner, "InstantAddChilds", 100);
                Click("StartGame Button");
                Time.timeScale = 3f;
                float nextLog = 0f;
                yield return WaitUntil(() =>
                {
                    if (Time.time >= nextLog)
                    {
                        nextLog = Time.time + 1f;
                        Debug.Log($"[AMO] t={Time.time:0} z={_runner.transform.position.z:0.0} crowd={Holder.childCount} attack={Get<bool>(_runner, "IsAttacking")} end={Get<bool>(_runner, "ReachedEndLine")}");
                    }
                    return Get<bool>(_gm, "IsGameOver");
                }, 120f, "the run to end");
                Debug.Log($"[AMO] crowd level {level}: endline={Get<bool>(_runner, "ReachedEndLine")} won={Get<int>(_gm, "CurrentLevel") == level + 1}");
                Assert.AreEqual(level + 1, Get<int>(_gm, "CurrentLevel"), $"Level {level} not won");

                var end = Find("Solo.MOST_IN_ONE.EndGameUIHandler");
                yield return WaitUntil(() => Get<bool>(end, "CountFinished"), 40f, "gem count-up");
                int earned = Get<int>(end, "LastEarned");
                Debug.Log($"[AMO] crowd level {level} earned {earned} gems");
                Assert.Greater(earned, 0, "Stairs multiplier paid nothing");
                int before = GetInt(_db, Gems);
                Click("Triple Reward Button");
                Assert.AreEqual(before + 2 * earned, GetInt(_db, Gems));
            }
            finally { Restore(); }
        }

        [UnityTest]
        public IEnumerator CrowdWipedOffersOneRevive()
        {
            yield return Setup();
            try
            {
                Click("StartGame Button");
                yield return new WaitForSeconds(0.3f);
                KillCrowd();
                yield return WaitUntil(() => FindObject("Lose UI").activeInHierarchy, 6f, "lose panel");
                Assert.IsTrue(FindObject("Revive Button").activeInHierarchy, "Revive not offered");

                Click("Revive Button");
                yield return null;
                yield return null;
                Assert.IsFalse(Get<bool>(_gm, "IsGameOver"));
                Assert.GreaterOrEqual(Holder.childCount, 15, "Revive did not respawn the crowd");
                Assert.IsTrue(Get<bool>(_runner, "IsActive"));
                Assert.IsFalse(FindObject("Lose UI").activeInHierarchy);

                yield return new WaitForSeconds(0.5f);
                KillCrowd();
                yield return WaitUntil(() => FindObject("Lose UI").activeInHierarchy, 6f, "second lose panel");
                Assert.IsFalse(FindObject("Revive Button").activeInHierarchy, "Revive offered twice");
            }
            finally { Restore(); }
        }

        void KillCrowd()
        {
            var holder = Holder;
            for (int i = holder.childCount - 1; i >= 0; i--)
                Call(holder.GetChild(i).GetComponent(GameType("Solo.MOST_IN_ONE.CharacterControl_Count_Child")), "DestroyChild");
        }

        [UnityTest]
        public IEnumerator CrowdUpgradeAndRewardedBiggerStart()
        {
            yield return Setup(resetMeta: true);
            try
            {
                int start = Holder.childCount;
                SetInt(_db, Gems, 10000);
                Call(Find("AllMightyOne.Runner.RunnerUpgradeShop"), "Refresh");
                Click("Upgrade_Crowd_Button");
                yield return null;
                Assert.AreEqual(10000 - 50, GetInt(_db, Gems), "Crowd upgrade should cost 50");
                Assert.AreEqual(start + 3, Holder.childCount, "Crowd did not grow on the menu");

                SetInt(_db, Gems, 0);
                Click("Upgrade_Crowd_Button"); // free with the placeholder rewarded ad
                yield return null;
                Assert.AreEqual(3, PlayerPrefs.GetInt(CrowdKey));
                Assert.AreEqual(start + 6, Holder.childCount);

                Click("Bigger Start Button");
                yield return null;
                Assert.AreEqual(start + 6 + 25, Holder.childCount, "Bigger start did not add 25");
                Assert.IsFalse(FindObject("Bigger Start Button").activeInHierarchy, "Bigger start should be one-time");

                yield return LoadScene(Scene);
                Bind();
                Assert.AreEqual(start + 6, Holder.childCount, "Upgraded starting crowd not applied on load");
            }
            finally { Restore(); }
        }
    }
}
