using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AllMightyOne.Hub
{
    public enum MissionKind { PlayDifferentGames, PlayGame, FinishRuns, WinLevels, EarnGems }

    public class Mission
    {
        public int Slot;
        public MissionKind Kind;
        public int Target;
        public int Reward;
        public string GameId, GameName;
        public int Progress;
        public bool Claimed;

        public bool Done => Progress >= Target;

        public string Text => Kind switch
        {
            MissionKind.PlayDifferentGames => $"Play {Target} different games",
            MissionKind.PlayGame => $"Play {GameName}",
            MissionKind.FinishRuns => $"Finish {Target} runs in any game",
            MissionKind.WinLevels => $"Win {Target} levels",
            _ => $"Earn {Target} gems in games",
        };
    }

    /// <summary>
    /// Three missions a day, the same for the whole day, new ones at midnight. The games report what happens (a game
    /// started from the hub, a run won or lost, gems brought back to the hub) into day counters; each mission reads
    /// the counter of its kind. Saved in PlayerPrefs.
    /// </summary>
    public static class DailyMissions
    {
        public const int Count = 3;

        const string DayKey = "AMO_Mis_Day", GameKey = "AMO_Mis_Game", PlayedKey = "AMO_Mis_Played",
            RunsKey = "AMO_Mis_Runs", WinsKey = "AMO_Mis_Wins", GemsKey = "AMO_Mis_Gems", ClaimedPrefix = "AMO_Mis_Claimed_";

        // ---------------------------------------------------------------- events from the games and the hub

        public static void GameStarted(string gameId)
        {
            Roll();
            var played = Played;
            if (played.Contains(gameId)) return;
            played.Add(gameId);
            PlayerPrefs.SetString(PlayedKey, string.Join(",", played));
            PlayerPrefs.Save();
        }

        /// <summary> Called by UniversalGameManager when a run is won or lost. </summary>
        public static void RunEnded(bool won)
        {
            Roll();
            Add(RunsKey, 1);
            if (won) Add(WinsKey, 1);
        }

        public static void GemsEarned(int gems)
        {
            if (gems <= 0) return;
            Roll();
            Add(GemsKey, gems);
        }

        // ---------------------------------------------------------------- today's missions

        /// <summary> Today's missions with their progress. The catalog chooses the game for "Play X". </summary>
        public static Mission[] Today(GameCatalog catalog)
        {
            Roll();
            // every draw happens up front in a fixed order: the same day always gives the same missions
            var rng = new System.Random(CollectionClock.Today * 7919);
            bool playGame = rng.Next(2) == 0, finishRuns = rng.Next(2) == 0;
            int gameRoll = rng.Next(1 << 20);
            var missions = new Mission[Count];

            // 1: try games (a different game each day, preferring ones not played lately), 2: play runs, 3: earn gems
            var gameId = PlayerPrefs.GetString(GameKey, "");
            var game = catalog ? catalog.Find(gameId) : null;
            if (game == null && catalog)
            {
                var unlocked = catalog.Games.Where(catalog.IsUnlocked).ToList();
                var last = catalog.LastPlayed;
                var pool = unlocked.Where(g => g != last).ToList();
                if (pool.Count == 0) pool = unlocked;
                if (pool.Count > 0)
                {
                    game = pool[gameRoll % pool.Count];
                    PlayerPrefs.SetString(GameKey, game.Id);
                    PlayerPrefs.Save();
                }
            }
            missions[0] = game != null && playGame
                ? new Mission { Kind = MissionKind.PlayGame, Target = 1, Reward = 25, GameId = game.Id, GameName = game.DisplayName.ToUpperInvariant() }
                : new Mission { Kind = MissionKind.PlayDifferentGames, Target = 3, Reward = 30 };
            missions[1] = finishRuns
                ? new Mission { Kind = MissionKind.FinishRuns, Target = 5, Reward = 25 }
                : new Mission { Kind = MissionKind.WinLevels, Target = 3, Reward = 30 };
            missions[2] = new Mission { Kind = MissionKind.EarnGems, Target = 60, Reward = 40 };

            var played = Played;
            for (int i = 0; i < Count; i++)
            {
                var m = missions[i];
                m.Slot = i;
                m.Progress = m.Kind switch
                {
                    MissionKind.PlayDifferentGames => played.Count,
                    MissionKind.PlayGame => played.Contains(m.GameId) ? 1 : 0,
                    MissionKind.FinishRuns => PlayerPrefs.GetInt(RunsKey, 0),
                    MissionKind.WinLevels => PlayerPrefs.GetInt(WinsKey, 0),
                    _ => PlayerPrefs.GetInt(GemsKey, 0),
                };
                m.Progress = Mathf.Min(m.Progress, m.Target);
                m.Claimed = PlayerPrefs.GetInt(ClaimedPrefix + i, 0) == 1;
            }
            return missions;
        }

        /// <summary> Marks a finished mission as claimed and returns its reward (0 if not finished or already claimed). </summary>
        public static int Claim(Mission m)
        {
            if (m == null || !m.Done || m.Claimed) return 0;
            m.Claimed = true;
            PlayerPrefs.SetInt(ClaimedPrefix + m.Slot, 1);
            PlayerPrefs.Save();
            return m.Reward;
        }

        /// <summary> Missions finished but not claimed yet (the red dot on the hub button). </summary>
        public static int Claimable(GameCatalog catalog) => Today(catalog).Count(m => m.Done && !m.Claimed);

        // ---------------------------------------------------------------- storage

        static HashSet<string> Played =>
            new(PlayerPrefs.GetString(PlayedKey, "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));

        static void Add(string key, int n)
        {
            PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key, 0) + n);
            PlayerPrefs.Save();
        }

        // a new day wipes yesterday's counters, claims and chosen game
        static void Roll()
        {
            int today = CollectionClock.Today;
            if (PlayerPrefs.GetInt(DayKey, int.MinValue) == today) return;
            PlayerPrefs.SetInt(DayKey, today);
            foreach (var k in new[] { GameKey, PlayedKey, RunsKey, WinsKey, GemsKey }) PlayerPrefs.DeleteKey(k);
            for (int i = 0; i < Count; i++) PlayerPrefs.DeleteKey(ClaimedPrefix + i);
            PlayerPrefs.Save();
        }
    }
}
