using System;
using Solo.MOST_IN_ONE;
using UnityEngine;

namespace AllMightyOne.Hub
{
    public enum GameCategory { Runner, Arcade, Puzzle }

    /// <summary> Where a game keeps the number shown on its hub card. </summary>
    public enum ProgressKind { DatabaseLevel, DatabaseBest, PrefsLevel, PrefsBestSeconds }

    [Serializable]
    public class GameEntry
    {
        public string Id;
        public string DisplayName;
        public string Tagline;
        [Tooltip("Scene name as listed in the build settings.")]
        public string SceneName;
        public GameCategory Category;
        public Color Theme = Color.white;
        [Tooltip("Card art. Empty: the card shows the game's initials.")]
        public Sprite Icon;
        [Tooltip("Gems to unlock. 0 = unlocked from the start.")]
        [Min(0)] public int UnlockCost;
        public ProgressKind Progress;
        [Tooltip("Database entry or PlayerPrefs key read for the card's progress line.")]
        public string ProgressKey;
    }

    /// <summary> The collection's games as shown in the hub, with progress, unlock and play-history state. </summary>
    [CreateAssetMenu(menuName = "All Mighty One/Game Catalog")]
    public class GameCatalog : ScriptableObject
    {
        public MOST_Database Database;
        public string CurrencyName = "Currency_Gems";
        public GameEntry[] Games = new GameEntry[0];

        const string UnlockedPrefix = "AMO_Unlocked_", PlayedPrefix = "AMO_Played_", LastPlayedKey = "AMO_LastPlayed";

        public GameEntry Find(string id) => Array.Find(Games, g => g.Id == id);

        public int Gems => Database ? Database.Get<IntData>(CurrencyName)?.Value ?? 0 : 0;

        public bool IsUnlocked(GameEntry g) => g.UnlockCost <= 0 || PlayerPrefs.GetInt(UnlockedPrefix + g.Id, 0) == 1;

        /// <summary> Spends the unlock price; false when the player cannot afford it. </summary>
        public bool TryUnlock(GameEntry g)
        {
            if (IsUnlocked(g)) return true;
            var gems = Database.Get<IntData>(CurrencyName);
            if (gems == null || !gems.TrySubtract(g.UnlockCost)) return false;
            Database.SaveToJson();
            PlayerPrefs.SetInt(UnlockedPrefix + g.Id, 1);
            PlayerPrefs.Save();
            return true;
        }

        public static bool WasPlayed(GameEntry g) => PlayerPrefs.GetInt(PlayedPrefix + g.Id, 0) == 1;

        public static void MarkPlayed(GameEntry g)
        {
            PlayerPrefs.SetInt(PlayedPrefix + g.Id, 1);
            PlayerPrefs.SetString(LastPlayedKey, g.Id);
            PlayerPrefs.Save();
        }

        public GameEntry LastPlayed => Find(PlayerPrefs.GetString(LastPlayedKey, ""));

        /// <summary> The card's progress line: "LEVEL 7", "BEST 420", "BEST 35s", or "NEW" before the first play. </summary>
        public string ProgressText(GameEntry g)
        {
            switch (g.Progress)
            {
                case ProgressKind.DatabaseLevel:
                    {
                        var d = Database ? Database.Get<IntData>(g.ProgressKey) : null;
                        return $"LEVEL {Mathf.Max(1, d?.Value ?? 1)}";
                    }
                case ProgressKind.DatabaseBest:
                    {
                        var d = Database ? Database.Get<IntData>(g.ProgressKey) : null;
                        int best = d?.HighestValue ?? 0;
                        return best > 0 ? $"BEST {best}" : "NEW";
                    }
                case ProgressKind.PrefsLevel:
                    return $"LEVEL {Mathf.Max(1, PlayerPrefs.GetInt(g.ProgressKey, 1))}";
                default:
                    {
                        int best = PlayerPrefs.GetInt(g.ProgressKey, 0);
                        return best > 0 ? $"BEST {best}s" : "NEW";
                    }
            }
        }

        public static string CategoryName(GameCategory c) => c.ToString().ToUpperInvariant();
    }
}
