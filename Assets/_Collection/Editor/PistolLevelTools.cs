using System.Linq;
using UnityEditor;
using UnityEngine;
using static AllMightyOne.EditorTools.RunnerLevelGen;

namespace AllMightyOne.EditorTools
{
    /// <summary>
    /// Generates Pistol levels from the hand-made Pistol_Level 1 (its road and finish line are kept) using the same prefabs.
    /// The kit's PistolProfile cannot do this: its row step equals the whole track length.
    /// </summary>
    public static class PistolLevelTools
    {
        public const string Folder = "Assets/Most In One/Runner/Scenes/Levels/Pistol";
        const float TrackStart = 12f, TrackEnd = 124f;

        [MenuItem("All Mighty One/Pistol/Generate Levels 4-30")]
        public static void GenerateMenu() => Generate(4, 30, 3030);

        public static void Generate(int from, int to, int seed)
        {
            var gen = new RunnerLevelGen(Folder + "/Pistol_Level 1.prefab", "RoadGr", "EndLine");
            if (!gen.Has("Gem", "Gate_Barrel", "Gate_Barrel_FireRate", "Gate_Barrel_FireRange", "Gate_Barrel_Currency",
                "Gate_Fixed_FireRate", "Gate_Fixed_FireRange", "Gate_FireRate", "Gate_FireRange", "Gate_Health", "EnemyGun1_ShootRunner")) return;

            var rng = new System.Random(seed);
            for (int level = from; level <= to; level++)
            {
                float t = Mathf.InverseLerp(from, to, level);
                var root = gen.NewLevel($"Pistol_Level {level}");
                BuildRows(gen, root.transform, rng, t);
                Save(root, $"{Folder}/Pistol_Level {level}.prefab");
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[AMO] Generated Pistol levels {from}-{to}");
        }

        static void BuildRows(RunnerLevelGen gen, Transform root, System.Random rng, float t)
        {
            var slots = Slots(rng, TrackStart, TrackEnd, 8f, 12f);
            // enemy rows: 1 at the start of the curve, up to 4 on the hardest levels, never in the first 35m
            var enemySlots = Spread(slots, 1 + Mathf.RoundToInt(t * 3f), 35f);

            for (int i = 0; i < slots.Count; i++)
            {
                float z = slots[i];
                if (enemySlots.Contains(i))
                {
                    bool three = t > 0.6f && rng.NextDouble() < t - 0.4f;
                    foreach (var x in three ? new[] { -1.5f, 0f, 1.5f } : new[] { -1.25f, 1.25f })
                        gen.Place(root, "EnemyGun1_ShootRunner", x, z);
                    continue;
                }

                double r = rng.NextDouble();
                if (r < 0.32) GatePair(gen, root, rng, t, z);
                else if (r < 0.52) FixedGate(gen, root, rng, t, z);
                else if (r < 0.80) BarrelPair(gen, root, rng, t, z);
                else GemLine(gen, root, rng, z);
            }
        }

        // two shootable gates that start negative: the player picks a side and shoots it to make it positive
        static void GatePair(RunnerLevelGen gen, Transform root, System.Random rng, float t, float z)
        {
            string[] kinds = { "Gate_FireRate", "Gate_FireRange", "Gate_Health" };
            var a = kinds[rng.Next(kinds.Length)];
            var b = kinds.Where(k => k != a).ElementAt(rng.Next(2));
            gen.PlaceGate(root, a, -1.25f, z, NegativeGateText(a, rng, t));
            gen.PlaceGate(root, b, 1.25f, z, NegativeGateText(b, rng, t));
        }

        static string NegativeGateText(string kind, System.Random rng, float t)
        {
            if (kind == "Gate_Health") return "-" + Num(5 * (1 + rng.Next(2 + (int)(t * 5))));
            float max = 0.4f + t * 1.6f;
            return "-" + Num(Mathf.Max(0.1f, Mathf.Round((float)rng.NextDouble() * max * 10f) / 10f));
        }

        // walk-through gate with a fixed bonus; harder levels add negative traps
        static void FixedGate(RunnerLevelGen gen, Transform root, System.Random rng, float t, float z)
        {
            bool rate = rng.NextDouble() < 0.5;
            bool trap = rng.NextDouble() < 0.15 + 0.35 * t;
            float x = new[] { -1f, 0f, 1f }[rng.Next(3)];
            string text = rate ? (trap ? "-1" : "+1") : (trap ? "-0.5" : "+0.5");
            gen.PlaceGate(root, rate ? "Gate_Fixed_FireRate" : "Gate_Fixed_FireRange", x, z, text);
        }

        // shootable barrels: gems, or a stat boost when destroyed
        static void BarrelPair(RunnerLevelGen gen, Transform root, System.Random rng, float t, float z)
        {
            foreach (var x in new[] { -1.25f, 1.25f })
            {
                switch (rng.Next(4))
                {
                    case 0: gen.PlaceGate(root, "Gate_Barrel", x, z, "+" + Num(5 + 5 * rng.Next(1 + (int)(t * 15)))); break;
                    case 1: gen.PlaceGate(root, "Gate_Barrel_FireRate", x, z, "+" + Num(3 + rng.Next(8)), "+" + Num(1 + rng.Next(3) * 0.25f)); break;
                    case 2: gen.PlaceGate(root, "Gate_Barrel_FireRange", x, z, "+" + Num(15 + 5 * rng.Next(4)), "+0.5"); break;
                    default: gen.PlaceGate(root, "Gate_Barrel_Currency", x, z, "+" + Num(30 + 5 * rng.Next(4)), "+" + Num(5 + 5 * rng.Next(2))); break;
                }
            }
        }

        static void GemLine(RunnerLevelGen gen, Transform root, System.Random rng, float z)
        {
            float x = new[] { -1.5f, 0f, 1.5f }[rng.Next(3)];
            for (int k = 0; k < 3; k++) gen.Place(root, "Gem", x, z + 2f * k);
        }
    }
}
