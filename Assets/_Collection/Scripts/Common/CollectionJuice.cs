using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AllMightyOne
{
    /// <summary>
    /// Screen-space juice shared by every game: confetti when a win screen shows, a soft red edge flash when a run is
    /// lost, and end screens popping their panels in one after another. Draws on its own overlay canvas above every
    /// scene's UI and never takes touches. Lives next to CollectionFeedback (which adds it), so it exists once.
    /// Reduced motion (CollectionFeedback.ReducedMotion) turns off the confetti and the pops and halves the flash.
    /// </summary>
    public class CollectionJuice : MonoBehaviour
    {
        [Header("Confetti")]
        public int ConfettiCount = 40;
        public float ConfettiLife = 2.6f;
        public float Gravity = 1.6f;          // screen heights per second squared
        public Color[] ConfettiColors =
        {
            new(1f, 0.32f, 0.42f), new(1f, 0.8f, 0.2f), new(0.3f, 0.85f, 1f),
            new(0.55f, 1f, 0.45f), new(0.8f, 0.45f, 1f), Color.white,
        };

        [Header("Lose flash")]
        public Color FlashColor = new(1f, 0.12f, 0.12f, 1f);
        [Range(0f, 1f)] public float FlashAlpha = 0.55f;
        public float FlashTime = 0.55f;

        [Header("Menu pop")]
        public float PopTime = 0.38f;
        public float PopStagger = 0.06f;
        public float PopFromScale = 0.6f;
        public int MaxPopTargets = 10;

        struct Piece
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Velocity;   // screen heights per second
            public float Spin, Age, Flutter, Width;
        }

        static CollectionJuice _instance;
        RectTransform _root;
        Image _flash;
        Piece[] _pieces;
        int _active;
        float _flashT = -1f, _flashPeak;
        readonly Dictionary<Transform, Vector3> _popBase = new();

        /// <summary> Confetti pieces still in the air (tests read it). </summary>
        public int ActiveConfetti => _active;

        /// <summary> Current alpha of the lose flash (tests read it). </summary>
        public float FlashLevel => _flash ? _flash.color.a : 0f;

        void Awake()
        {
            _instance = this;
            var canvasGo = new GameObject("Juice Overlay", typeof(RectTransform), typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500; // above every game's UI, below nothing that takes touches (no raycaster)
            _root = (RectTransform)canvasGo.transform;

            _flash = NewImage("Lose Flash", EdgeSprite());
            var fr = _flash.rectTransform;
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = Vector2.one;
            fr.offsetMin = fr.offsetMax = Vector2.zero;
            _flash.color = Clear(FlashColor);
            _flash.enabled = false;

            _pieces = new Piece[ConfettiCount];
            for (int i = 0; i < _pieces.Length; i++)
            {
                var img = NewImage("Confetti", null);
                img.rectTransform.anchorMin = img.rectTransform.anchorMax = Vector2.zero;
                img.enabled = false;
                _pieces[i] = new Piece { Rect = img.rectTransform, Image = img };
            }
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            if (_instance != this) return;
            _instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // a new scene starts clean: no confetti from the last level, no stale pop targets
            _popBase.Clear();
            for (int i = 0; i < _active; i++) _pieces[i].Image.enabled = false;
            _active = 0;
        }

        Image NewImage(string name, Sprite sprite)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_root, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        static Color Clear(Color c) { c.a = 0f; return c; }

        // white edges fading to a clear centre, so the flash tints the screen's border and leaves the play area readable
        static Sprite EdgeSprite()
        {
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = Mathf.Abs(x + 0.5f - n * 0.5f) / (n * 0.5f), dy = Mathf.Abs(y + 0.5f - n * 0.5f) / (n * 0.5f);
                float d = Mathf.Max(dx, dy);
                px[y * n + x] = new Color32(255, 255, 255, (byte)(255 * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, d))));
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f));
        }

        /// <summary> Fires confetti from both bottom corners. Safe to call when no instance exists. </summary>
        public static void Confetti()
        {
            if (_instance && !CollectionFeedback.ReducedMotion) _instance.Burst();
        }

        /// <summary> Flashes the screen's edges red once. Safe to call when no instance exists. </summary>
        public static void LoseFlash()
        {
            if (!_instance) return;
            _instance._flashPeak = _instance.FlashAlpha * (CollectionFeedback.ReducedMotion ? 0.5f : 1f);
            _instance._flashT = 0f;
            _instance._flash.enabled = true;
        }

        /// <summary>
        /// Called by UniversalGameManager when a win or lose screen shows: its panels pop in one after another, and a
        /// win adds confetti on top.
        /// </summary>
        public static void MenuShown(GameObject menu, bool won)
        {
            if (!_instance || !menu) return;
            if (won) Confetti();
            if (!CollectionFeedback.ReducedMotion) _instance.Pop(menu.transform);
        }

        void Burst()
        {
            var size = _root.rect.size; float h = size.y, w = size.x; // canvas units: right whatever the canvas renders to
            for (int n = 0; n < _pieces.Length; n++)
            {
                int i = _active < _pieces.Length ? _active++ : Random.Range(0, _pieces.Length); // pool full: recycle one
                ref var p = ref _pieces[i];
                bool left = (n & 1) == 0;
                float angle = Random.Range(55f, 80f) * Mathf.Deg2Rad;
                float speed = Random.Range(1.5f, 2.3f); // high arcs: the pieces cross the title quickly, then rain down
                p.Velocity = new Vector2((left ? 1 : -1) * Mathf.Cos(angle) * speed * h / w, Mathf.Sin(angle) * speed);
                p.Age = 0f;
                p.Spin = Random.Range(-720f, 720f);
                p.Flutter = Random.Range(6f, 12f);
                p.Width = h * Random.Range(0.008f, 0.013f);
                p.Rect.sizeDelta = new Vector2(p.Width, p.Width * Random.Range(1.4f, 2.2f));
                p.Rect.anchoredPosition = new Vector2(left ? -p.Width : w + p.Width, h * Random.Range(0.02f, 0.12f));
                p.Rect.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
                p.Image.color = ConfettiColors[Random.Range(0, ConfettiColors.Length)];
                p.Image.enabled = true;
            }
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (_active > 0) StepConfetti(dt);
            if (_flashT >= 0f) StepFlash(dt);
        }

        void StepConfetti(float dt)
        {
            var size = _root.rect.size; float h = size.y, w = size.x; // canvas units: right whatever the canvas renders to
            for (int i = _active - 1; i >= 0; i--)
            {
                ref var p = ref _pieces[i];
                p.Age += dt;
                p.Velocity.y -= Gravity * dt;
                p.Velocity *= 1f - 1.4f * dt; // air drag: the burst slows and the pieces drift down
                var pos = p.Rect.anchoredPosition + new Vector2(p.Velocity.x * w, p.Velocity.y * h) * dt;
                p.Rect.anchoredPosition = pos;
                p.Rect.localRotation *= Quaternion.Euler(0, 0, p.Spin * dt);
                // flipping paper: squash the width on a sine so pieces seem to tumble
                p.Rect.localScale = new Vector3(Mathf.Abs(Mathf.Sin(p.Age * p.Flutter)) * 0.8f + 0.2f, 1f, 1f);
                var c = p.Image.color;
                c.a = Mathf.Clamp01((ConfettiLife - p.Age) / 0.5f);
                p.Image.color = c;
                if (p.Age < ConfettiLife && pos.y > -p.Width * 3f) continue;

                p.Image.enabled = false;
                var dead = p; // copy first: p aliases slot i, which the swap overwrites
                _pieces[i] = _pieces[--_active]; // swap-remove keeps the live pieces packed at the front
                _pieces[_active] = dead;
            }
        }

        void StepFlash(float dt)
        {
            _flashT += dt;
            float k = _flashT / FlashTime;
            // fast attack (first 20%), slow release: one flash, well under 3 per second
            float a = k < 0.2f ? k / 0.2f : 1f - Mathf.InverseLerp(0.2f, 1f, k);
            var c = FlashColor;
            c.a = _flashPeak * Mathf.Clamp01(a);
            _flash.color = c;
            if (k < 1f) return;
            _flashT = -1f;
            _flash.enabled = false;
        }

        void Pop(Transform menu)
        {
            var targets = new List<RectTransform>();
            CollectPopTargets(menu, targets, 0);
            for (int i = 0; i < targets.Count; i++)
                StartCoroutine(PopOne(targets[i], i * PopStagger));
        }

        // panels, titles and buttons pop; full-screen backdrops stay put (their children pop instead), and anything an
        // Animator drives is left to its animation
        void CollectPopTargets(Transform parent, List<RectTransform> into, int depth)
        {
            foreach (Transform child in parent)
            {
                if (into.Count >= MaxPopTargets) return;
                if (!child.gameObject.activeSelf || child is not RectTransform rt) continue;
                if (child.GetComponentInParent<Animator>() || child.GetComponent<UiPopIn>()) continue;
                bool backdrop = rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one;
                if (backdrop && depth < 2 && child.childCount > 0) CollectPopTargets(child, into, depth + 1);
                else if (!backdrop) into.Add(rt);
            }
        }

        IEnumerator PopOne(RectTransform target, float delay)
        {
            if (!_popBase.TryGetValue(target, out var baseScale))
                _popBase[target] = baseScale = target.localScale; // a pop cut short by another pop keeps the true size
            target.localScale = baseScale * PopFromScale;
            if (delay > 0f) target.localScale = Vector3.zero;
            for (float t = -delay; t < PopTime; t += Time.unscaledDeltaTime)
            {
                if (!target) yield break;
                if (t >= 0f)
                {
                    // ease-out-back, like the hub's UiPopIn
                    const float c1 = 1.70158f, c3 = c1 + 1f;
                    float k = t / PopTime - 1f;
                    float e = 1f + c3 * k * k * k + c1 * k * k;
                    target.localScale = baseScale * Mathf.LerpUnclamped(PopFromScale, 1f, e);
                }
                yield return null;
            }
            if (target) target.localScale = baseScale;
        }
    }
}
