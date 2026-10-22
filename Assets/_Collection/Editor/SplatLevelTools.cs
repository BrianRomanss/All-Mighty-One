using System.Collections.Generic;
using System.Text;
using AllMightyOne.Cubey;
using AllMightyOne.Splat;
using Solo.MOST_IN_ONE;
using UnityEditor;
using UnityEngine;

namespace AllMightyOne.EditorTools
{
    public static class SplatLevelTools
    {
        public const string SplatLevelsFolder = "Assets/Most In One/Grid/Scene/Splat Levels";
        const string BuilderPath = "Assets/Most In One/Grid/Scene/Builder profiles/SplatLevel Builder.asset";
        const string WallPath = "Assets/Most In One/Grid/Prefabs/Wall.prefab";
        const string StopPath = "Assets/Most In One/Grid/Prefabs/Splat/StopPointSplat.prefab";
        const string StartPath = "Assets/Most In One/Grid/Prefabs/Splat/StartBlock Splat.prefab";

        public static SplatBoard Board(GridLevelData data) => SplatBoard.From(CubeyLevelReader.Read(data));

        [MenuItem("All Mighty One/Splat/Validate Levels")]
        public static void ValidateMenu() => Validate();

        /// <summary> Every level must be solvable, trap-free and its tile count must match the level data the character counts down. </summary>
        public static bool Validate()
        {
            var report = new StringBuilder();
            bool allOk = true;
            foreach (var (number, path) in CubeyLevelTools.LevelPrefabs(SplatLevelsFolder, "SplatLevel"))
            {
                var data = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<GridLevelData>();
                var board = Board(data);
                var start = CubeyLevelReader.StartCell(data);
                int remaining = data.NumberOfEmptyCells + data.NumberCountedObjects;
                var sol = board.Solve(start, new bool[board.CellCount]);
                bool noTraps = board.NoTraps(start);
                bool ok = sol != null && noTraps && board.CountedTotal() == remaining;
                allOk &= ok;
                report.AppendLine($"Level {number}: {(ok ? "OK" : "FAIL")} moves={(sol == null ? -1 : sol.Count)} tiles={board.CountedTotal()} remaining={remaining} noTraps={noTraps}");
                if (!ok || number % 10 == 1) report.AppendLine(board.ToString());
            }
            Debug.Log($"[AMO] Splat validation {(allOk ? "PASSED" : "FAILED")}\n{report}");
            return allOk;
        }

        /// <summary> The kit's 5 levels are replaced too: 2-5 cannot be solved (tiles no slide passes over) and 1 has traps. </summary>
        [MenuItem("All Mighty One/Splat/Generate Levels 1-50")]
        public static void GenerateMenu() => Generate(1, 50, 5150);

        public static void BuildAllLevels()
        {
            GenerateMenu();
            Validate();
        }

        public static void Generate(int from, int to, int seed, float tMin = 0f, float tMax = 1f)
        {
            var builderSource = AssetDatabase.LoadAssetAtPath<GridLevelProfile>(BuilderPath);
            var wall = AssetDatabase.LoadAssetAtPath<GameObject>(WallPath);
            var stop = AssetDatabase.LoadAssetAtPath<GameObject>(StopPath);
            var start = AssetDatabase.LoadAssetAtPath<GameObject>(StartPath);
            if (!builderSource || !wall || !stop || !start) { Debug.LogError("[AMO] Splat: missing builder or prefabs."); return; }

            var rng = new System.Random(seed);
            var report = new StringBuilder();
            for (int level = from; level <= to; level++)
            {
                float t = Mathf.Lerp(tMin, tMax, from == to ? 0f : Mathf.InverseLerp(from, to, level));
                CubeyCell[] cells = null; Vector2Int startCell = default; int moves = 0;
                const int attempts = 6000;
                for (int attempt = 0; attempt < attempts && cells == null; attempt++)
                {
                    float relax = 1f - 0.4f * attempt / attempts;
                    cells = TryBuild(rng, t, relax, out startCell, out moves);
                }
                if (cells == null) { Debug.LogError($"[AMO] Splat: could not build level {level}"); continue; }

                var profile = Object.Instantiate(builderSource);
                profile.grid.Resize(Size, Size, false);
                profile.grid.ClearAll();
                for (int y = 0; y < Size; y++)
                    for (int x = 0; x < Size; x++)
                    {
                        switch (cells[y * Size + x])
                        {
                            case CubeyCell.Wall: profile.grid.Set(x, y, wall); break;
                            case CubeyCell.Stop: profile.grid.Set(x, y, stop); break;
                            case CubeyCell.Start: profile.grid.Set(x, y, start); break;
                        }
                    }
                profile.grid.MarkStartCell(startCell.x, startCell.y);
                profile.outputFolder = SplatLevelsFolder;
                profile.prefabNameOverride = $"SplatLevel {level}";
                profile.overwritePrefab = true;
                profile.GenerateLevelPrefab();
                Object.DestroyImmediate(profile);
                report.AppendLine($"SplatLevel {level}: t={t:0.00} moves={moves}");
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[AMO] Generated Splat levels {from}-{to}\n{report}");
        }

        const int Size = 9; // the scene camera is framed for the kit's 9x9 boards

        /// <summary>
        /// Carves a level by playing it forward: each random slide paints floor cells and the cell after it becomes a wall
        /// (or the slide ends on a stop point). Everything not carved is wall. Kept only when the model says it is solvable,
        /// trap-free and needs enough swipes.
        /// </summary>
        static CubeyCell[] TryBuild(System.Random rng, float t, float relax, out Vector2Int startCell, out int moves)
        {
            moves = 0;
            int targetCells = (int)((26 + t * 30 + rng.Next(0, 5)) * relax); // ~26..60 tiles
            float stopChance = 0.04f + 0.1f * t;
            int minMoves = (int)((6 + t * 12) * relax);

            var grid = new CubeyCell?[Size * Size]; // null = undecided
            int Idx(Vector2Int c) => c.y * Size + c.x;
            bool In(Vector2Int c) => c.x >= 0 && c.x < Size && c.y >= 0 && c.y < Size;

            var pos = new Vector2Int(rng.Next(Size), rng.Next(Size));
            startCell = pos;
            grid[Idx(pos)] = CubeyCell.Start;
            int carved = 0;
            Vector2Int lastDir = Vector2Int.zero;
            for (int guard = 0; guard < 500 && carved < targetCells; guard++)
            {
                var dir = CubeyBoard.Directions[rng.Next(4)];
                if (dir == lastDir) continue;
                int len = rng.Next(1, Size);
                var path = new List<Vector2Int>();
                var c = pos;
                for (int i = 0; i < len; i++)
                {
                    var n = c + dir;
                    if (!In(n) || grid[Idx(n)] == CubeyCell.Wall) break;
                    c = n;
                    path.Add(c);
                }
                if (path.Count == 0) continue;
                var after = c + dir;
                bool endOnStop = grid[Idx(c)] == null && rng.NextDouble() < stopChance;
                bool afterOk = !In(after) || grid[Idx(after)] == null || grid[Idx(after)] == CubeyCell.Wall;
                if (!endOnStop && !afterOk) continue; // the slide would run on over an open cell

                foreach (var p in path) if (grid[Idx(p)] == null) { grid[Idx(p)] = CubeyCell.Floor; carved++; }
                if (endOnStop) grid[Idx(c)] = CubeyCell.Stop;
                else if (In(after)) grid[Idx(after)] = CubeyCell.Wall;
                pos = c;
                lastDir = dir;
            }
            if (carved < targetCells * 0.85f) return null;

            var cells = new CubeyCell[Size * Size];
            for (int i = 0; i < cells.Length; i++) cells[i] = grid[i] ?? CubeyCell.Wall;
            var board = new SplatBoard(Size, Size, cells);
            if (!board.NoTraps(startCell)) return null;
            var solution = board.Solve(startCell, new bool[cells.Length]);
            if (solution == null || solution.Count < minMoves) return null;
            moves = solution.Count;
            return cells;
        }
    }
}
