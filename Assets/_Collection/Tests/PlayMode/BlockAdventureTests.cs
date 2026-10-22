using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static AllMightyOne.Tests.GameDriver;

namespace AllMightyOne.Tests
{
    public class BlockAdventureTests : GameTestBase
    {
        const string Scene = "Assets/Most In One/Grid/Scene/BlockBlast Adventure.unity";
        const int MaxPlacements = 400;

        Component _gm, _game;
        int _saved;

        IEnumerator Open(int level)
        {
            Time.timeScale = 4f;
            yield return LoadScene(Scene);
            _gm = Find("Solo.MOST_IN_ONE.UniversalGameManager");
            _saved = Get<int>(_gm, "CurrentLevel");
            if (level != _saved)
            {
                Call(_gm, "LoadLevel", level);
                yield return new WaitForSeconds(1f);
            }
            _gm = Find("Solo.MOST_IN_ONE.UniversalGameManager");
            _game = Find("Solo.MOST_IN_ONE.BlockBlastGame");
            Assert.AreEqual(level, Get<int>(_gm, "CurrentLevel"));
        }

        IEnumerator Restore()
        {
            Time.timeScale = 1f;
            Call(_gm, "LoadLevel", _saved);
            yield return new WaitForSeconds(0.5f);
        }

        // plays with the given hook until the level ends; returns the number of placements through the callback
        IEnumerator Play(string hook, System.Action<int> report)
        {
            int placed = 0;
            while (!Get<bool>(_game, "IsGameOver") && placed < MaxPlacements)
            {
                yield return WaitUntil(() => !Get<bool>(_game, "IsBusy"), 5f, "piece animation");
                if (Get<bool>(_game, "IsGameOver")) break;
                bool ok = (bool)Call(_game, hook);
                Assert.IsTrue(ok || Get<bool>(_game, "IsGameOver"), "No piece could be placed but the level is not over");
                placed++;
                yield return null;
            }
            yield return WaitUntil(() => !Get<bool>(_game, "IsBusy"), 5f, "last animation");
            report(placed);
        }

        // board (# occupied, X blocked) and tray shapes, for diagnosing a loss
        string Board()
        {
            var states = (System.Array)Get<object>(_game, "_states");
            int cols = Get<int>(_game, "_cols");
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < states.Length; i++)
            {
                int v = System.Convert.ToInt32(states.GetValue(i));
                sb.Append(v == 0 ? '.' : v == 1 ? '#' : 'X');
                if (i % cols == cols - 1) sb.AppendLine();
            }
            foreach (var p in (System.Array)Get<object>(_game, "_tray"))
                sb.Append(Get<string>(p, "shapeId")).Append(' ');
            return sb.ToString();
        }

        static readonly int[] SampleLevels = { 1, 6, 12, 18, 24 };

        [UnityTest]
        public IEnumerator GreedyPlayerReachesTheTarget([ValueSource(nameof(SampleLevels))] int level)
        {
            yield return Open(level);
            int placed = 0;
            yield return Play("AutoPlaceBest", n => placed = n);
            int score = Get<int>(_game, "Score"), target = Get<int>(_game, "TargetScore");
            Debug.Log($"[AMO] block adventure level {level}: target={target} score={score} placements={placed} won={Get<int>(_gm, "CurrentLevel") == level + 1}\n{Board()}");
            Assert.IsTrue(Get<bool>(_game, "IsGameOver"), $"Level {level} did not end after {placed} placements");
            Assert.GreaterOrEqual(score, target, $"Level {level} lost at {score}/{target}");
            yield return WaitUntil(() => FindObject("Win UI").activeInHierarchy, 6f, "win panel");
            Assert.AreEqual(level + 1, Get<int>(_gm, "CurrentLevel"), "Win did not advance the level");
            yield return Restore();
        }

        [UnityTest]
        public IEnumerator NextLevelLoadsTheFollowingLevel()
        {
            yield return Open(1);
            Assert.AreEqual(2000, Get<int>(_game, "TargetScore"), "Level 1 should have the easier target");
            yield return Play("AutoPlaceBest", _ => { });
            yield return WaitUntil(() => FindObject("Win UI").activeInHierarchy, 6f, "win panel");
            Click("Next Level");
            yield return new WaitForSeconds(1f);
            _gm = Find("Solo.MOST_IN_ONE.UniversalGameManager");
            _game = Find("Solo.MOST_IN_ONE.BlockBlastGame");
            Assert.AreEqual(2, Get<int>(_gm, "CurrentLevel"), "Next Level skipped or repeated a level");
            Assert.AreEqual(2500, Get<int>(_game, "TargetScore"), "Level 2 not loaded");
            Assert.IsFalse(Get<bool>(_game, "IsGameOver"));
            yield return Restore();
        }

        [UnityTest]
        public IEnumerator GameOverOffersOneContinue()
        {
            yield return Open(24);
            // an out-of-reach target: the run can only end with a loss
            Set(Get<object>(_game, "levelMeta"), "CustomValue", 1e7f);
            yield return Play("AutoPlaceAny", _ => { });
            yield return WaitUntil(() => FindObject("Lose UI").activeInHierarchy, 6f, "lose panel");
            Assert.IsTrue(FindObject("Continue Button").activeInHierarchy, "Continue not offered");
            int score = Get<int>(_game, "Score");

            Click("Continue Button");
            yield return null;
            Assert.IsFalse(Get<bool>(_game, "IsGameOver"));
            Assert.IsFalse(Get<bool>(_gm, "IsGameOver"));
            Assert.IsFalse(FindObject("Lose UI").activeInHierarchy, "Lose panel still shown after continue");
            Assert.AreEqual(score, Get<int>(_game, "Score"), "Continue changed the score");

            yield return Play("AutoPlaceAny", _ => { });
            yield return WaitUntil(() => FindObject("Lose UI").activeInHierarchy, 6f, "second lose panel");
            Assert.IsFalse(FindObject("Continue Button").activeInHierarchy, "Continue offered twice");
            Assert.AreEqual(24, Get<int>(_gm, "CurrentLevel"), "A loss must not advance the level");
            yield return Restore();
        }
    }
}
