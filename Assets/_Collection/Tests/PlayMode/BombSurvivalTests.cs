using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static AllMightyOne.Tests.GameDriver;

namespace AllMightyOne.Tests
{
    public class BombSurvivalTests : GameTestBase
    {
        const string Scene = "Assets/Most In One/Spawn/Scenes/Explosion.unity";
        const string BestKey = "AMO_Bomb_Best";
        const string Gems = "Currency_Gems";

        Component _game, _player;
        object _db;
        int _savedGems, _savedBest;
        bool _hadBest;

        IEnumerator Setup()
        {
            _hadBest = PlayerPrefs.HasKey(BestKey);
            _savedBest = PlayerPrefs.GetInt(BestKey, 0);
            PlayerPrefs.SetInt(BestKey, 5);
            yield return LoadScene(Scene);
            _game = Find("AllMightyOne.Bomb.BombSurvival");
            _player = Get<Component>(_game, "Player");
            _db = Get<object>(_game, "Database");
            _savedGems = GetInt(_db, Gems);
        }

        void Restore()
        {
            SetInt(_db, Gems, _savedGems);
            Call(_db, "SaveToJson");
            if (_hadBest) PlayerPrefs.SetInt(BestKey, _savedBest); else PlayerPrefs.DeleteKey(BestKey);
            Time.timeScale = 1f;
        }

        bool Playing => Get<bool>(_game, "IsPlaying");
        string Text(string field) => Get<UnityEngine.UI.Text>(_game, field).text;
        static int Spawned(string spawner) => Get<int>(FindObject(spawner).GetComponent(GameType("Solo.MOST_IN_ONE.MOST_Spawn")), "TotalSpawnedObjects");
        void Shield(bool on) { Set(_player, "UseOnTrigger", !on); Set(_player, "UseOnCollision", !on); }

        [UnityTest]
        public IEnumerator HazardsRunWithTheGameAndABlastKills()
        {
            yield return Setup();
            try
            {
                Assert.IsTrue(FindObject("Menu UI").activeInHierarchy, "Menu not shown on load");
                Assert.AreEqual("BEST 5s", Text("MenuBestText"));
                Assert.IsFalse(FindObject("JoyStick").activeInHierarchy, "Joystick shown on the menu");
                yield return new WaitForSeconds(2f);
                Assert.AreEqual(0, Spawned("SpawnSystem_Bombs"), "Bombs fall while on the menu");

                SetInt(_db, Gems, 1000);
                Click("StartGame Button");
                yield return null;
                Assert.IsTrue(Playing);
                Assert.IsTrue(FindObject("InGame UI").activeInHierarchy);
                Assert.IsTrue(FindObject("JoyStick").activeInHierarchy);

                // all three hazards fire (the cannon and the line blasts were wired to renamed classes in the kit)
                Shield(true);
                Time.timeScale = 3f;
                var lines = Find("Solo.MOST_IN_ONE.ExplosionLine");
                // lines: first cycle starts at 4 s, spawns at its end (cycle 12 s), then flashes 5 s before the blast
                yield return WaitUntil(() => Get<float>(_game, "Survived") >= 24f, 15f, "24 s of play");
                int cannonShots = Object.FindObjectsByType(GameType("Solo.MOST_IN_ONE.ExplosionBomb"), FindObjectsSortMode.None)
                    .Count(b => ((Component)b).name.StartsWith("Bomb_Projectile"));
                Debug.Log($"[AMO] bomb hazards at 24 s: bombs={Spawned("SpawnSystem_Bombs")} cannon cycles={Spawned("SpawnSystem_Cannon")} cannon bombs alive={cannonShots} lines={Spawned("SpawnSystem_Lines")} lineList={Get<IList>(lines, "_lineList").Count}");
                Assert.Greater(Spawned("SpawnSystem_Bombs"), 0, "No bombs");
                Assert.Greater(Spawned("SpawnSystem_Cannon") + cannonShots, 0, "Cannon never fired");
                Assert.Greater(Get<IList>(lines, "_lineList").Count, 0, "Line blasts never activated");
                yield return WaitUntil(() => Object.FindObjectsByType<Collider>(FindObjectsSortMode.None)
                    .Any(c => c.enabled && c.CompareTag("Damage") && c.name.StartsWith("SquareExplosion")), 15f, "a blast");
                Debug.Log($"[AMO] bomb hazards: bombs={Spawned("SpawnSystem_Bombs")} cannon={Spawned("SpawnSystem_Cannon")} lines={Spawned("SpawnSystem_Lines")} t={Get<float>(_game, "Survived"):0}");
                Assert.IsTrue(Playing, "The shielded player should still be alive");
                Time.timeScale = 1f;

                // a real bomb next to the player: its blast kills
                Shield(false);
                var bomb = Object.FindObjectsByType(GameType("Solo.MOST_IN_ONE.ExplosionBomb"), FindObjectsSortMode.None)
                    .Cast<Component>().First(b => b.name.StartsWith("Bomb_Spawn") && !Get<bool>(b, "_exploded"));
                bomb.transform.position = _player.transform.position + Vector3.right;
                Call(bomb, "ExplodeNow");
                yield return WaitUntil(() => !Playing, 5f, "the blast to hit the player");
                yield return WaitUntil(() => !_player.gameObject.activeSelf, 2f, "the defeated player to be removed"); // a frame after OnDefeat
                yield return WaitUntil(() => FindObject("Lose UI").activeInHierarchy, 5f, "game over panel");

                float survived = Get<float>(_game, "Survived");
                int gems = Mathf.FloorToInt(survived / 5f);
                Assert.AreEqual($"{Mathf.FloorToInt(survived)}s", Text("RunTimeText"));
                Assert.AreEqual("NEW BEST!", Text("RunBestText"));
                Assert.AreEqual(Mathf.FloorToInt(survived), PlayerPrefs.GetInt(BestKey));
                Assert.AreEqual($"+{gems} GEMS", Text("RunGemsText"));
                Assert.AreEqual(1000 + gems, GetInt(_db, Gems), "1 gem per 5 s survived");
                Assert.IsFalse(FindObject("JoyStick").activeInHierarchy);
                Assert.IsFalse(Get<bool>(FindObject("SpawnSystem_Bombs").GetComponent(GameType("Solo.MOST_IN_ONE.MOST_Spawn")), "Enable"), "Hazards still running after game over");

                Click("Triple Reward Button");
                Assert.AreEqual(1000 + 3 * gems, GetInt(_db, Gems));
                Assert.AreEqual($"+{3 * gems} GEMS", Text("RunGemsText"));
            }
            finally { Restore(); }
        }

        [UnityTest]
        public IEnumerator ReviveOnceAndPayOnlyNewTime()
        {
            yield return Setup();
            try
            {
                SetInt(_db, Gems, 1000);
                Click("StartGame Button");
                Shield(true);
                Time.timeScale = 4f;
                yield return WaitUntil(() => Get<float>(_game, "Survived") >= 12f, 10f, "12 s of play");
                Time.timeScale = 1f;
                Shield(false);
                Call(_player, "OnDamage", 1000f);
                yield return WaitUntil(() => FindObject("Lose UI").activeInHierarchy, 5f, "game over panel");
                int first = GetInt(_db, Gems) - 1000;
                Assert.GreaterOrEqual(first, 2);
                Assert.IsTrue(FindObject("Revive Button").activeInHierarchy, "Revive not offered");

                Click("Revive Button");
                yield return null;
                Assert.IsTrue(Playing);
                Assert.IsTrue(_player.gameObject.activeSelf, "Player not back");
                Assert.AreEqual(100f, Get<float>(_player, "Health"));
                Assert.IsFalse(FindObject("Lose UI").activeInHierarchy);
                Assert.IsTrue(FindObject("InGame UI").activeInHierarchy);
                Assert.IsTrue(FindObject("JoyStick").activeInHierarchy);
                Assert.IsFalse(Get<bool>(_player, "UseOnTrigger"), "No protection right after the revive");

                Shield(true);
                Time.timeScale = 4f;
                float t0 = Get<float>(_game, "Survived");
                yield return WaitUntil(() => Get<float>(_game, "Survived") >= t0 + 6f, 10f, "6 more seconds");
                Time.timeScale = 1f;
                Shield(false);
                Call(_player, "OnDamage", 1000f);
                yield return WaitUntil(() => FindObject("Lose UI").activeInHierarchy, 5f, "second game over");
                int total = Mathf.FloorToInt(Get<float>(_game, "Survived") / 5f);
                Assert.AreEqual(1000 + total, GetInt(_db, Gems), "The revived run must only pay the new time");
                Assert.IsFalse(FindObject("Revive Button").activeInHierarchy, "Revive offered twice");

                Click("Retry Button");
                yield return new WaitForSeconds(1f);
                Assert.IsTrue(FindObject("Menu UI").activeInHierarchy, "Retry should go back to the menu");
            }
            finally { Restore(); }
        }
    }
}
