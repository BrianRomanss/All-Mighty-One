using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AllMightyOne.EditorTools
{
    /// <summary> Helpers used by the per-game setup scripts to wire scenes from code (idempotent: safe to re-run). </summary>
    public static class CollectionSceneUtil
    {
        public static Scene Open(string path) => EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

        public static void Save(Scene scene)
        {
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        /// <summary> Finds a GameObject by name in the open scenes, including inactive ones. Optional parent path "Parent/Child". </summary>
        public static GameObject Find(string path)
        {
            var parts = path.Split('/');
            var candidates = Resources.FindObjectsOfTypeAll<GameObject>()
                .Where(g => g.scene.IsValid() && g.name == parts[parts.Length - 1]);
            foreach (var c in candidates)
            {
                var t = c.transform;
                bool match = true;
                for (int i = parts.Length - 2; i >= 0 && match; i--)
                {
                    t = t.parent;
                    match = t && t.name == parts[i];
                }
                if (match) return c;
            }
            return null;
        }

        public static T FindComponent<T>() where T : Component =>
            Resources.FindObjectsOfTypeAll<T>().FirstOrDefault(c => c.gameObject.scene.IsValid());

        public static void DestroyChild(Transform parent, string name)
        {
            foreach (Transform c in parent.Cast<Transform>().ToList())
                if (c.name == name) UnityEngine.Object.DestroyImmediate(c.gameObject);
        }

        /// <summary> Creates a rounded text button (built-in UI sprite) styled with an existing Text's font. </summary>
        public static Button CreateTextButton(Transform parent, string name, string label, Vector2 anchoredPos, Vector2 size,
            Text fontSource, Color background, Color textColor)
        {
            DestroyChild(parent, name);
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.layer = parent.gameObject.layer;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = size;

            var img = go.GetComponent<Image>();
            img.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 0.25f;
            img.color = background;

            var textGo = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textGo.layer = go.layer;
            var trt = (RectTransform)textGo.transform;
            trt.SetParent(rt, false);
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            var text = textGo.GetComponent<Text>();
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = textColor;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 10;
            text.resizeTextMaxSize = (int)(size.y * 0.55f);
            if (fontSource)
            {
                text.font = fontSource.font;
                text.fontStyle = fontSource.fontStyle;
            }
            text.raycastTarget = false;
            var outline = textGo.AddComponent<UnityEngine.UI.Outline>(); // "Outline" alone resolves to the Quick Outline asset
            outline.effectColor = new Color(0, 0, 0, 0.5f);
            outline.effectDistance = new Vector2(3, -3);
            return go.GetComponent<Button>();
        }

        /// <summary> Duplicates an existing styled button next to it, relabels it and removes its click wiring. </summary>
        public static Button CloneButton(GameObject source, string name, string label, Vector2 anchoredPos)
        {
            var parent = source.transform.parent;
            DestroyChild(parent, name);
            var go = UnityEngine.Object.Instantiate(source, parent);
            go.name = name;
            ((RectTransform)go.transform).anchoredPosition = anchoredPos;
            foreach (var t in go.GetComponentsInChildren<TMPro.TMP_Text>(true)) t.text = label;
            foreach (var t in go.GetComponentsInChildren<Text>(true)) t.text = label;
            var button = go.GetComponent<Button>();
            for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(button.onClick, i);
            return button;
        }

        /// <summary> Adds a persistent (serialized) void listener, so the wiring survives in the scene file. </summary>
        public static void AddListener(UnityEvent evt, UnityEngine.Object target, string method)
        {
            var mi = target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.Public, null, Type.EmptyTypes, null);
            if (mi == null) throw new Exception($"{target.GetType().Name}.{method}() not found");
            RemoveListener(evt, target, method);
            UnityEventTools.AddVoidPersistentListener(evt, (UnityAction)Delegate.CreateDelegate(typeof(UnityAction), target, mi));
        }

        public static void RemoveListener(UnityEvent evt, UnityEngine.Object target, string method)
        {
            for (int i = evt.GetPersistentEventCount() - 1; i >= 0; i--)
                if (evt.GetPersistentTarget(i) == target && evt.GetPersistentMethodName(i) == method)
                    UnityEventTools.RemovePersistentListener(evt, i);
        }

        public static void AddScenesToBuild(IEnumerable<string> paths)
        {
            var list = EditorBuildSettings.scenes.ToList();
            foreach (var p in paths)
                if (!list.Any(s => s.path == p)) list.Add(new EditorBuildSettingsScene(p, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        /// <summary> Copies an existing legacy Text as a centred label (font, outline and shadow kept). </summary>
        public static Text LegacyLabel(GameObject source, Transform parent, string name, string text, Vector2 pos, int size, Color color)
        {
            DestroyChild(parent, name);
            var go = UnityEngine.Object.Instantiate(source, parent);
            go.name = name;
            go.SetActive(true);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(1400, size * 2.6f);
            var t = go.GetComponent<Text>();
            t.text = text;
            t.fontSize = size;
            t.resizeTextForBestFit = false;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;
            return t;
        }

        public static readonly Color AdButtonColor = new(1f, 0.78f, 0.15f, 1f);
        public static readonly Color AdTextColor = new(0.36f, 0.17f, 0.02f, 1f); // dark brown: white was unreadable on the yellow
    }
}
