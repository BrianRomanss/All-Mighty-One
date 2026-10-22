using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static AllMightyOne.EditorTools.RunnerLevelGen;

namespace AllMightyOne.EditorTools
{
    public static partial class CountLevelTools
    {
        const float TrackStart = 6f, TrackEnd = 78f;
        const int BaseCrowd = 10; // the scene's StartAmount without upgrades

        [MenuItem("All Mighty One/Crowd Count/Generate Levels 4-30")]
        public static void GenerateMenu() => Generate(4, 30, 5050);

        /// <summary>
        /// Builds levels from the hand-made ones: Count_Level_1 gives the track (EndLine, Road, multiplier stairs),
        /// Count_Level_3 gives the palette (two-gate pairs, enemy zones, red walls, ramp, gems).
        /// Gate values and zone sizes follow a simulated best path so every level can be won with good choices.
        /// </summary>
        public static void Generate(int from, int to, int seed)
        {
            var track = new RunnerLevelGen($"{Folder}/Count_Level_1.prefab", "EndLine", "Road", "Bonus");
            var palette = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{Folder}/Count_Level_3.prefab"));
            try
            {
                var level = palette.transform.Find("Level");
                var gatePair = ToSource(level.Find("Gates").GetChild(0));
                var zone = ToSource(level.Find("Enemies").Cast<Transform>().First(t => t.name.StartsWith("EnemyZone")));
                var ramp = ToSource(level.Find("Ramp"));
                var gem = ToSource(level.Find("Gems").GetChild(0));
                // "RedWalls" is a series of DeadZone hazards: one in the centre right after the ramp, the rest in side lanes
                var deadZone = level.Find("Enemies/RedWalls").Cast<Transform>().First().gameObject;

                var rng = new System.Random(seed);
                var report = new System.Text.StringBuilder();
                for (int n = from; n <= to; n++)
                {
                    float t = Mathf.InverseLerp(from, to, n);
                    var root = track.NewLevel($"Count_Level_{n}");
                    var levelRoot = new GameObject("Level").transform;
                    levelRoot.SetParent(root.transform, false);
                    var gates = Group(levelRoot, "Gates");
                    var gems = Group(levelRoot, "Gems");
                    var enemies = Group(levelRoot, "Enemies");

                    var hazards = Group(enemies, "RedWalls");
                    int best = Build(rng, t, gates, gems, enemies, hazards, levelRoot, gatePair, zone, ramp, gem, deadZone, out int zones);
                    report.AppendLine($"Count_Level_{n}: zones={zones} best-path crowd at the end={best}");
                    Save(root, $"{Folder}/Count_Level_{n}.prefab");
                }
                AssetDatabase.SaveAssets();
                Debug.Log($"[AMO] Generated Crowd Count levels {from}-{to}\n{report}");
            }
            finally { Object.DestroyImmediate(palette); }
        }

        // the palette is an instance of a level prefab: resolve to the original (innermost) prefab asset, not the level
        static Source ToSource(Transform t) => new() { Prefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(t.gameObject), Y = t.position.y };

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

        static int Build(System.Random rng, float t, Transform gates, Transform gems, Transform enemies, Transform hazards, Transform levelRoot,
            Source gatePair, Source zone, Source ramp, Source gem, GameObject deadZone, out int zoneCount)
        {
            var slots = Slots(rng, TrackStart, TrackEnd, 7f, 10f);
            zoneCount = 1 + Mathf.RoundToInt(t * 2f);
            var zoneSlots = Spread(slots, zoneCount, 30f);
            // ramp jump: the ramp, then a centre dead zone 1.5m later (as in every hand-made level)
            int rampSlot = rng.NextDouble() < 0.7 ? slots.FindIndex(z => z >= 12f && z <= 35f && !zoneSlots.Contains(slots.IndexOf(z))) : -1;
            // side-lane hazards the player steers around; more on harder levels
            var hazardSlots = new HashSet<int>();
            for (int k = 0, want = Mathf.RoundToInt(t * 4f); k < 40 && hazardSlots.Count < want; k++)
            {
                int i = rng.Next(1, slots.Count);
                if (!zoneSlots.Contains(i) && i != rampSlot && !zoneSlots.Contains(i - 1)) hazardSlots.Add(i);
            }
            int best = BaseCrowd;

            for (int i = 0; i < slots.Count; i++)
            {
                float z = slots[i];
                if (zoneSlots.Contains(i))
                {
                    int max = Mathf.Max(5, Mathf.RoundToInt(best * (0.33f + 0.22f * t)));
                    int min = Mathf.Max(3, Mathf.RoundToInt(max * 0.8f));
                    var go = Place(zone, enemies, 0f, z);
                    SetZone(go, min, max);
                    best -= max; // worst case of the fight
                    continue;
                }
                if (i == rampSlot)
                {
                    var r = Place(ramp, levelRoot, 0f, z);
                    r.transform.SetSiblingIndex(0);
                    PlaceHazard(deadZone, hazards, 0f, z + 1.5f);
                    best = Mathf.RoundToInt(best * 0.95f); // a few runners miss the jump
                    continue;
                }
                if (hazardSlots.Contains(i))
                {
                    float side = rng.NextDouble() < 0.5 ? -1f : 1f;
                    PlaceHazard(deadZone, hazards, side * (1f + 0.75f * (float)rng.NextDouble()), z);
                    if (t > 0.5f && rng.NextDouble() < t - 0.3f) // a second hazard on the other side, a few meters on
                        PlaceHazard(deadZone, hazards, -side * (1f + 0.75f * (float)rng.NextDouble()), z + 4.5f);
                    best = Mathf.RoundToInt(best * 0.92f); // the widest runners clip the hazard
                    continue;
                }

                var (a, b) = GatePair(rng, t, best);
                var pair = Place(gatePair, gates, 0f, z);
                SetGates(pair, rng.NextDouble() < 0.5 ? new[] { a, b } : new[] { b, a });
                best = Mathf.Max(Apply(a, best), Apply(b, best));

                if (rng.NextDouble() < 0.45) // gems between rows
                {
                    float x = rng.NextDouble() < 0.5 ? -1.5f : 1.5f;
                    for (int k = 0; k < 3; k++) Place(gem, gems, x, z + 3f + 1.5f * k);
                }
            }

            return best;
        }

        static void PlaceHazard(GameObject deadZone, Transform parent, float x, float z)
        {
            var go = Object.Instantiate(deadZone, parent);
            go.name = "DeadZone";
            go.transform.position = new Vector3(x, deadZone.transform.position.y, z);
        }

        // a better and a worse gate; values scale with the crowd the player can have at this point
        static (string good, string bad) GatePair(System.Random rng, float t, int crowd)
        {
            string good = crowd < 70 && rng.NextDouble() < 0.3 ? "x2" : "+" + Num(5 * (2 + rng.Next(3 + (int)(t * 5))));
            double r = rng.NextDouble();
            string bad;
            if (r < 0.45 + 0.25 * t) bad = "-" + Num(Mathf.Clamp(5 * (1 + rng.Next(2 + (int)(t * 8))), 5, Mathf.Max(5, crowd - 1)));
            else if (r < 0.6 + 0.25 * t) bad = "x0.5";
            else bad = "+" + Num(1 + rng.Next(5));
            return (good, bad);
        }

        static int Apply(string gate, int crowd)
        {
            float v = float.Parse(gate.Substring(1), System.Globalization.CultureInfo.InvariantCulture);
            return gate[0] switch
            {
                '+' => crowd + (int)v,
                '-' => crowd - (int)v,
                'x' => (int)(crowd * v),
                _ => crowd
            };
        }

        static void SetGates(GameObject pair, string[] texts)
        {
            var gatesInPair = pair.GetComponentsInChildren<Component>(true).Where(c => c && c.GetType().Name == "MOST_Gate").ToList();
            for (int i = 0; i < gatesInPair.Count && i < texts.Length; i++)
            {
                var so = new SerializedObject(gatesInPair[i]);
                so.FindProperty("GateText").stringValue = texts[i];
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        static void SetZone(GameObject zoneGo, int min, int max)
        {
            foreach (var c in zoneGo.GetComponentsInChildren<Component>(true))
            {
                if (!c || c.GetType().Name != "EnemyZoneControl") continue;
                var so = new SerializedObject(c);
                var range = so.FindProperty("NumberOfChildren");
                range.FindPropertyRelative("min").intValue = min;
                range.FindPropertyRelative("max").intValue = max;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
