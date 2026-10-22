using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static AllMightyOne.Tests.GameDriver;

namespace AllMightyOne.Tests
{
    public class SplatTests : GameTestBase
    {
        const string Scene = "Assets/Most In One/Grid/Scene/Splat.unity";

        // 1: tutorial; 11, 31, 41: with stop points; 50: last
        static readonly int[] SampleLevels = { 1, 11, 31, 41, 50 };

        [UnityTest]
        public IEnumerator SolverPaintsLevelToWin([ValueSource(nameof(SampleLevels))] int level)
        {
            yield return LoadScene(Scene);
            var gm = Find("Solo.MOST_IN_ONE.UniversalGameManager");
            int saved = Get<int>(gm, "CurrentLevel");
            try
            {
                if (level != saved)
                {
                    Call(gm, "LoadLevel", level);
                    yield return new WaitForSeconds(1f);
                    gm = Find("Solo.MOST_IN_ONE.UniversalGameManager");
                }
                var player = Find("Solo.MOST_IN_ONE.CharacterControl_Cubey");
                Assert.AreEqual(level, Get<int>(gm, "CurrentLevel"));
                Click("Start Game B");
                yield return new WaitForSeconds(0.5f);

                var moves = (Vector2[])Call(player, "SolveFromHere");
                Assert.IsNotNull(moves, $"Level {level} has no solution in-scene");
                int i = 0;
                foreach (var m in moves)
                {
                    // the solver's model must match the physics: every swipe has to land where the model says
                    Call(player, "InputDetection", m);
                    yield return null;
                    yield return WaitUntil(() => !Get<bool>(player, "IsMoving"), 10f, "move to finish");
                    yield return new WaitForSeconds(0.1f);
                    i++;
                    if (Get<bool>(player, "IsGameOver")) break;
                    var rest = (Vector2[])Call(player, "SolveFromHere");
                    Assert.IsNotNull(rest, $"Level {level}: no solution after move {i} at {player.transform.position}");
                }
                Debug.Log($"[AMO] splat level {level}: {i}/{moves.Length} moves, remain={Get<int>(player, "_remainBlocks")}");
                Assert.AreEqual(0, Get<int>(player, "_remainBlocks"), $"Level {level}: tiles left after the solution (stop points not counted?)");
                yield return WaitUntil(() => Get<bool>(gm, "IsGameOver"), 3f, "game over");
                Assert.AreEqual(level + 1, Get<int>(gm, "CurrentLevel"), $"Level {level} not completed");
            }
            finally { Call(gm, "LoadLevel", saved); }
            yield return new WaitForSeconds(0.5f);
        }

        [UnityTest]
        public IEnumerator HintPointsAlongTheSolutionAndSkipCompletes()
        {
            yield return LoadScene(Scene);
            var gm = Find("Solo.MOST_IN_ONE.UniversalGameManager");
            int saved = Get<int>(gm, "CurrentLevel");
            try
            {
                if (saved != 11)
                {
                    Call(gm, "LoadLevel", 11);
                    yield return new WaitForSeconds(1f);
                    gm = Find("Solo.MOST_IN_ONE.UniversalGameManager");
                }
                var player = Find("Solo.MOST_IN_ONE.CharacterControl_Cubey");
                Click("Start Game B");
                yield return new WaitForSeconds(0.5f);

                // follow the hint marker for a few swipes: each must move the player onto the marked cell
                for (int k = 0; k < 4; k++)
                {
                    Click("Hint Button"); // placeholder ads grant the reward immediately
                    yield return null;
                    var marker = FindObject("Cubey Hint");
                    Assert.IsNotNull(marker, "Hint marker not created");
                    Assert.IsTrue(marker.activeInHierarchy, "Hint marker not visible");
                    var target = marker.transform.position;
                    var next = ((Vector2[])Call(player, "SolveFromHere"))[0];
                    Call(player, "InputDetection", next);
                    yield return null;
                    yield return WaitUntil(() => !Get<bool>(player, "IsMoving"), 10f, "hinted move");
                    var p = player.transform.position;
                    Assert.AreEqual(new Vector2(target.x, target.z), new Vector2(p.x, p.z), $"Hint {k} pointed elsewhere");
                    Assert.IsFalse(marker.activeInHierarchy, "Hint marker should hide on the next swipe");
                }

                Click("Skip Button");
                yield return WaitUntil(() => Get<bool>(gm, "IsGameOver"), 3f, "skip");
                Assert.AreEqual(12, Get<int>(gm, "CurrentLevel"), "Skip did not complete the level");
            }
            finally { Call(gm, "LoadLevel", saved); }
            yield return new WaitForSeconds(0.5f);
        }
    }
}
