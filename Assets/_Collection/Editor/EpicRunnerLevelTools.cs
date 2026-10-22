using UnityEditor;
using UnityEngine;
using static AllMightyOne.EditorTools.RunnerLevelGen;

namespace AllMightyOne.EditorTools
{
    /// <summary>
    /// Generates Squad Runner (EpicRunner) levels from the hand-made EpicRunner_Level 1: its EndLine (with the bonus gem barrels)
    /// is kept, rows are built from the same prefabs. Lanes are x = -2 / 0 / 2 like the hand-made levels.
    /// </summary>
    public static class EpicRunnerLevelTools
    {
        public const string Folder = "Assets/Most In One/Runner/Scenes/Levels/EpicRunner";
        const float TrackStart = 12f, TrackEnd = 126f;
        static readonly float[] Lanes = { -2f, 0f, 2f };

        [MenuItem("All Mighty One/Squad Runner/Generate Levels 4-30")]
        public static void GenerateMenu() => Generate(4, 30, 4040);

        public static void Generate(int from, int to, int seed)
        {
            var gen = new RunnerLevelGen(Folder + "/EpicRunner_Level 1.prefab", "EndLine");
            if (!gen.Has("Gem", "Gate_Barrel", "Gate_Barrel_Currency", "Gate_Barrel_Upgrade", "Gate_Fixed_AddChilds", "Gate_Childs",
                "Gate_Fixed_Heal", "Gate_Health", "EnemyGun1_ShootRunner", "EnemyNoGun")) return;

            var rng = new System.Random(seed);
            for (int level = from; level <= to; level++)
            {
                float t = Mathf.InverseLerp(from, to, level);
                var root = gen.NewLevel($"EpicRunner_Level {level}");
                BuildRows(gen, root.transform, rng, t);
                Save(root, $"{Folder}/EpicRunner_Level {level}.prefab");
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[AMO] Generated Squad Runner levels {from}-{to}");
        }

        static void BuildRows(RunnerLevelGen gen, Transform root, System.Random rng, float t)
        {
            var slots = Slots(rng, TrackStart, TrackEnd, 9f, 13f);
            var enemySlots = Spread(slots, 1 + Mathf.RoundToInt(t * 3f), 23f);
            var upgradeSlots = Spread(slots, t < 0.5f ? 1 : 2, 30f);

            for (int i = 0; i < slots.Count; i++)
            {
                float z = slots[i];
                if (i == 0) { AddChildsChoice(gen, root, rng, t, z); continue; } // grow the squad early
                if (enemySlots.Contains(i)) { EnemyRow(gen, root, rng, t, z); continue; }
                if (upgradeSlots.Contains(i))
                {
                    // tier-up barrel: toughness rises along the track and with difficulty
                    float hp = 7 + 48 * (z / TrackEnd) * (0.5f + t);
                    gen.PlaceGate(root, "Gate_Barrel_Upgrade", Lanes[rng.Next(3)], z, "+" + Num(Mathf.Round(hp)), "+10");
                    continue;
                }

                double r = rng.NextDouble();
                if (r < 0.30) AddChildsChoice(gen, root, rng, t, z);
                else if (r < 0.45) ShootableChilds(gen, root, rng, t, z);
                else if (r < 0.60) HealthRow(gen, root, rng, t, z);
                else if (r < 0.80) CurrencyBarrels(gen, root, rng, t, z);
                else GemGrid(gen, root, rng, z);
            }
        }

        // two walk-through gates: a good and a worse option
        static void AddChildsChoice(RunnerLevelGen gen, Transform root, System.Random rng, float t, float z)
        {
            int good = 3 + rng.Next(3 + (int)(t * 4));
            int bad = rng.NextDouble() < 0.4 + 0.4 * t ? -(2 + rng.Next(2 + (int)(t * 4))) : 1 + rng.Next(2);
            bool goodLeft = rng.NextDouble() < 0.5;
            gen.PlaceGate(root, "Gate_Fixed_AddChilds", goodLeft ? -2f : 2f, z, Signed(good));
            gen.PlaceGate(root, "Gate_Fixed_AddChilds", goodLeft ? 2f : -2f, z, Signed(bad));
        }

        // a shootable gate that starts negative, next to a modest fixed gate
        static void ShootableChilds(RunnerLevelGen gen, Transform root, System.Random rng, float t, float z)
        {
            bool left = rng.NextDouble() < 0.5;
            gen.PlaceGate(root, "Gate_Childs", left ? -2f : 2f, z, "-" + Num(3 + rng.Next(3 + (int)(t * 9))));
            gen.PlaceGate(root, "Gate_Fixed_AddChilds", left ? 2f : -2f, z, Signed(2 + rng.Next(3)));
        }

        static void HealthRow(RunnerLevelGen gen, Transform root, System.Random rng, float t, float z)
        {
            if (rng.NextDouble() < 0.5 - 0.2 * t)
            {
                gen.PlaceGate(root, "Gate_Fixed_Heal", Lanes[rng.Next(3)], z, "+10");
                return;
            }
            gen.PlaceGate(root, "Gate_Health", -2f, z, "-" + Num(1 + rng.Next(2 + (int)(t * 4))));
            gen.PlaceGate(root, "Gate_Health", 2f, z, "-" + Num(1 + rng.Next(2 + (int)(t * 4))));
        }

        static void CurrencyBarrels(RunnerLevelGen gen, Transform root, System.Random rng, float t, float z)
        {
            float x = Lanes[rng.Next(3)];
            if (rng.NextDouble() < 0.5)
                gen.PlaceGate(root, "Gate_Barrel", x, z, "+" + Num(3 + 5 * rng.Next(1 + (int)(5 + t * 15))));
            else
                gen.PlaceGate(root, "Gate_Barrel_Currency", x, z, "+" + Num(16 + rng.Next(10 + (int)(t * 20))), "+" + Num(3 + rng.Next(4)));
        }

        static void GemGrid(RunnerLevelGen gen, Transform root, System.Random rng, float z)
        {
            int cols = 1 + rng.Next(3);
            int start = rng.Next(3 - cols + 1);
            for (int c = 0; c < cols; c++)
                for (int k = 0; k < 2; k++) gen.Place(root, "Gem", Lanes[start + c], z + 4f * k);
        }

        static void EnemyRow(RunnerLevelGen gen, Transform root, System.Random rng, float t, float z)
        {
            double r = rng.NextDouble();
            if (t > 0.5f && r < t - 0.3f)
            {
                gen.Place(root, "EnemyGun1_ShootRunner", 0f, z + 6f);
                gen.Place(root, "EnemyNoGun", -2f, z);
                gen.Place(root, "EnemyNoGun", 2f, z);
            }
            else if (r < 0.5) { gen.Place(root, "EnemyNoGun", -2f, z); gen.Place(root, "EnemyNoGun", 2f, z); }
            else gen.Place(root, "EnemyGun1_ShootRunner", Lanes[rng.Next(3)], z);
        }
    }
}
