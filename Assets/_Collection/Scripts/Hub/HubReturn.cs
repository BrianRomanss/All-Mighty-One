using Solo.MOST_IN_ONE;
using UnityEngine;
using UnityEngine.UI;

namespace AllMightyOne.Hub
{
    /// <summary>
    /// In every game: the home button and the Android back key return to the hub (through the game manager, so
    /// the scene fades). The back key is ignored while a run is being played.
    /// </summary>
    public class HubReturn : MonoBehaviour
    {
        [Tooltip("The game's menu panel: the back key only returns to the hub while it is shown.")]
        public GameObject MenuPanel;
        public Button HomeButton;
        [Tooltip("Optional gem pill: shown and hidden together with the home button.")]
        public GameObject GemPill;

        UniversalGameManager _gm;

        void Start()
        {
            _gm = FindFirstObjectByType<UniversalGameManager>();
            if (HomeButton) HomeButton.onClick.AddListener(GoHome);
        }

        void Update()
        {
            // the home button only shows on the game's menu, never during a run
            if (MenuPanel && HomeButton && HomeButton.gameObject.activeSelf != MenuPanel.activeInHierarchy)
                HomeButton.gameObject.SetActive(MenuPanel.activeInHierarchy);
            if (MenuPanel && GemPill && GemPill.activeSelf != MenuPanel.activeInHierarchy)
                GemPill.SetActive(MenuPanel.activeInHierarchy);
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (MenuPanel && !MenuPanel.activeInHierarchy) return;
            GoHome();
        }

        public void GoHome()
        {
            if (SceneFader.IsLoading) return;
            if (_gm) _gm.BackToHub();
            else SceneFader.Load(UniversalGameManager.HubSceneName);
        }
    }
}
