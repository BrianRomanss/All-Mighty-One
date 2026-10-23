using System;
using UnityEngine;

namespace AllMightyOne.Hub
{
    /// <summary> Calendar days for the daily features. <see cref="Now"/> can be replaced (tests jump between days). </summary>
    public static class CollectionClock
    {
        public static Func<DateTime> Now = () => DateTime.Now;
        static readonly DateTime Epoch = new(2020, 1, 1);

        /// <summary> Local calendar day as a number: consecutive days differ by one. </summary>
        public static int Today => (int)(Now().Date - Epoch).TotalDays;

        /// <summary> Time left until the next local midnight. </summary>
        public static TimeSpan UntilTomorrow => Now().Date.AddDays(1) - Now();
    }

    /// <summary>
    /// Seven-day login reward. Claiming on consecutive days walks through <see cref="Amounts"/>; missing a day starts
    /// again at day 1, and after day 7 the cycle repeats. Saved in PlayerPrefs.
    /// </summary>
    public static class DailyReward
    {
        public static readonly int[] Amounts = { 20, 30, 40, 50, 60, 80, 150 };

        const string LastKey = "AMO_Daily_Last", StreakKey = "AMO_Daily_Streak";

        static int Last => PlayerPrefs.GetInt(LastKey, int.MinValue);
        static int Streak => PlayerPrefs.GetInt(StreakKey, 0);
        static bool StreakAlive => Last == CollectionClock.Today - 1;

        public static bool Available => Last != CollectionClock.Today;

        /// <summary> Index (0-6) of today's reward: the one to claim, or the one already claimed today. </summary>
        public static int Day
        {
            get
            {
                if (!Available) return (Streak - 1) % Amounts.Length;
                return StreakAlive ? Streak % Amounts.Length : 0;
            }
        }

        public static int TodayAmount => Amounts[Day];

        /// <summary> Records today's claim and returns the gems to add (0 when already claimed today). </summary>
        public static int Claim(int multiplier = 1)
        {
            if (!Available) return 0;
            int amount = TodayAmount * Mathf.Max(1, multiplier);
            PlayerPrefs.SetInt(StreakKey, StreakAlive ? Streak + 1 : 1);
            PlayerPrefs.SetInt(LastKey, CollectionClock.Today);
            PlayerPrefs.Save();
            return amount;
        }
    }
}
