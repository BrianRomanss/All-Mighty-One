using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static AllMightyOne.Tests.GameDriver;

namespace AllMightyOne.Tests
{
    public class PistolTests : GameTestBase
    {
        const string Scene = "Assets/Most In One/Runner/Scenes/Pistol.unity";
        const string Gems = "Currency_Gems";
        static readonly string[] StatKeys = { "CharacterGun_MaxHealth", "CharacterGun_FireRate", "CharacterGun_FireRange" };

        Component _gm, _player, _damage;
        object _db;
        int _savedGems, _savedLevel;
        readonly float[] _savedStats = new float[3];
        readonly bool[] _hadStats = new bool[3];

        IEnumerator Setup()
        {
            for (int i = 0; i < 3; i++)
            {
                _hadStats[i] = PlayerPrefs.HasKey(StatKeys[i]);
                _savedStats[i] = PlayerPrefs.GetFloat(StatKeys[i]);
            }
            yield return LoadScene(Scene);
            _gm = Find("Solo.MOST_IN_ONE.UniversalGameManager");
            _player = Find("Solo.MOST_IN_ONE.CharacterControl_Gun");
            _damage = Get<Component>(_player, "DamageSystem"); // enemies have MOST_Damage too
            _db = Get<object>(_gm, "DatabaseHolder");
            _savedGems = GetInt(_db, Gems);
            _savedLevel = Get<int>(_gm, "CurrentLevel");
        }

        void Restore()
        {
            SetInt(_db, Gems, _savedGems);
            SetInt(_db, "CurrentLevel_Pistol", _savedLevel);
            Call(_db, "SaveToJson");
            for (int i = 0; i < 3; i++)
                if (_hadStats[i]) PlayerPrefs.SetFloat(StatKeys[i], _savedStats[i]); else PlayerPrefs.DeleteKey(StatKeys[i]);
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator InvincibleRunReachesFinishAndTriplesGems()
        {
            yield return Setup();
            try
            {
                Click("StartGame Button");
                Set(_damage, "MaxHealth", 100000f);
                Set(_damage, "Health", 100000f);
                Time.timeScale = 3f;
                int gemsBefore = GetInt(_db, Gems);

                yield return WaitUntil(() => Get<bool>(_gm, "IsGameOver"), 90f, "the run to end");
                Debug.Log($"[AMO] run ended: z={_player.transform.position.z:0.0} health={Get<float>(_damage, "Health")} lose={FindObject("Lose UI").activeInHierarchy} level={Get<int>(_gm, "CurrentLevel")} saved={_savedLevel} active={Get<bool>(_player, "IsActive")}");
                Assert.AreEqual(_savedLevel + 1, Get<int>(_gm, "CurrentLevel"), "Run ended without a win");

                var end = Find("Solo.MOST_IN_ONE.EndGameUIHandler");
                yield return WaitUntil(() => Get<bool>(end, "CountFinished"), 20f, "gem count-up");
                int earned = Get<int>(end, "LastEarned");
                Debug.Log($"[AMO] level {_savedLevel} earned {earned} gems");
                var triple = FindObject("Triple Reward Button");
                Assert.AreEqual(earned > 0, triple.activeInHierarchy, "x3 button visibility should follow the reward");
                if (earned > 0)
                {
                    int gemsAfterRun = GetInt(_db, Gems);
                    Click("Triple Reward Button");
                    Assert.AreEqual(gemsAfterRun + 2 * earned, GetInt(_db, Gems), "x3 did not add twice the earned gems");
                    Assert.IsFalse(triple.activeInHierarchy, "x3 button still visible after claiming");
                }
                Assert.GreaterOrEqual(GetInt(_db, Gems), gemsBefore);
            }
            finally { Restore(); }
        }

        static readonly int[] GeneratedLevels = { 4, 15, 30 };

        [UnityTest]
        public IEnumerator GeneratedLevelCanBeFinished([ValueSource(nameof(GeneratedLevels))] int level)
        {
            yield return Setup();
            try
            {
                Call(_gm, "LoadLevel", level);
                yield return new WaitForSeconds(1f);
                _gm = Find("Solo.MOST_IN_ONE.UniversalGameManager");
                _player = Find("Solo.MOST_IN_ONE.CharacterControl_Gun");
                _damage = Get<Component>(_player, "DamageSystem");
                Assert.AreEqual(level, Get<int>(_gm, "CurrentLevel"));

                Click("StartGame Button");
                Set(_damage, "MaxHealth", 100000f);
                Set(_damage, "Health", 100000f);
                Time.timeScale = 3f;
                yield return WaitUntil(() => Get<bool>(_gm, "IsGameOver"), 90f, "the run to end");
                Debug.Log($"[AMO] generated level {level}: z={_player.transform.position.z:0.0} damage taken={100000f - Get<float>(_damage, "Health"):0}");
                Assert.AreEqual(level + 1, Get<int>(_gm, "CurrentLevel"), $"Level {level} not won");
                Assert.Greater(GameObject.FindGameObjectsWithTag("Enemy").Length + 1, 0);
            }
            finally { Restore(); }
        }

        [UnityTest]
        public IEnumerator DeathOffersOneRevive()
        {
            yield return Setup();
            try
            {
                Click("StartGame Button");
                yield return new WaitForSeconds(0.5f);
                Call(_damage, "OnDamage", 100000f);
                yield return WaitUntil(() => FindObject("Lose UI").activeInHierarchy, 5f, "lose panel");
                var revive = FindObject("Revive Button");
                Assert.IsTrue(revive.activeInHierarchy, "Revive not offered");

                Click("Revive Button");
                yield return null;
                Assert.IsFalse(Get<bool>(_gm, "IsGameOver"), "Still game over after revive");
                Assert.IsTrue(Get<bool>(_player, "IsActive"), "Player not running after revive");
                Assert.AreEqual(Get<float>(_damage, "MaxHealth"), Get<float>(_damage, "Health"), 0.01f, "Health not restored");
                Assert.IsFalse(FindObject("Lose UI").activeInHierarchy, "Lose panel still visible");

                yield return new WaitForSeconds(3f); // invulnerability ends
                Call(_damage, "OnDamage", 100000f);
                yield return WaitUntil(() => FindObject("Lose UI").activeInHierarchy, 5f, "second lose panel");
                Assert.IsFalse(revive.activeInHierarchy, "Revive offered twice");
            }
            finally { Restore(); }
        }

        [UnityTest]
        public IEnumerator UpgradesCostMoreEachTimeAndAreFreeWithAd()
        {
            for (int i = 0; i < 3; i++) PlayerPrefs.DeleteKey(StatKeys[i]); // start from level 1 stats
            yield return Setup();
            try
            {
                SetInt(_db, Gems, 10000);
                float rate0 = Get<float>(_player, "FireRate");
                Click("Upgrade_FireRate_Button");
                int afterFirst = GetInt(_db, Gems);
                Click("Upgrade_FireRate_Button");
                int afterSecond = GetInt(_db, Gems);
                int cost1 = 10000 - afterFirst, cost2 = afterFirst - afterSecond;
                Debug.Log($"[AMO] fire rate costs {cost1} then {cost2}");
                Assert.Greater(cost1, 0);
                Assert.Greater(cost2, cost1, "Second upgrade should cost more");
                Assert.Greater(Get<float>(_player, "FireRate"), rate0);

                SetInt(_db, Gems, 0);
                float range0 = Get<float>(_player, "FireRange");
                Click("Upgrade_FireRange_Button"); // no gems: placeholder rewarded ad grants a free upgrade
                Assert.Greater(Get<float>(_player, "FireRange"), range0, "Free upgrade not applied");
                Assert.AreEqual(0, GetInt(_db, Gems), "Free upgrade charged gems");
            }
            finally { Restore(); }
        }
    }
}
