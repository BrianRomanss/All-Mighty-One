using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static AllMightyOne.Tests.GameDriver;

namespace AllMightyOne.Tests
{
    public class CubeyTests : GameTestBase
    {
        const string Scene = "Assets/Most In One/Grid/Scene/Cubey.unity";

        [UnityTest]
        public IEnumerator SolverPlaysCurrentLevelToWin()
        {
            yield return LoadScene(Scene);
            var gm = Find("Solo.MOST_IN_ONE.UniversalGameManager");
            var player = Find("Solo.MOST_IN_ONE.CharacterControl_Cubey");
            int levelBefore = Get<int>(gm, "CurrentLevel");

            Click("Start Game B");
            yield return new WaitForSeconds(0.5f);

            var moves = (Vector2[])Call(player, "SolveFromHere");
            Assert.IsNotNull(moves, $"Level {levelBefore} has no solution from the start");

            Debug.Log($"[AMO] start pos={player.transform.position} remain={Get<int>(player, "_remainBlocks")} moves={string.Join(" ", moves)}");
            foreach (var m in moves)
            {
                Call(player, "InputDetection", m);
                yield return null;
                yield return WaitUntil(() => !Get<bool>(player, "IsMoving"), 10f, "move to finish");
                yield return new WaitForSeconds(0.4f); // visited tiles rise
                Debug.Log($"[AMO] move {m} -> pos={player.transform.position} remain={Get<int>(player, "_remainBlocks")} over={Get<bool>(player, "IsGameOver")}");
                if (Get<bool>(player, "IsGameOver")) break;
            }

            yield return WaitUntil(() => Get<bool>(gm, "IsGameOver"), 3f, "game over");
            Assert.AreEqual(levelBefore + 1, Get<int>(gm, "CurrentLevel"), "Level was not completed (lost instead?)");

            // restore the saved level so the test does not change progress
            Call(gm, "LoadLevel", levelBefore);
            yield return new WaitForSeconds(0.5f);
        }

        static readonly int[] SampleLevels = { 39, 46, 51, 75, 100 };

        [UnityTest]
        public IEnumerator GeneratedLevelPlaysToWin([ValueSource(nameof(SampleLevels))] int level)
        {
            yield return LoadScene(Scene);
            var gm = Find("Solo.MOST_IN_ONE.UniversalGameManager");
            int saved = Get<int>(gm, "CurrentLevel");
            Call(gm, "LoadLevel", level); // reloads the scene on that level
            yield return new WaitForSeconds(1f);

            gm = Find("Solo.MOST_IN_ONE.UniversalGameManager");
            var player = Find("Solo.MOST_IN_ONE.CharacterControl_Cubey");
            Assert.AreEqual(level, Get<int>(gm, "CurrentLevel"));
            Click("Start Game B");
            yield return new WaitForSeconds(0.5f);

            var moves = (Vector2[])Call(player, "SolveFromHere");
            Assert.IsNotNull(moves, $"Level {level} has no solution in-scene");
            foreach (var m in moves)
            {
                Call(player, "InputDetection", m);
                yield return null;
                yield return WaitUntil(() => !Get<bool>(player, "IsMoving"), 10f, "move to finish");
                yield return new WaitForSeconds(0.4f);
                if (Get<bool>(player, "IsGameOver")) break;
            }
            yield return WaitUntil(() => Get<bool>(gm, "IsGameOver"), 3f, "game over");
            Assert.AreEqual(level + 1, Get<int>(gm, "CurrentLevel"), $"Level {level} was lost");

            Call(gm, "LoadLevel", saved);
            yield return new WaitForSeconds(0.5f);
        }

        [UnityTest]
        public IEnumerator HintShowsMarker()
        {
            yield return LoadScene(Scene);
            var player = Find("Solo.MOST_IN_ONE.CharacterControl_Cubey");
            Click("Start Game B");
            yield return new WaitForSeconds(0.5f);

            Click("Hint Button"); // placeholder ads grant the reward immediately
            yield return null;
            var marker = FindObject("Cubey Hint");
            Assert.IsNotNull(marker, "Hint marker not created");
            Assert.IsTrue(marker.activeInHierarchy, "Hint marker not visible");
            Assert.IsFalse(Get<bool>(player, "IsGameOver"));
        }
    }
}
