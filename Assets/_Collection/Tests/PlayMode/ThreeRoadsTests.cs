using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static AllMightyOne.Tests.GameDriver;

namespace AllMightyOne.Tests
{
    public class ThreeRoadsTests : GameTestBase
    {
        const string Scene = "Assets/Most In One/ThreeRoads/Scene/3Road.unity";
        const string Gems = "Currency_Gems";
        const string ScoreName = "3Road Score";

        Component _gm, _roads;
        object _db;
        int _savedGems, _savedBest;

        IEnumerator Setup()
        {
            yield return LoadScene(Scene);
            _gm = Find("Solo.MOST_IN_ONE.UniversalGameManager");
            _roads = Find("Solo.MOST_IN_ONE.ThreeRoadsController");
            _db = Get<object>(_roads, "DatabaseHolder");
            _savedGems = GetInt(_db, Gems);
            _savedBest = Get<int>(GetIntData(_db, ScoreName), "HighestValue");
        }

        void Restore()
        {
            SetInt(_db, Gems, _savedGems);
            var score = GetIntData(_db, ScoreName);
            Set(score, "Value", 0);
            Set(score, "HighestValue", _savedBest);
            Call(_db, "SaveToJson");
            Time.timeScale = 1f;
        }

        bool Playing => Get<bool>(_roads, "isPlaying");
        int Runners => Get<int>(_roads, "ActiveRunners");
        float Z => Get<float>(_roads, "CurrentZ");
        List<Component> Characters => Get<IList>(_roads, "Characters").Cast<Component>().Where(c => c).ToList();
        static string Text(string name) => Get<string>(FindObject(name).GetComponent(GameType("TMPro.TMP_Text")), "text");

        // trigger stand-ins, far below the track so no runner touches them physically
        static Collider Fake(string tag, Vector3 pos)
        {
            var go = new GameObject($"Fake {tag}", typeof(BoxCollider));
            go.transform.position = pos;
            go.tag = tag;
            return go.GetComponent<Collider>();
        }

        // the pieces after the first ones depend on this seed
        void Seed(int seed) => Set(_roads, "_rng", new System.Random(seed));

        static void Hit(Component runner, string tag) => Call(runner, "OnTriggerEnter", Fake(tag, new Vector3(0, -50, 0)));

        [UnityTest]
        public IEnumerator GatesAddRunnersGemsPayAndReviveOnce()
        {
            yield return Setup();
            try
            {
                SetInt(_db, Gems, 1000);
                // the runners stay at the start: obstacles never reach them, every hit below is scripted
                Set(_roads, "SpeedGainPerMeter", 0f);
                Set(_roads, "ForwardSpeed", 0f);
                Click("StartGame Button");
                yield return null;
                Assert.IsTrue(Playing);
                Assert.AreEqual(1, Runners);

                Hit(Characters[0], "Gate");
                Assert.AreEqual(2, Runners, "A gate should add a runner");
                Assert.AreEqual(new[] { true, true, false }, Get<bool[]>(_roads, "ActiveLanes"));
                var added = Characters.First(c => Get<int>(c, "LaneIndex") == 0);
                Hit(added, "Enemy");
                Assert.AreEqual(2, Runners, "A runner added by a gate ignores obstacles for a moment");

                // a real gem of the track, moved onto the mid runner
                var mid = Characters.First(c => Get<int>(c, "LaneIndex") == 1);
                yield return new WaitForSeconds(0.8f);
                var gem = GameObject.FindGameObjectsWithTag("Currency").First(g => g.transform.parent && g.transform.parent.name.StartsWith("Sub_"));
                gem.transform.position = mid.transform.position;
                yield return WaitUntil(() => GetInt(_db, Gems) == 1001, 3f, "the gem pick-up");

                Hit(added, "Enemy");
                Assert.AreEqual(1, Runners);
                Assert.IsTrue(Playing, "The run goes on while a runner is left");
                Hit(mid, "Enemy");
                Assert.IsFalse(Playing);
                yield return WaitUntil(() => FindObject("Lose UI").activeInHierarchy, 6f, "lose panel");
                Assert.AreEqual("+1 GEMS", Text("Run Gems Text"));
                Assert.IsTrue(FindObject("Revive Button").activeInHierarchy, "Revive not offered");

                Click("Triple Reward Button");
                Assert.AreEqual(1003, GetInt(_db, Gems), "x3 should add 2 more gems");
                Assert.AreEqual("+3 GEMS", Text("Run Gems Text"));
                Assert.IsFalse(FindObject("Triple Reward Button").activeInHierarchy);

                var obstacleAhead = Fake("Enemy", new Vector3(10, 0, Z + 5));
                Click("Revive Button");
                yield return null;
                Assert.IsTrue(Playing);
                Assert.AreEqual(new[] { false, true, false }, Get<bool[]>(_roads, "ActiveLanes"), "Revive should bring back the mid runner");
                Assert.IsFalse(Get<bool>(_gm, "IsGameOver"));
                Assert.IsFalse(FindObject("Lose UI").activeInHierarchy);
                Assert.IsTrue(FindObject("InGame UI").activeInHierarchy);
                Assert.IsFalse(obstacleAhead.gameObject.activeSelf, "Obstacles right ahead should be cleared on revive");
                var revived = Characters.Single();
                Hit(revived, "Enemy");
                Assert.AreEqual(1, Runners, "A hit right after the revive should be ignored");

                yield return new WaitForSeconds(2.1f);
                Hit(revived, "Enemy");
                Assert.IsFalse(Playing);
                yield return WaitUntil(() => FindObject("Lose UI").activeInHierarchy, 6f, "second lose panel");
                Assert.AreEqual("+0 GEMS", Text("Run Gems Text"), "The x3 bonus must not be paid again");
                Assert.IsFalse(FindObject("Triple Reward Button").activeInHierarchy);
                Assert.IsFalse(FindObject("Revive Button").activeInHierarchy, "Revive offered twice");
            }
            finally { Restore(); }
        }

        [UnityTest]
        public IEnumerator SpeedRampsAndPiecesFollowDifficulty()
        {
            yield return Setup();
            try
            {
                float startSpeed = Get<float>(_roads, "ForwardSpeed");
                Seed(11);
                Click("StartGame Button");
                Time.timeScale = 4f;
                var seen = new Dictionary<int, (string name, float z)>();
                int maxLive = 0;
                yield return WaitUntil(() =>
                {
                    foreach (var c in Characters) Set(c, "InvulnerableUntil", float.MaxValue);
                    var pieces = SceneManager.GetActiveScene().GetRootGameObjects().Where(g => g.name.StartsWith("Sub_")).ToList();
                    maxLive = Mathf.Max(maxLive, pieces.Count);
                    foreach (var p in pieces) seen[p.GetInstanceID()] = (p.name, p.transform.position.z);
                    return Z >= 1300f || !Playing;
                }, 120f, "1300 m of track");

                float speed = Get<float>(_roads, "ForwardSpeed");
                int score = Get<int>(_roads, "Score");
                string Kinds(string prefix) => string.Join(",", seen.Values.Where(v => v.name.StartsWith(prefix)).Select(v => v.z.ToString("0")));
                Debug.Log($"[AMO] 3 roads: speed {startSpeed}->{speed:0.0} score={score} runners={Runners} pieces={seen.Count} maxLive={maxLive}\n" +
                          $"easy: {Kinds("Sub_Gen_Easy")}\nmedium: {Kinds("Sub_Gen_Medium")}\nhard: {Kinds("Sub_Gen_Hard")}\nkit: {Kinds("Sub_1")},{Kinds("Sub_2")},{Kinds("Sub_3")}");
                Assert.IsTrue(Playing);
                Assert.AreEqual(startSpeed + 1300 * 0.004f, speed, 0.3f, "Speed does not ramp up with distance");
                Assert.Greater(score, 0);
                Assert.LessOrEqual(maxLive, 8, "Pieces behind the runners are not removed");
                foreach (var (name, z) in seen.Values)
                {
                    if (name.StartsWith("Sub_Gen_Medium")) Assert.GreaterOrEqual(z, 360f, $"{name} too early");
                    if (name.StartsWith("Sub_Gen_Hard")) Assert.GreaterOrEqual(z, 900f, $"{name} too early");
                    if (name.StartsWith("Sub_Gen_Easy")) Assert.LessOrEqual(z, 960f, $"{name} too late");
                    if (name.StartsWith("Sub_1") || name.StartsWith("Sub_2") || name.StartsWith("Sub_3")) Assert.GreaterOrEqual(z, 120f, $"{name} too early");
                }
                Assert.IsTrue(seen.Values.Any(v => v.name.StartsWith("Sub_Gen_Hard")), "No hard piece after 900 m");
            }
            finally { Restore(); }
        }

        // a simple player: each runner switches rail when its rail is blocked just ahead and the other one is clear
        [UnityTest]
        public IEnumerator BotDodgesThroughTheTrack()
        {
            yield return Setup();
            try
            {
                var lanes = Get<IList>(_roads, "Lanes");
                Seed(7);
                Click("StartGame Button");
                Time.timeScale = 3f;
                float start = Time.time;
                yield return WaitUntil(() =>
                {
                    float speed = Get<float>(_roads, "ForwardSpeed");
                    float ahead = speed * 0.3f + 1.2f;
                    var enemies = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None).Where(c => c.enabled && c.CompareTag("Enemy")).ToList();
                    foreach (var ch in Characters)
                    {
                        float cx = Get<float>(lanes[Get<int>(ch, "LaneIndex")], "CenterX");
                        bool right = Get<bool>(ch, "OnRightSide");
                        float z = ch.transform.position.z;
                        bool Blocked(float x) => enemies.Any(e =>
                        {
                            var b = e.bounds;
                            return b.max.y > 0.02f && Mathf.Abs(b.center.x - x) < b.extents.x + 0.25f && b.max.z > z - 0.3f && b.min.z < z + ahead;
                        });
                        float here = cx + (right ? 0.5f : -0.5f), other = cx + (right ? -0.5f : 0.5f);
                        if (Blocked(here) && !Blocked(other)) Call(ch, "ToggleSide");
                    }
                    return !Playing || Z >= 1200f;
                }, 120f, "1200 m of bot play");
                Debug.Log($"[AMO] 3 roads bot: alive={Playing} z={Z:0} t={Time.time - start:0} score={Get<int>(_roads, "Score")} runners={Runners}");
                Assert.IsTrue(Playing, $"The bot was caught at z={Z:0}");
            }
            finally { Restore(); }
        }
    }
}
