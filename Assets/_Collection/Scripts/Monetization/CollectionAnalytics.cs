using System;
using System.Collections.Generic;
using Unity.Services.Analytics;
using Unity.Services.Core;
using UnityEngine;

namespace AllMightyOne
{
    /// <summary>
    /// Gameplay analytics for the whole collection, sent to Unity Analytics (Unity Gaming Services).
    /// Every call is safe to make at any time: events raised before the service is ready are queued, and nothing here
    /// can throw into gameplay. Each event must exist in the dashboard's Event Manager with the same parameters.
    /// </summary>
    public static class CollectionAnalytics
    {
        public static bool DebugLog = true;
        /// <summary> Editor play sessions (and tests) only log; set true to check events in the dashboard from the editor. </summary>
        public static bool SendFromEditor = false;

        const string ConsentKey = "AMO_AnalyticsConsent";
        const int MaxQueued = 100;

        static bool s_ready;
        static readonly Queue<CustomEvent> s_queue = new();
        static string s_game; // catalog id of the game started from the hub

        // ---------------------------------------------------------------- events

        public static void GameStart(string gameId)
        {
            s_game = gameId;
            Log("game_start", ("game_id", gameId));
        }

        public static void LevelStart(string gameId, int level) => Log("level_start", ("game_id", Game(gameId)), ("level", level));
        public static void LevelComplete(string gameId, int level) => Log("level_complete", ("game_id", Game(gameId)), ("level", level));
        public static void LevelFail(string gameId, int level) => Log("level_fail", ("game_id", Game(gameId)), ("level", level));
        public static void RunEnd(string gameId, int score) => Log("run_end", ("game_id", Game(gameId)), ("score", score));
        public static void GameUnlock(string gameId, string method) => Log("game_unlock", ("game_id", gameId), ("method", method));

        public static void AdInterstitial(string placement) => Log("ad_interstitial", ("placement", placement));
        public static void AdRewarded(string placement, bool rewarded) => Log("ad_rewarded", ("placement", placement), ("rewarded", rewarded));
        public static void Purchase(string productId) => Log("purchase", ("product_id", productId));
        public static void DailyReward(int day, bool doubled) => Log("daily_reward", ("day", day), ("doubled", doubled));
        public static void MissionClaim(string mission) => Log("mission_claim", ("mission", mission));

        // the game manager reports its save key (CurrentLevel_Runner...); the hub's id (squad...) reads better in the dashboard
        static string Game(string gameId) => string.IsNullOrEmpty(s_game) ? gameId : s_game;

        public static void Log(string eventName, params (string key, object value)[] parameters)
        {
            if (DebugLog && Debug.isDebugBuild)
            {
                var dict = new Dictionary<string, object>();
                foreach (var (key, value) in parameters) dict[key] = value;
                Debug.Log($"[Analytics] {eventName} {string.Join(", ", dict)}");
            }
            if (!Consent) return;
            try
            {
                var e = new CustomEvent(eventName);
                foreach (var (key, value) in parameters)
                    e.Add(key, value is int or long or float or double or bool or string ? value : value?.ToString() ?? "");
                if (s_ready) AnalyticsService.Instance.RecordEvent(e);
                else if (s_queue.Count < MaxQueued) s_queue.Enqueue(e);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Analytics] {eventName} not recorded: {ex.Message}");
            }
        }

        // ---------------------------------------------------------------- consent and start-up

        /// <summary>
        /// Whether the player allows analytics. On by default until the consent prompt sets it; where consent is
        /// required (EEA, UK), the prompt must call SetConsent before any data is sent.
        /// </summary>
        public static bool Consent => PlayerPrefs.GetInt(ConsentKey, 1) == 1;

        public static void SetConsent(bool allowed)
        {
            PlayerPrefs.SetInt(ConsentKey, allowed ? 1 : 0);
            PlayerPrefs.Save();
            if (!allowed) s_queue.Clear();
            if (UnityServices.State != ServicesInitializationState.Initialized) return;
            try
            {
                if (allowed) StartCollection();
                else
                {
                    s_ready = false;
                    AnalyticsService.Instance.StopDataCollection();
                }
            }
            catch (Exception ex) { Debug.LogWarning($"[Analytics] Consent change failed: {ex.Message}"); }
        }

        /// <summary> Asks Unity to delete everything collected from this player (privacy request), then stops collecting. </summary>
        public static void RequestDataDeletion()
        {
            SetConsent(false);
            try { if (UnityServices.State == ServicesInitializationState.Initialized) AnalyticsService.Instance.RequestDataDeletion(); }
            catch (Exception ex) { Debug.LogWarning($"[Analytics] Data deletion failed: {ex.Message}"); }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static async void Init()
        {
            s_ready = false;
            s_game = null;
            s_queue.Clear();
            if (Application.isEditor && !SendFromEditor) return;
            try
            {
                await UnityServices.InitializeAsync();
                if (Consent) StartCollection();
            }
            catch (Exception ex)
            {
                // no Unity Cloud project linked, or offline at launch: the game plays on without analytics
                Debug.LogWarning($"[Analytics] Unity Services unavailable: {ex.Message}");
            }
        }

        static void StartCollection()
        {
            AnalyticsService.Instance.StartDataCollection();
            s_ready = true;
            while (s_queue.Count > 0) AnalyticsService.Instance.RecordEvent(s_queue.Dequeue());
        }
    }
}
