using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AllMightyOne
{
    /// <summary> Fades the screen out, loads a scene, and fades back in. Created on first use and kept across scenes. </summary>
    public class SceneFader : MonoBehaviour
    {
        public static float FadeOut = 0.22f, FadeIn = 0.3f;
        public static Color FadeColor = new(0.16f, 0.12f, 0.36f, 1f);

        static SceneFader _instance;
        CanvasGroup _group;
        bool _busy;

        public static bool IsLoading => _instance && _instance._busy;

        public static void Load(string scene)
        {
            Ensure();
            if (_instance._busy) return;
            _instance.StartCoroutine(_instance.Run(scene));
        }

        static void Ensure()
        {
            if (_instance) return;
            var go = new GameObject("Scene Fader", typeof(Canvas), typeof(CanvasGroup));
            DontDestroyOnLoad(go);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            var img = new GameObject("Fade", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(go.transform, false);
            var rt = img.rectTransform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            img.color = FadeColor;
            _instance = go.AddComponent<SceneFader>();
            _instance._group = go.GetComponent<CanvasGroup>();
            _instance._group.alpha = 0;
            _instance._group.blocksRaycasts = false;
        }

        IEnumerator Run(string scene)
        {
            _busy = true;
            _group.blocksRaycasts = true;
            yield return Fade(0, 1, FadeOut);
            var op = SceneManager.LoadSceneAsync(scene);
            while (op != null && !op.isDone) yield return null;
            yield return null;
            yield return Fade(1, 0, FadeIn);
            _group.blocksRaycasts = false;
            _busy = false;
        }

        IEnumerator Fade(float from, float to, float time)
        {
            for (float t = 0; t < time; t += Time.unscaledDeltaTime)
            {
                _group.alpha = Mathf.Lerp(from, to, t / time);
                yield return null;
            }
            _group.alpha = to;
        }
    }
}
