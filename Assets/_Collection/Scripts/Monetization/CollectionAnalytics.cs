using System.Collections.Generic;
using UnityEngine;

namespace AllMightyOne
{
    /// <summary>
    /// Placeholder analytics. Route Log() to the GBP analytics module (Firebase) once it is installed.
    /// </summary>
    public static class CollectionAnalytics
    {
        public static bool DebugLog = true;

        public static void GameStart(string gameId) => Log("game_start", ("game_id", gameId));
        public static void LevelStart(string gameId, int level) => Log("level_start", ("game_id", gameId), ("level", level));
        public static void LevelComplete(string gameId, int level) => Log("level_complete", ("game_id", gameId), ("level", level));
        public static void LevelFail(string gameId, int level) => Log("level_fail", ("game_id", gameId), ("level", level));
        public static void RunEnd(string gameId, int score) => Log("run_end", ("game_id", gameId), ("score", score));
        public static void GameUnlock(string gameId, string method) => Log("game_unlock", ("game_id", gameId), ("method", method));

        public static void Log(string eventName, params (string key, object value)[] parameters)
        {
            var dict = new Dictionary<string, object>();
            foreach (var (key, value) in parameters) dict[key] = value;
            // TODO(GBP analytics): forward eventName + dict to the analytics SDK here.
            if (DebugLog && Debug.isDebugBuild)
                Debug.Log($"[Analytics] {eventName} {string.Join(", ", dict)}");
        }
    }
}
