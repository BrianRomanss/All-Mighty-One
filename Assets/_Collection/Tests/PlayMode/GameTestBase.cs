using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AllMightyOne.Tests
{
    /// <summary>
    /// Fails a test when a win / lose screen appeared without AdsBridge being told about the level end (no interstitial).
    /// Fails a test on any logged error/exception, except the TMP importer popup that headless editors open at startup
    /// (TMP resources are present; the popup is an editor batch-mode timing artifact, not a game error).
    /// </summary>
    public abstract class GameTestBase
    {
        static readonly string[] IgnoredFragments =
        {
            "TextMesh Pro Essential Resources are missing",
            "Cannot import package in play mode",
        };

        readonly List<string> _errors = new();
        int _levelEndsAtStart;
        bool _endScreenSeen;
        bool _ownScene;

        [SetUp]
        public void CaptureErrors()
        {
            _errors.Clear();
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += OnLog;
            _levelEndsAtStart = LevelEndCount();
            _endScreenSeen = false;
            _ownScene = false;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update += CloseTmpImporterPopup;
            UnityEditor.EditorApplication.update += WatchEndScreens;
#endif
        }

        static int LevelEndCount() =>
            (int)GameDriver.GameType("AllMightyOne.AdsBridge").GetProperty("LevelEndCount").GetValue(null);

        // only after this test loaded its own scene: the previous test's scene (and its end screen) is still up during SetUp
        void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode) => _ownScene = true;

        void WatchEndScreens()
        {
            if (_ownScene && !_endScreenSeen && Application.isPlaying && (GameObject.Find("Lose UI") || GameObject.Find("Win UI")))
                _endScreenSeen = true;
        }

#if UNITY_EDITOR
        // TMP opens its importer window ~1s after an early settings check during play-mode domain reloads.
        // Close it before it draws (its first draw tries to import a package, which fails in play mode).
        static void CloseTmpImporterPopup()
        {
            foreach (var w in Resources.FindObjectsOfTypeAll<UnityEditor.EditorWindow>())
                if (w && w.GetType().Name == "TMP_PackageResourceImporterWindow") w.Close();
        }
#endif

        [TearDown]
        public void AssertNoErrors()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= CloseTmpImporterPopup;
            UnityEditor.EditorApplication.update -= WatchEndScreens;
#endif
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            Application.logMessageReceived -= OnLog;
            LogAssert.ignoreFailingMessages = false;
            Time.timeScale = 1f;
            Assert.IsEmpty(_errors, "Errors were logged during the test:\n" + string.Join("\n", _errors));
            if (_endScreenSeen)
                Assert.Greater(LevelEndCount(), _levelEndsAtStart, "A win/lose screen was shown but AdsBridge.OnLevelEnd was never called");
        }

        void OnLog(string message, string stack, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            foreach (var f in IgnoredFragments) if (message.Contains(f)) return;
            _errors.Add($"{type}: {message}\n{stack}");
        }
    }
}
