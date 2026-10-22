using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static AllMightyOne.EditorTools.RunnerLevelGen;

namespace AllMightyOne.EditorTools
{
    public static partial class BigBrainLevelTools
    {
        const float TrackStart = 9f, TrackEnd = 124f; // EndLine is at z=134
        const float BaseScale = 2.5f;                 // head scale without upgrades
        const float GateRescale = 0.05f;              // MOST_Gate.AmountRescale of TwoGates_BigBrain
        const float MaxScale = 12f;                   // CharacterControl_BigBrain.MaxHeadScale
        // CharacterControl_BigBrain: obstacle layers 7/8/9 shrink the head by 15/25/40 (divided by 40)
        static readonly Dictionary<string, float> ObstacleCost = new() { ["Cone"] = 15 / 40f, ["StopSign"] = 25 / 40f, ["Barrier"] = 40 / 40f };

        [MenuItem("All Mighty One/Big Brain/Generate Levels 4-30")]
        public static void GenerateMenu() => Generate(4, 30, 7070);

        /// <summary>
        /// Builds levels from the hand-made ones: BigBrain_Level_1 gives the track (EndLine, Road, multiplier stairs) and the palette
        /// (gate pairs, Cone/StopSign/Barrier obstacles, gems). Rows are gate pairs, full-width walls the player must go through
        /// and lane obstacles the player steers around. Gate values follow a simulated best path so every level can be won.
        /// </summary>
        public static void Generate(int from, int to, int seed)
        {
            var track = new RunnerLevelGen($"{Folder}/BigBrain_Level_1.prefab", "EndLine BigBrain", "Road", "Bonus");
            var palette = AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/BigBrain_Level_1.prefab").transform.Find("Level");
            Source Src(string path) { var t = palette.Find(path); return new() { Prefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(t.gameObject), Y = t.position.y }; }
            var gatePair = Src("Gates/TwoGates_BigBrain");
            var gem = Src("Gems/GemPref");
            var obstacles = new Dictionary<string, Source> { ["Cone"] = Src("Obst/Cone"), ["StopSign"] = Src("Obst/StopSign"), ["Barrier"] = Src("Obst/Barrier") };

            var rng = new System.Random(seed);
            var report = new System.Text.StringBuilder();
            for (int n = from; n <= to; n++)
            {
                float t = Mathf.InverseLerp(from, to, n);
                var root = track.NewLevel($"BigBrain_Level_{n}");
                var level = new GameObject("Level").transform;
                level.SetParent(root.transform, false);
                level.SetAsFirstSibling();
                var obst = Group(level, "Obst");
                var gems = Group(level, "Gems");
                var gates = Group(level, "Gates");

                var (end, low) = Build(rng, t, gates, gems, obst, gatePair, gem, obstacles);
                report.AppendLine($"BigBrain_Level_{n}: best-path head at the end={end:0.##} (lowest {low:0.##}) ~{Mathf.CeilToInt(end / (35 / 40f))} bonus steps");
                Save(root, $"{Folder}/BigBrain_Level_{n}.prefab");
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[AMO] Generated Big Brain levels {from}-{to}\n{report}");
        }

        static (float end, float low) Build(System.Random rng, float t, Transform gates, Transform gems, Transform obst,
            Source gatePair, Source gem, Dictionary<string, Source> obstacles)
        {
            var slots = Slots(rng, TrackStart, TrackEnd, 7.5f, 10.5f);
            // full-width walls cost head size on every path; lane obstacles are free for a player who steers
            var walls = Spread(slots, 1 + Mathf.RoundToInt(t * 3f), 20f);
            var lanes = new HashSet<int>();
            for (int k = 0, want = 2 + Mathf.RoundToInt(t * 4f); k < 60 && lanes.Count < want; k++)
            {
                int i = rng.Next(1, slots.Count);
                if (!walls.Contains(i) && !walls.Contains(i - 1) && !lanes.Contains(i - 1)) lanes.Add(i);
            }

            float best = BaseScale, low = BaseScale;
            for (int i = 0; i < slots.Count; i++)
            {
                float z = slots[i];
                if (walls.Contains(i))
                {
                    // the cheapest wall that keeps the best path alive; harder levels use pricier walls
                    var kind = PickWall(rng, t, best);
                    foreach (float x in new[] { -1.75f, 0f, 1.75f }) Place(obstacles[kind], obst, x, z);
                    best -= ObstacleCost[kind];
                    low = Mathf.Min(low, best);
                    continue;
                }
                if (lanes.Contains(i))
                {
                    PlaceLaneObstacles(rng, t, obstacles, obst, z);
                    continue;
                }

                var (good, bad) = GatePair(rng, t, best);
                var pair = Place(gatePair, gates, 0f, z);
                SetGates(pair, rng.NextDouble() < 0.5 ? new[] { good, bad } : new[] { bad, good });
                best = Mathf.Min(MaxScale, Mathf.Max(Apply(good, best), Apply(bad, best)));

                if (rng.NextDouble() < 0.5) // gems between rows, on the side the player has to choose
                {
                    float x = rng.NextDouble() < 0.5 ? -1.5f : 1.5f;
                    for (int k = 0; k < 3; k++) Place(gem, gems, x, z + 2.5f + 1.5f * k);
                }
            }
            return (best, low);
        }

        static string PickWall(System.Random rng, float t, float head)
        {
            string kind = rng.NextDouble() < t ? (rng.NextDouble() < 0.5 ? "Barrier" : "StopSign") : (rng.NextDouble() < 0.6 ? "Cone" : "StopSign");
            // never a wall that the best path cannot survive with a margin
            if (head - ObstacleCost[kind] < 1f) kind = head - ObstacleCost["StopSign"] >= 1f ? "StopSign" : "Cone";
            return kind;
        }

        // one or two lanes blocked; at least one lane (left, centre or right) stays open
        static void PlaceLaneObstacles(System.Random rng, float t, Dictionary<string, Source> obstacles, Transform parent, float z)
        {
            double r = rng.NextDouble();
            if (r < 0.4) // a barrier in one half
            {
                Place(obstacles["Barrier"], parent, rng.NextDouble() < 0.5 ? -1f : 1f, z);
            }
            else if (r < 0.75) // two of three lanes blocked by stop signs or cones
            {
                int open = rng.Next(3);
                string kind = rng.NextDouble() < 0.5 + 0.3 * t ? "StopSign" : "Cone";
                float[] xs = kind == "StopSign" ? new[] { -2f, 0f, 2f } : new[] { -1.75f, 0f, 1.75f };
                for (int l = 0; l < 3; l++) if (l != open) Place(obstacles[kind], parent, xs[l], z);
            }
            else // slalom: a barrier, then another one on the other side
            {
                float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                Place(obstacles["Barrier"], parent, side, z);
                Place(obstacles["Barrier"], parent, -side, z + 4f);
            }
        }

        // a better and a worse gate, in gate units (x AmountRescale = head scale)
        static (string good, string bad) GatePair(System.Random rng, float t, float head)
        {
            string good = head < 4f && rng.NextDouble() < 0.2 ? "x2" : "+" + 5 * (2 + rng.Next(2 + (int)((1 - t) * 2.5f)));
            double r = rng.NextDouble();
            string bad;
            float maxLoss = Mathf.Max(5, Mathf.Floor((head - 0.5f) / GateRescale / 5) * 5); // a wrong gate hurts but does not end the run
            if (r < 0.45 + 0.25 * t) bad = "-" + Mathf.Min(maxLoss, 5 * (2 + rng.Next(2 + (int)(t * 8))));
            else if (r < 0.6 + 0.25 * t) bad = head > 3f && rng.NextDouble() < 0.3 ? "÷3" : "÷2";
            else bad = "+" + (1 + rng.Next(5));
            return (good, bad);
        }

        static float Apply(string gate, float head)
        {
            float v = float.Parse(gate.Substring(1), System.Globalization.CultureInfo.InvariantCulture);
            return gate[0] switch
            {
                '+' => head + v * GateRescale,
                '-' => Mathf.Max(0, head - v * GateRescale),
                'x' => head * v,
                '÷' => head / v,
                _ => head
            };
        }

        static Transform Group(Transform parent, string name)
        {
            var g = new GameObject(name).transform;
            g.SetParent(parent, false);
            return g;
        }

        static GameObject Place(Source s, Transform parent, float x, float z)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(s.Prefab, parent);
            go.transform.position = new Vector3(x, s.Y, z);
            return go;
        }

        // writes the structured gate fields (and the legacy text the kit keeps in sync), so no runtime re-parse is needed
        static void SetGates(GameObject pair, string[] texts)
        {
            var gatesInPair = pair.GetComponentsInChildren<Component>(true).Where(c => c && c.GetType().Name == "MOST_Gate").ToList();
            for (int i = 0; i < gatesInPair.Count && i < texts.Length; i++)
            {
                string text = texts[i];
                int op = text[0] switch { '+' => 0, '-' => 1, 'x' => 2, '÷' => 3, _ => 0 };
                var so = new SerializedObject(gatesInPair[i]);
                so.FindProperty("GateOperation").enumValueIndex = op;
                so.FindProperty("GateValue").floatValue = float.Parse(text.Substring(1), System.Globalization.CultureInfo.InvariantCulture);
                so.FindProperty("GateText").stringValue = text;
                so.FindProperty("_lastSyncedGateText").stringValue = text;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
