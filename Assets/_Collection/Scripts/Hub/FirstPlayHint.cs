using Solo.MOST_IN_ONE;
using TMPro;
using UnityEngine;

namespace AllMightyOne.Hub
{
    /// <summary>
    /// The first time a game is played: a cartoon hand shows the gesture over the play area with one line of text.
    /// It appears when the run starts (or straight away for games without a start screen), goes away once the player
    /// touches the screen or after a few seconds, and never shows again for that game.
    /// </summary>
    public class FirstPlayHint : MonoBehaviour
    {
        public enum Gesture { Tap, SwipeSides, SwipeAround, DragUp, SwipeTurns }

        public string GameId;
        public Gesture Motion;
        public RectTransform Hand, Ripple;
        public TMP_Text Label;
        public CanvasGroup Group;
        [Tooltip("Show as soon as the scene opens (games that start without a 'tap to start' menu).")]
        public bool ShowOnStart;
        [Tooltip("Distance the hand travels, in canvas units.")]
        public float Reach = 260f;
        public float MinSeconds = 1f, MaxSeconds = 5f;

        public const string SeenPrefix = "AMO_Tut_";
        public bool IsShowing => _showing;

        UniversalGameManager _gm;
        Vector2 _home;
        float _t;
        bool _showing;

        public static bool Seen(string id) => PlayerPrefs.GetInt(SeenPrefix + id, 0) == 1;

        void Start()
        {
            Group.alpha = 0f;
            Group.blocksRaycasts = Group.interactable = false;
            _home = Hand.anchoredPosition;
            if (Seen(GameId)) { gameObject.SetActive(false); return; }
            if (ShowOnStart) Show();
            else
            {
                _gm = FindFirstObjectByType<UniversalGameManager>();
                if (_gm) _gm.OnStartPlay.AddListener(Show);
                else Show();
            }
        }

        void OnDestroy()
        {
            if (_gm) _gm.OnStartPlay.RemoveListener(Show);
        }

        public void Show()
        {
            if (_showing || Seen(GameId)) return;
            _showing = true;
            _t = 0f;
        }

        void Update()
        {
            if (!_showing) return;
            _t += Time.unscaledDeltaTime;
            Group.alpha = Mathf.MoveTowards(Group.alpha, 1f, Time.unscaledDeltaTime * 4f);
            Animate(_t);
            bool touched = _t > MinSeconds && Input.GetMouseButtonDown(0);
            if (touched || _t > MaxSeconds) Done();
        }

        void Done()
        {
            _showing = false;
            PlayerPrefs.SetInt(SeenPrefix + GameId, 1);
            PlayerPrefs.Save();
            gameObject.SetActive(false);
        }

        void Animate(float t)
        {
            Vector2 offset = Vector2.zero;
            float press = 0f; // 0..1: the finger presses down (hand shrinks a little, ripple fires)
            switch (Motion)
            {
                case Gesture.Tap:
                    press = Mathf.Clamp01(1f - Mathf.Abs((t % 1f) - 0.3f) * 5f);
                    break;
                case Gesture.SwipeSides:
                    offset.x = Mathf.Sin(t * 2.4f) * Reach;
                    press = 1f;
                    break;
                case Gesture.SwipeAround:
                    offset = new Vector2(Mathf.Cos(t * 2.2f), Mathf.Sin(t * 2.2f) * 0.8f) * Reach * 0.7f;
                    press = 1f;
                    break;
                case Gesture.DragUp:
                {
                    float k = (t % 1.6f) / 1.6f; // pick up at the tray, carry it onto the board, lift off
                    offset.y = Mathf.SmoothStep(0f, Reach, Mathf.Clamp01(k / 0.75f));
                    press = k < 0.8f ? 1f : 0f;
                    break;
                }
                case Gesture.SwipeTurns:
                {
                    // right, down, left, up: one quick swipe each with a pause, like steering a sliding cube
                    Vector2[] dirs = { Vector2.right, Vector2.down, Vector2.left, Vector2.up };
                    int i = (int)(t / 0.9f) % 4;
                    float k = (t % 0.9f) / 0.9f;
                    offset = dirs[i] * Reach * 0.6f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(k / 0.45f));
                    press = k < 0.5f ? 1f : 0f;
                    break;
                }
            }
            Hand.anchoredPosition = _home + offset;
            Hand.localScale = Vector3.one * (1f - 0.12f * press);
            if (Ripple)
            {
                float r = Motion == Gesture.Tap ? (t % 1f) : 0f;
                Ripple.gameObject.SetActive(Motion == Gesture.Tap && r > 0.3f);
                Ripple.anchoredPosition = Hand.anchoredPosition;
                Ripple.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.4f, (r - 0.3f) / 0.7f);
                if (Ripple.TryGetComponent(out UnityEngine.UI.Image img))
                    img.color = new Color(1f, 1f, 1f, Mathf.Lerp(0.8f, 0f, (r - 0.3f) / 0.7f));
            }
        }
    }
}
