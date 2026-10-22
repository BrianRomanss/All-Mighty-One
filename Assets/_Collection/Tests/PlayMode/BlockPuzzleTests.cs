using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static AllMightyOne.Tests.GameDriver;

namespace AllMightyOne.Tests
{
    public class BlockPuzzleTests : GameTestBase
    {
        const string Scene = "Assets/Most In One/Grid/Scene/BlockBlast Classic.unity";
        const int MaxPlacements = 1500;

        IEnumerator PlayUntilGameOver(Component game, System.Action<int> report)
        {
            int placed = 0;
            while (!Get<bool>(game, "IsGameOver") && placed < MaxPlacements)
            {
                yield return WaitUntil(() => !Get<bool>(game, "IsBusy"), 5f, "piece animation");
                if (Get<bool>(game, "IsGameOver")) break;
                bool ok = (bool)Call(game, "AutoPlaceAny");
                Assert.IsTrue(ok || Get<bool>(game, "IsGameOver"), "No piece could be placed but the game is not over");
                placed++;
                yield return null;
            }
            yield return WaitUntil(() => !Get<bool>(game, "IsBusy"), 5f, "last animation");
            report(placed);
        }

        [UnityTest]
        public IEnumerator PlayToGameOverThenReviveOnce()
        {
            Time.timeScale = 4f;
            yield return LoadScene(Scene);
            var game = Find("Solo.MOST_IN_ONE.BlockBlastGame");
            var gm = Find("Solo.MOST_IN_ONE.UniversalGameManager");

            int placed = 0;
            yield return PlayUntilGameOver(game, n => placed = n);
            Debug.Log($"[AMO] first run: {placed} placements, score {Get<int>(game, "Score")}");
            Assert.IsTrue(Get<bool>(game, "IsGameOver"), $"No game over after {placed} placements");
            Assert.Greater(Get<int>(game, "Score"), 0);

            yield return new WaitForSeconds(2f); // lose panel delay
            Assert.IsTrue(FindObject("Lose UI").activeInHierarchy, "Lose panel not shown");
            var cont = FindObject("Continue Button");
            Assert.IsTrue(cont.activeInHierarchy, "Continue button hidden on first loss");

            int scoreBefore = Get<int>(game, "Score");
            int occupiedBefore = Get<int>(game, "OccupiedCount");
            Click("Continue Button"); // placeholder ad grants immediately
            yield return null;

            Assert.IsFalse(Get<bool>(game, "IsGameOver"), "Still game over after revive");
            Assert.IsFalse(FindObject("Lose UI").activeInHierarchy, "Lose panel still visible after revive");
            Assert.IsTrue(FindObject("InGame UI").activeInHierarchy, "In-game panel not visible after revive");
            Assert.Less(Get<int>(game, "OccupiedCount"), occupiedBefore, "Revive did not clear any blocks");
            Assert.AreEqual(scoreBefore, Get<int>(game, "Score"), "Revive changed the score");
            Assert.IsTrue((bool)Call(game, "AutoPlaceAny"), "No move available right after revive");

            yield return PlayUntilGameOver(game, n => placed = n);
            Debug.Log($"[AMO] after revive: {placed} placements, score {Get<int>(game, "Score")}");
            Assert.IsTrue(Get<bool>(game, "IsGameOver"));
            yield return new WaitForSeconds(2f);
            Assert.IsFalse(cont.activeInHierarchy, "Continue button offered twice in one run");
            Time.timeScale = 1f;
        }
    }
}
