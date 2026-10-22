using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static AllMightyOne.EditorTools.RunnerLevelGen;

namespace AllMightyOne.EditorTools
{
    /// <summary>
    /// Builds extra 60 m track pieces for 3 Roads from Sub_1 (its three roads are kept, its boxes / spikes / gates / gems are the palette).
    /// Every row blocks at most one rail per lane (or puts a timed spike pair in it), so each runner always has a way through;
    /// harder tiers have closer rows, more blocked lanes and spikes, and only appear further into the run (TrackPart.MinZ).
    /// </summary>
    public static class ThreeRoadsTrackGenerator
    {
        public const string PartsFolder = "Assets/Most In One/ThreeRoads/Scene/Parts";
        const float FirstRow = 4f, LastRow = 56f; // pieces are 60 m long
        static readonly float[] LaneX = { -2.3f, 0f, 2.3f };
        const float RailOffset = 0.5f; // ThreeRoadsController lanes: Width 1, the runner sits on CenterX +- 0.5

        public struct Tier
        {
            public string Name; public float MinZ, MaxZ, GapMin, GapMax, BlockChance, SpikeChance, GateChance, GemChance; public int Count;
        }

        public static readonly Tier[] Tiers =
        {
            new() { Name = "Easy",   MinZ = 0,   MaxZ = 900, GapMin = 7f,   GapMax = 9f,   BlockChance = 0.4f,  SpikeChance = 0f,    GateChance = 0.7f, GemChance = 0.35f, Count = 3 },
            new() { Name = "Medium", MinZ = 360, GapMin = 5.5f, GapMax = 7.5f, BlockChance = 0.55f, SpikeChance = 0.15f, GateChance = 0.5f, GemChance = 0.3f,  Count = 3 },
            new() { Name = "Hard",   MinZ = 900, GapMin = 4.5f, GapMax = 6f,   BlockChance = 0.7f,  SpikeChance = 0.25f, GateChance = 0.4f, GemChance = 0.25f, Count = 3 },
        };

        

        [MenuItem("All Mighty One/3 Roads/Generate Track Pieces")]
        public static void GenerateMenu() => Generate(9090);

        public static void Generate(int seed)
        {
            var track = new RunnerLevelGen($"{PartsFolder}/Sub_1.prefab", "Road-1", "RoadGr+1", "RoadGr_0");
            if (!track.Has("Gem", "Gate", "BoxLeft", "BoxRight", "SwappedSpike")) return;
            var rng = new System.Random(seed);
            var report = new System.Text.StringBuilder();
            foreach (var tier in Tiers)
            {
                for (int k = 1; k <= tier.Count; k++)
                {
                    string name = $"Sub_Gen_{tier.Name}_{k}";
                    var root = track.NewLevel(name);
                    var (rows, blocks, spikes, gates, gems) = Build(rng, tier, track, root.transform);
                    string path = $"{PartsFolder}/{name}.prefab";
                    Save(root, path);
                    report.AppendLine($"{name}: rows={rows} blocked rails={blocks} spikes={spikes} gates={gates} gems={gems}");
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[AMO] Generated 3 Roads track pieces\n{report}");
        }

        static (int rows, int blocks, int spikes, int gates, int gems) Build(System.Random rng, Tier tier, RunnerLevelGen track, Transform root)
        {
            var slots = Slots(rng, FirstRow, LastRow, tier.GapMin, tier.GapMax);
            int gateRow = rng.NextDouble() < tier.GateChance && slots.Count > 2 ? rng.Next(1, slots.Count - 1) : -1;
            int gateLane = rng.Next(3);
            var lastWasSpike = new bool[3];
            int blocks = 0, spikes = 0, gems = 0;

            for (int i = 0; i < slots.Count; i++)
            {
                float z = slots[i];
                for (int lane = 0; lane < 3; lane++)
                {
                    float cx = LaneX[lane];
                    if (i == gateRow && lane == gateLane)
                    {
                        track.Place(root, "Gate", cx, z);
                        lastWasSpike[lane] = false;
                        continue;
                    }
                    if (rng.NextDouble() < tier.BlockChance)
                    {
                        // a spike pair (one rail up at a time, swapping every 1.5 s) or a box on one rail
                        if (!lastWasSpike[lane] && rng.NextDouble() < tier.SpikeChance)
                        {
                            track.Place(root, "SwappedSpike", cx, z);
                            lastWasSpike[lane] = true;
                            spikes++;
                            continue;
                        }
                        // BoxLeft's cube sits on the right rail, BoxRight's on the left rail
                        track.Place(root, rng.NextDouble() < 0.5 ? "BoxLeft" : "BoxRight", cx, z);
                        lastWasSpike[lane] = false;
                        blocks++;
                        continue;
                    }
                    lastWasSpike[lane] = false;
                    if (rng.NextDouble() < tier.GemChance) // a short gem line on one rail of an empty lane
                    {
                        float x = cx + (rng.NextDouble() < 0.5 ? -RailOffset : RailOffset);
                        for (int g = -1; g <= 1; g++) track.Place(root, "Gem", x, z + 1.2f * g);
                        gems += 3;
                    }
                }
            }
            return (slots.Count, blocks, spikes, gateRow >= 0 ? 1 : 0, gems);
        }
    }
}
