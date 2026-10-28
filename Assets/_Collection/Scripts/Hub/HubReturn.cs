using Solo.MOST_IN_ONE;
using UnityEngine;
using UnityEngine.UI;

namespace AllMightyOne.Hub
{
    /// <summary>
    /// In every game: the home button and the Android back key return to the hub (through the game manager, so
    /// the scene fades). During a run the home button gives way to a pause button; the back key or leaving the app
    /// pauses too, and the pause panel offers to resume or go home.
    /// </summary>
    public class HubReturn : MonoBehaviour
    {
        [Tooltip("The game's menu panel: the back key only returns to the hub while it is shown.")]
        public GameObject MenuPanel;
        public Button HomeButton;
        [Tooltip("Optional gem pill: shown and hidden together with the home button.")]
        public GameObject GemPill;

        [Header("Pause (optional)")]
        [Tooltip("Shown instead of the home button while a run is played.")]
        public Button PauseButton;
        public GameObject PausePanel;
        public Button ResumeButton, PauseHomeButton;

        UniversalGameManager _gm;

        /// <summary> True while the pause panel holds the game (time scale 0). </summary>
        public bool Paused { get; private set; }

        /// <summary> A run is being played: the menu is closed and the game is not over. </summary>
        public bool InRun => MenuPanel && !MenuPanel.activeInHierarchy && _gm && !_gm.IsGameOver;

        void Start()
        {
            _gm = FindFirstObjectByType<UniversalGameManager>();
            if (HomeButton) HomeButton.onClick.AddListener(GoHome);
            if (PauseButton) PauseButton.onClick.AddListener(Pause);
            if (ResumeButton) ResumeButton.onClick.AddListener(Resume);
            if (PauseHomeButton) PauseHomeButton.onClick.AddListener(GoHome);
            if (PausePanel) PausePanel.SetActive(false);
        }

        void Update()
        {
            // the home button only shows on the game's menu, never during a run
            bool onMenu = MenuPanel && MenuPanel.activeInHierarchy;
            if (MenuPanel && HomeButton && HomeButton.gameObject.activeSelf != onMenu) HomeButton.gameObject.SetActive(onMenu);
            if (MenuPanel && GemPill && GemPill.activeSelf != onMenu) GemPill.SetActive(onMenu);
            bool canPause = PauseButton && InRun && !Paused;
            if (PauseButton && PauseButton.gameObject.activeSelf != canPause) PauseButton.gameObject.SetActive(canPause);

            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (Paused) Resume();
            else if (PausePanel && InRun) Pause();
            else if (!MenuPanel || onMenu) GoHome();
        }

        void OnApplicationPause(bool paused)
        {
            if (paused && PausePanel && InRun) Pause(); // back from another app, the run waits for the player
        }

        void OnDestroy()
        {
            if (Paused) Time.timeScale = 1f;
        }

        /// <summary> Freezes the run and shows the pause panel. Does nothing outside a run. </summary>
        public void Pause()
        {
            if (Paused || !PausePanel || !InRun) return;
            Paused = true;
            Time.timeScale = 0f;
            PausePanel.SetActive(true);
            if (PauseButton) PauseButton.gameObject.SetActive(false);
        }

        /// <summary> Hides the pause panel and lets the run continue. </summary>
        public void Resume()
        {
            if (!Paused) return;
            Paused = false;
            Time.timeScale = 1f;
            if (PausePanel) PausePanel.SetActive(false);
        }

        public void GoHome()
        {
            if (SceneFader.IsLoading) return;
            // from the pause panel the run stays frozen behind the fade (it uses unscaled time); OnDestroy restores time
            if (_gm) _gm.BackToHub();
            else SceneFader.Load(UniversalGameManager.HubSceneName);
        }
    }
}
