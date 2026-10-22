using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static AllMightyOne.Tests.GameDriver;

namespace AllMightyOne.Tests
{
    public class BullDodgeTests : GameTestBase
    {
        const string Scene = "Assets/Most In One/Dodge/Scenes/Dodge.unity";
        const string Gems = "Currency_Gems";
        const string ScoreName = "Dodge Template Score";

        Component _gm, _character;
        object _db;
        int _savedGems, _savedBest;

        IEnumerator Setup()
        {
            yield return LoadScene(Scene);
            Bind();
            _db = Get<object>(_character, "ScoreHolder");
            _savedGems = GetInt(_db, Gems);
            _savedBest = Get<int>(GetIntData(_db, ScoreName), "HighestValue");
        }

        void Bind()
        {
            _gm = Find("Solo.MOST_IN_ONE.UniversalGameManager");
            _character = Find("Solo.MOST_IN_ONE.CharacterControl_Dodge");
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

        int Score => GetInt(_db, ScoreName);
        bool Defeated => Get<bool>(_character, "Defeat");
        // TMP lives in Assembly-CSharp (the kit's copy), so its text is read through reflection
        static string Text(string name) => Get<string>(FindObject(name).GetComponent(GameType("TMPro.TMP_Text")), "text");

        // a fake bull: its close-dodge rings (tag Score Object, layers 7/8/9) are children of the bull, like the prefab
        static Collider[] FakeBull()
        {
            var bull = new GameObject("Fake Bull");
            bull.transform.position = new Vector3(0, 0, 30);
            var rings = new Collider[3];
            for (int i = 0; i < 3; i++)
            {
                var ring = new GameObject($"level{i + 1}", typeof(BoxCollider));
                ring.transform.SetParent(bull.transform, false);
                ring.tag = "Score Object";
                ring.layer = 7 + i;
                rings[i] = ring.GetComponent<Collider>();
            }
            return rings;
        }

        static Collider FakeHorns()
        {
            var horns = new GameObject("Fake Horns", typeof(BoxCollider));
            horns.transform.position = new Vector3(0, 0, 1);
            horns.tag = "Enemy";
            return horns.GetComponent<Collider>();
        }

        // enters the rings down to `closest` (1..3), then leaves the outer ring: the dodge is scored
        void Dodge(int closest)
        {
            var rings = FakeBull();
            for (int i = 0; i < closest; i++) Call(_character, "OnTriggerEnter", rings[i]);
            Call(_character, "OnTriggerExit", rings[0]);
        }

        [UnityTest]
        public IEnumerator CloseDodgesScoreTiersPayGemsAndReviveOnce()
        {
            yield return Setup();
            try
            {
                Set(GetIntData(_db, ScoreName), "HighestValue", 777);
                yield return LoadScene(Scene);
                Bind();
                yield return null;
                Assert.AreEqual("Best: 777", Text("Best Score"), "Menu should show the best score");

                SetInt(_db, Gems, 1000);
                Click("StartGameButton");
                yield return null;
                // scripted bulls only: live bulls would add dodges (or hits) of their own
                Call(Find("Solo.MOST_IN_ONE.MOST_Spawn"), "EnableState", false);
                foreach (var bull in Object.FindObjectsByType(GameType("Solo.MOST_IN_ONE.ForwardMovement"), FindObjectsSortMode.None))
                    Object.Destroy(((Component)bull).gameObject);
                Assert.AreEqual(0, Score);
                Dodge(3);
                Assert.AreEqual(20, Score, "A closest dodge pays 20");
                Dodge(1);
                Assert.AreEqual(25, Score, "A wide dodge pays 5");

                Call(_character, "OnTriggerEnter", FakeHorns());
                Assert.IsTrue(Defeated);
                yield return WaitUntil(() => FindObject("End Game UI").activeInHierarchy, 6f, "end screen");
                Assert.AreEqual("SCORE 25", Text("Run Score Text"));
                Assert.AreEqual("BEST 777", Text("Run Best Text"));
                Assert.AreEqual("+2 GEMS", Text("Run Gems Text"));
                Assert.AreEqual(1002, GetInt(_db, Gems), "25 points pay 2 gems");
                Assert.IsTrue(FindObject("Revive Button").activeInHierarchy, "Revive not offered");

                Click("Triple Reward Button");
                Assert.AreEqual(1006, GetInt(_db, Gems), "x3 should add 4 more gems");
                Assert.AreEqual("+6 GEMS", Text("Run Gems Text"));
                Assert.IsFalse(FindObject("Triple Reward Button").activeInHierarchy);

                Click("Revive Button");
                yield return null;
                yield return null;
                Assert.IsFalse(Defeated);
                Assert.IsFalse(Get<bool>(_gm, "IsGameOver"));
                Assert.IsFalse(FindObject("End Game UI").activeInHierarchy);
                Assert.IsTrue(FindObject("InGame UI").activeInHierarchy);
                Assert.IsTrue(FindObject("Appearance_Effects").activeInHierarchy, "Effects not restored");
                Assert.IsTrue(Get<bool>(Find("Solo.MOST_IN_ONE.MOST_FreeMovement"), "Enable"), "Movement not restored");
                Assert.IsTrue(Get<bool>(Find("Solo.MOST_IN_ONE.MOST_Controller"), "Enable"), "Joystick not restored");

                Call(_character, "OnTriggerEnter", FakeHorns());
                Assert.IsFalse(Defeated, "A bull hit right after the revive should be ignored");

                Dodge(2);
                Assert.AreEqual(35, Score, "Score continues after the revive");
                yield return new WaitForSeconds(2.2f);
                Call(_character, "OnTriggerEnter", FakeHorns());
                Assert.IsTrue(Defeated);
                yield return WaitUntil(() => FindObject("End Game UI").activeInHierarchy, 6f, "second end screen");
                Assert.AreEqual("+1 GEMS", Text("Run Gems Text"), "Only the points after the revive are paid");
                Assert.AreEqual(1007, GetInt(_db, Gems));
                Assert.IsFalse(FindObject("Revive Button").activeInHierarchy, "Revive offered twice");
            }
            finally { Restore(); }
        }

        [UnityTest]
        public IEnumerator BullsRampUpAndAreCleanedUp()
        {
            yield return Setup();
            try
            {
                // keep the character out of the bulls' way: no colliders, no gravity
                foreach (var c in _character.GetComponents<Collider>()) c.enabled = false;
                _character.GetComponent<Rigidbody>().isKinematic = true;

                var spawner = Find("Solo.MOST_IN_ONE.MOST_Spawn");
                int startRate = Get<int>(spawner, "SpawnRatePerCycle");
                Click("StartGameButton");
                Time.timeScale = 4f;
                int maxActive = 0;
                float until = Time.time + 40f;
                yield return WaitUntil(() =>
                {
                    maxActive = Mathf.Max(maxActive, Get<int>(spawner, "CurrentActiveObjects"));
                    return Time.time >= until;
                }, 30f, "40 s of play");
                int total = Get<int>(spawner, "TotalSpawnedObjects");
                int active = Object.FindObjectsByType(GameType("Solo.MOST_IN_ONE.ForwardMovement"), FindObjectsSortMode.None).Length;
                int rate = Get<int>(spawner, "SpawnRatePerCycle");
                Debug.Log($"[AMO] dodge: rate {startRate}->{rate} spawned={total} alive={active} maxActive={maxActive}");
                Assert.Greater(rate, startRate, "Spawn rate did not ramp up");
                Assert.Greater(total, 60, "Too few bulls spawned");
                Assert.Less(active, total / 2, "Bulls that left the arena are not removed");
                Assert.IsFalse(Defeated);
            }
            finally { Restore(); }
        }
    }
}
