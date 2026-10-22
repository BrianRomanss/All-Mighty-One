using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.TestTools;
using static AllMightyOne.Tests.GameDriver;

namespace AllMightyOne.Tests
{
    public class NinjaTests : GameTestBase
    {
        const string Scene = "Assets/Most In One/Controls/Scenes/Ninja.unity";
        const string LevelKey = "AMO_Ninja_Level";
        const string Gems = "Currency_Gems";

        Component _arena;
        object _db;
        int _savedGems, _savedLevel;
        bool _hadLevel;

        IEnumerator Setup(int level)
        {
            _hadLevel = PlayerPrefs.HasKey(LevelKey);
            _savedLevel = PlayerPrefs.GetInt(LevelKey, 1);
            PlayerPrefs.SetInt(LevelKey, level);
            yield return LoadScene(Scene);
            _arena = Find("AllMightyOne.Ninja.NinjaArena");
            _db = Get<object>(_arena, "Database");
            _savedGems = GetInt(_db, Gems);
        }

        void Restore()
        {
            SetInt(_db, Gems, _savedGems);
            Call(_db, "SaveToJson");
            if (_hadLevel) PlayerPrefs.SetInt(LevelKey, _savedLevel); else PlayerPrefs.DeleteKey(LevelKey);
            Time.timeScale = 1f;
        }

        bool Playing => Get<bool>(_arena, "IsPlaying");
        float Health => Get<float>(_arena, "Health");
        int Alive => Get<int>(_arena, "AliveZombies");
        // labels share names across panels: read them through the arena's references
        string Text(string field) => Get<UnityEngine.UI.Text>(_arena, field).text;

        void KillAll()
        {
            foreach (var d in Object.FindObjectsByType(GameType("Solo.MOST_IN_ONE.MOST_Damage"), FindObjectsSortMode.None))
                if (!(bool)Call(d, "IsDefeated")) Call(d, "OnDamage", 1e6f);
        }

        [UnityTest]
        public IEnumerator ZombiesChaseHitAndClearingWinsWithGems()
        {
            yield return Setup(1);
            try
            {
                Assert.IsTrue(FindObject("Menu UI").activeInHierarchy, "Menu not shown on load");
                Assert.IsFalse(FindObject("Play Controls").activeInHierarchy, "Joysticks shown on the menu");
                SetInt(_db, Gems, 1000);
                Click("StartGame Button");
                yield return null;
                Assert.IsTrue(Playing);
                Assert.IsTrue(FindObject("InGame UI").activeInHierarchy);
                Assert.IsTrue(FindObject("Play Controls").activeInHierarchy);
                Assert.AreEqual(1, Get<int>(_arena, "WaveCount"));
                Assert.AreEqual(4, Get<int>(_arena, "ZombiesPerWave"));

                yield return WaitUntil(() => Alive == 4, 5f, "the wave to spawn");
                // the kit's zombie AI chases the ninja; contact hits cost health
                yield return WaitUntil(() => Health < 100f, 15f, "a zombie to reach the ninja");
                Debug.Log($"[AMO] ninja hit, health {Health}");
                Assert.IsTrue(Playing, "One hit should not end the level");

                KillAll();
                Assert.AreEqual(1004, GetInt(_db, Gems), "1 gem per kill");
                yield return WaitUntil(() => FindObject("Win UI").activeInHierarchy, 6f, "win panel");
                Assert.AreEqual(1014, GetInt(_db, Gems), "Level bonus 10");
                Assert.AreEqual("+14 GEMS", Text("WinGemsText"));
                Assert.AreEqual(2, PlayerPrefs.GetInt(LevelKey), "Level not advanced");
                Assert.IsFalse(FindObject("Play Controls").activeInHierarchy);

                Click("Triple Reward Button");
                Assert.AreEqual(1042, GetInt(_db, Gems));
                Assert.AreEqual("+42 GEMS", Text("WinGemsText"));

                Click("Next Level Button");
                yield return new WaitForSeconds(1f);
                _arena = Find("AllMightyOne.Ninja.NinjaArena");
                Assert.AreEqual(2, Get<int>(_arena, "Level"));
                Assert.AreEqual("LEVEL 2", Text("MenuLevelText"));
            }
            finally { Restore(); }
        }

        [UnityTest]
        public IEnumerator KatanaSlashKillsAZombie()
        {
            yield return Setup(1);
            try
            {
                Click("StartGame Button");
                yield return WaitUntil(() => Alive >= 1, 5f, "a zombie");
                var ninja = FindObject("Character_Ninja").transform;
                var damage = (Component)Get<IList>(_arena, "_zombies")[0]; // the template is also named "Zombie"
                var zombie = damage.gameObject;
                Set(_arena, "ZombieDamage", 0f);
                Set(zombie.GetComponent(GameType("Solo.MOST_IN_ONE.WalkEnemyManager")), "StartMove", false);
                var model = FindObject("Ninja KT").transform;
                zombie.transform.position = ninja.position + model.forward * 2f;
                yield return new WaitForFixedUpdate();

                var aim = FindObject("MainAttack").GetComponent(GameType("Solo.MOST_IN_ONE.MOST_Aim"));
                var dir = zombie.transform.position - ninja.position;
                Get<UnityEvent<Vector2>>(aim, "OnRelease").Invoke(new Vector2(dir.x, dir.z).normalized);
                yield return WaitUntil(() => (bool)Call(damage, "IsDefeated"), 3f, "the slash to kill the zombie");
            }
            finally { Restore(); }
        }

        [UnityTest]
        public IEnumerator DefeatOffersOneRevive()
        {
            yield return Setup(3);
            try
            {
                Click("StartGame Button");
                yield return WaitUntil(() => Alive >= 2, 5f, "zombies");
                Call(_arena, "TakeDamage", 1000f);
                Assert.IsFalse(Playing);
                yield return WaitUntil(() => FindObject("Lose UI").activeInHierarchy, 6f, "lose panel");
                Assert.IsTrue(FindObject("Revive Button").activeInHierarchy, "Revive not offered");
                Assert.IsFalse(FindObject("Play Controls").activeInHierarchy);

                Click("Revive Button");
                yield return null;
                Assert.IsTrue(Playing);
                Assert.AreEqual(100f, Health);
                Assert.IsFalse(FindObject("Lose UI").activeInHierarchy);
                Assert.IsTrue(FindObject("InGame UI").activeInHierarchy);
                Assert.IsTrue(FindObject("Play Controls").activeInHierarchy);
                Call(_arena, "TakeDamage", 1000f);
                Assert.IsTrue(Playing, "Hits right after the revive should be ignored");

                yield return new WaitForSeconds(2.1f);
                Call(_arena, "TakeDamage", 1000f);
                yield return WaitUntil(() => FindObject("Lose UI").activeInHierarchy, 6f, "second lose panel");
                Assert.IsFalse(FindObject("Revive Button").activeInHierarchy, "Revive offered twice");
                Assert.AreEqual(3, PlayerPrefs.GetInt(LevelKey), "A loss must not change the level");

                Click("Retry Button");
                yield return new WaitForSeconds(1f);
                Assert.IsTrue(FindObject("Menu UI").activeInHierarchy, "Retry should go back to the menu");
            }
            finally { Restore(); }
        }

        [UnityTest]
        public IEnumerator DifficultyGrowsWithLevels()
        {
            yield return Setup(1);
            try
            {
                int lastTotal = 0;
                float lastSpeed = 0;
                var report = new System.Text.StringBuilder();
                for (int level = 1; level <= 40; level++)
                {
                    PlayerPrefs.SetInt(LevelKey, level);
                    int waves = Get<int>(_arena, "WaveCount"), per = Get<int>(_arena, "ZombiesPerWave");
                    float speed = Get<float>(_arena, "CurrentZombieSpeed");
                    if (level % 5 == 1) report.Append($"L{level}: {waves}x{per} @{speed:0.0}  ");
                    Assert.GreaterOrEqual(waves * per, lastTotal, $"Level {level} has fewer zombies");
                    Assert.GreaterOrEqual(speed, lastSpeed);
                    Assert.Less(speed, 6f, "Zombies must stay slower than the ninja (6)");
                    lastTotal = waves * per;
                    lastSpeed = speed;
                }
                Debug.Log($"[AMO] ninja difficulty {report}");
            }
            finally { Restore(); }
        }
    }
}
