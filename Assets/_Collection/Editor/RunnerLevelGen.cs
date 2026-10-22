using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AllMightyOne.EditorTools
{
    /// <summary>
    /// Shared helpers for runner level generators: a hand-made level is used as the template (its track pieces are kept)
    /// and its prefab instances are the palette for new rows.
    /// </summary>
    public class RunnerLevelGen
    {
        public class Source { public GameObject Prefab; public float Y; }

        public readonly Dictionary<string, Source> Sources = new();
        readonly GameObject _template;
        readonly HashSet<string> _keep;

        public RunnerLevelGen(string templatePath, params string[] keepChildren)
        {
            _template = AssetDatabase.LoadAssetAtPath<GameObject>(templatePath);
            _keep = new HashSet<string>(keepChildren);
            foreach (Transform c in _template.transform)
            {
                var src = PrefabUtility.GetCorrespondingObjectFromSource(c.gameObject);
                if (src && !Sources.ContainsKey(src.name)) Sources[src.name] = new Source { Prefab = src, Y = c.position.y };
            }
        }

        public bool Has(params string[] names)
        {
            bool ok = true;
            foreach (var n in names)
                if (!Sources.ContainsKey(n)) { Debug.LogError($"[AMO] Template {_template.name} is missing prefab {n}"); ok = false; }
            return ok;
        }

        /// <summary> New level root: the template unpacked with only the kept children (track, finish line, bonus). </summary>
        public GameObject NewLevel(string name)
        {
            var root = (GameObject)PrefabUtility.InstantiatePrefab(_template);
            PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            root.name = name;
            foreach (Transform c in root.transform.Cast<Transform>().ToList())
                if (!_keep.Contains(c.name)) Object.DestroyImmediate(c.gameObject);
            return root;
        }

        public static void Save(GameObject root, string path)
        {
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        public GameObject Place(Transform root, string prefab, float x, float z)
        {
            var s = Sources[prefab];
            var go = (GameObject)PrefabUtility.InstantiatePrefab(s.Prefab, root);
            go.transform.position = new Vector3(x, s.Y, z);
            return go;
        }

        public GameObject PlaceGate(Transform root, string prefab, float x, float z, string gateText, string outputText = null)
        {
            var go = Place(root, prefab, x, z);
            SetGate(go, gateText, outputText);
            return go;
        }

        public static void SetGate(GameObject go, string gateText, string outputText)
        {
            foreach (var c in go.GetComponentsInChildren<Component>(true))
            {
                if (!c || c.GetType().Name != "MOST_Gate") continue;
                var so = new SerializedObject(c);
                so.FindProperty("GateText").stringValue = gateText;
                if (outputText != null) so.FindProperty("OutputText").stringValue = outputText;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        /// <summary> Row positions along the track, with a random gap in [minGap, maxGap]. </summary>
        public static List<float> Slots(System.Random rng, float start, float end, float minGap, float maxGap)
        {
            var slots = new List<float>();
            for (float z = start; z < end; z += minGap + (float)rng.NextDouble() * (maxGap - minGap)) slots.Add(z);
            return slots;
        }

        /// <summary> Picks `count` slot indices spread evenly over the slots at or after minZ. </summary>
        public static HashSet<int> Spread(List<float> slots, int count, float minZ)
        {
            var picked = new HashSet<int>();
            var candidates = Enumerable.Range(0, slots.Count).Where(i => slots[i] >= minZ).ToList();
            for (int k = 0; k < count && candidates.Count > 0; k++)
            {
                int pick = candidates[Mathf.Clamp((int)((k + 0.5f) / count * candidates.Count), 0, candidates.Count - 1)];
                picked.Add(pick);
                candidates.Remove(pick);
            }
            return picked;
        }

        public static string Num(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        public static string Signed(float v) => (v >= 0 ? "+" : "-") + Num(Mathf.Abs(v));
    }
}
