using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AllMightyOne.Cubey;
using Solo.MOST_IN_ONE;
using UnityEditor;
using UnityEngine;

namespace AllMightyOne.EditorTools
{
    public static class CubeyLevelTools
    {
        public const string CubeyLevelsFolder = "Assets/Most In One/Grid/Scene/Cubey Levels";
        const string BuilderPath = "Assets/Most In One/Grid/Scene/Builder profiles/CubeyLevel Builder.asset";
        const string WallPath = "Assets/Most In One/Grid/Prefabs/Wall.prefab";
        const string StopPath = "Assets/Most In One/Grid/Prefabs/Cubey/StopPoint.prefab";
        const string StartPath = "Assets/Most In One/Grid/Prefabs/Cubey/StartBlock Cubey.prefab";
        const string EndPath = "Assets/Most In One/Grid/Prefabs/Cubey/EndBlock Cubey.prefab";

        [MenuItem("All Mighty One/Cubey/Validate Levels")]
        public static void ValidateLevels() => ValidateFolder(CubeyLevelsFolder, "CubeyLevel");

        public static bool ValidateFolder(string folder, string prefix)
        {
            var report = new StringBuilder();
            bool allOk = true;
            foreach (var (number, path) in LevelPrefabs(folder, prefix))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var data = prefab ? prefab.GetComponent<GridLevelData>() : null;
                if (!data) { report.AppendLine($"Level {number}: no GridLevelData"); allOk = false; continue; }
                var board = CubeyLevelReader.Read(data);
                var start = CubeyLevelReader.StartCell(data);
                int remaining = data.NumberOfEmptyCells + data.NumberCountedObjects;
                int counted = board.CountedTotal();
                var sol = board.Solve(start, new bool[board.CellCount], remaining)
                          ?? board.Solve(start, new bool[board.CellCount], remaining, 20000000);
                bool ok = sol != null && counted == remaining;
                allOk &= ok;
                report.AppendLine($"Level {number}: {(ok ? "OK" : "FAIL")} moves={(sol == null ? -1 : sol.Count)} counted={counted} remaining={remaining} nodes={board.LastSearchNodes} exhausted={board.LastSearchExhausted}");
                if (!ok)
                {
                    report.AppendLine(board.ToString());
                    foreach (Transform t in prefab.transform.Find("Cells")) report.Append(t.name).Append(" | ");
                    report.AppendLine();
                }
            }
            Debug.Log($"[CubeyLevelTools] Validation {(allOk ? "PASSED" : "FAILED")}\n{report}");
            return allOk;
        }

        public static IEnumerable<(int number, string path)> LevelPrefabs(string folder, string prefix)
        {
            return AssetDatabase.FindAssets("t:Prefab", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(p => (n: ParseNumber(Path.GetFileNameWithoutExtension(p), prefix), p))
                .Where(t => t.n > 0)
                .OrderBy(t => t.n);
        }

        static int ParseNumber(string name, string prefix)
        {
            if (!name.StartsWith(prefix)) return -1;
            return int.TryParse(name.Substring(prefix.Length).Trim(), out int n) ? n : -1;
        }

        // ------------------------------------------------------------------ generation

        [MenuItem("All Mighty One/Cubey/Generate Levels 51-100")]
        public static void GenerateMenu() => GenerateLevels(51, 100, 12345, 0.4f, 1f);

        /// <summary> Template levels 39 and 46 are unsolvable (proven by exhaustive search); replace them. </summary>
        [MenuItem("All Mighty One/Cubey/Replace Unsolvable Levels")]
        public static void ReplaceUnsolvable()
        {
            GenerateLevels(39, 39, 3939, 0.6f, 0.6f);
            GenerateLevels(46, 46, 4646, 0.7f, 0.7f);
        }

        // Batch entry point: replace broken levels, generate 51-100, validate everything
        public static void BuildAllLevels()
        {
            ReplaceUnsolvable();
            GenerateMenu();
            ValidateLevels();
        }

        public static void GenerateLevels(int from, int to, int seed, float tMin = 0f, float tMax = 1f)
        {
            var builderSource = AssetDatabase.LoadAssetAtPath<GridLevelProfile>(BuilderPath);
            var wall = AssetDatabase.LoadAssetAtPath<GameObject>(WallPath);
            var stop = AssetDatabase.LoadAssetAtPath<GameObject>(StopPath);
            var start = AssetDatabase.LoadAssetAtPath<GameObject>(StartPath);
            var end = AssetDatabase.LoadAssetAtPath<GameObject>(EndPath);
            if (!builderSource || !wall || !stop || !start || !end) { Debug.LogError("[CubeyLevelTools] Missing builder or prefabs."); return; }

            var rng = new System.Random(seed);
            for (int level = from; level <= to; level++)
            {
                float t = Mathf.Lerp(tMin, tMax, from == to ? 0f : Mathf.InverseLerp(from, to, level)); // 0..1 difficulty
                CubeyCell[] cells = null; int cols = 0, rows = 0; Vector2Int startCell = default, endCell = default;
                const int attempts = 4000;
                for (int attempt = 0; attempt < attempts && cells == null; attempt++)
                {
                    float relax = 1f - 0.4f * attempt / attempts; // ask for fewer tiles the longer it fails
                    cells = TryBuild(rng, t, relax, out cols, out rows, out startCell, out endCell);
                }
                if (cells == null) { Debug.LogError($"[CubeyLevelTools] Could not build level {level}"); continue; }

                var profile = Object.Instantiate(builderSource);
                profile.grid.Resize(cols, rows, false);
                profile.grid.ClearAll();
                for (int y = 0; y < rows; y++)
                    for (int x = 0; x < cols; x++)
                    {
                        switch (cells[y * cols + x])
                        {
                            case CubeyCell.Wall: profile.grid.Set(x, y, wall); break;
                            case CubeyCell.Stop: profile.grid.Set(x, y, stop); break;
                            case CubeyCell.Start: profile.grid.Set(x, y, start); break;
                            case CubeyCell.End: profile.grid.Set(x, y, end); break;
                            case CubeyCell.Void: profile.grid.SetTriggered(x, y, true); break;
                        }
                    }
                profile.grid.MarkStartCell(startCell.x, startCell.y);
                profile.grid.MarkEndCell(endCell.x, endCell.y);
                profile.outputFolder = CubeyLevelsFolder;
                profile.prefabNameOverride = $"CubeyLevel {level}";
                profile.overwritePrefab = true;
                profile.GenerateLevelPrefab();
                Object.DestroyImmediate(profile);
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[CubeyLevelTools] Generated Cubey levels {from}-{to}");
        }

        /// <summary>
        /// Builds a level by playing it forward: random slides carve floor cells, the cell after each slide becomes a wall
        /// (or the slide ends on a stop point). Unused cells next to the path become walls, the rest void.
        /// The result is then checked with the solver.
        /// </summary>
        static CubeyCell[] TryBuild(System.Random rng, float t, float relax, out int cols, out int rows, out Vector2Int startCell, out Vector2Int endCell)
        {
            // Template levels are all 9x9 and the scene camera is framed for that; the board edge acts as a wall.
            cols = 9;
            rows = 9;
            int targetCells = (int)((40 + t * 22 + rng.Next(0, 4)) * relax); // ~40..65 counted tiles
            float stopChance = 0.08f + 0.12f * t;

            int W = cols, H = rows;
            var grid = new CubeyCell?[W * H]; // null = undecided
            int Idx(Vector2Int c) => c.y * W + c.x;

            var pos = new Vector2Int(rng.Next(0, W), rng.Next(0, H));
            startCell = pos;
            endCell = pos;
            grid[Idx(pos)] = CubeyCell.Start;
            int carved = 0;
            Vector2Int lastDir = Vector2Int.zero;

            for (int guard = 0; guard < 400 && carved < targetCells; guard++)
            {
                var dir = CubeyBoard.Directions[rng.Next(4)];
                if (dir == -lastDir || dir == lastDir) continue;
                int len = rng.Next(1, 7);
                var path = new List<Vector2Int>();
                var c = pos;
                for (int i = 0; i < len; i++)
                {
                    var n = c + dir;
                    if (!InBoundsWH(n, W, H) || grid[Idx(n)] != null) break;
                    c = n;
                    path.Add(c);
                }
                if (path.Count == 0) continue;

                var after = c + dir;
                bool endOnStop = rng.NextDouble() < stopChance;
                // the cell after the slide must be solid: the board edge, a new wall, or an already visited/solid cell
                bool afterSolid = !InBoundsWH(after, W, H) || grid[Idx(after)] != null;
                if (!endOnStop && !afterSolid && rng.NextDouble() < 0.15) continue; // prefer reusing existing stoppers

                foreach (var p in path) grid[Idx(p)] = CubeyCell.Floor;
                if (endOnStop) grid[Idx(c)] = CubeyCell.Stop;
                else if (!afterSolid) grid[Idx(after)] = CubeyCell.Wall;
                carved += path.Count;
                pos = c;
                lastDir = dir;
            }
            if (carved < targetCells * 0.85f) return null;

            // the final cell is the exit
            endCell = pos;
            grid[Idx(pos)] = CubeyCell.End;

            var cells = new CubeyCell[W * H];
            for (int i = 0; i < cells.Length; i++) cells[i] = grid[i] ?? CubeyCell.Wall;

            var board = new CubeyBoard(W, H, cells);
            int remaining = board.CountedTotal();
            var solution = board.Solve(startCell, new bool[W * H], remaining, 400000);
            if (solution == null) return null;
            // reject levels that are too easy: every first move must not be the only option for long stretches
            if (CountChoicePoints(board, startCell, solution, remaining) < 2 + (int)(t * 4)) return null;
            return cells;
        }

        static int CountChoicePoints(CubeyBoard board, Vector2Int start, List<Vector2Int> solution, int remaining)
        {
            var visited = new bool[board.CellCount];
            var pos = start;
            int choices = 0;
            foreach (var step in solution)
            {
                int options = 0;
                foreach (var d in CubeyBoard.Directions)
                {
                    var r = board.Slide(pos, d, visited, remaining).Result;
                    if (r == CubeyBoard.MoveResult.Moved || r == CubeyBoard.MoveResult.Win) options++;
                }
                if (options > 1) choices++;
                var m = board.Slide(pos, step, visited, remaining);
                foreach (var e in m.Entered) visited[board.Index(e)] = true;
                remaining -= m.Entered.Count;
                pos = m.Target;
            }
            return choices;
        }

        static bool InBoundsWH(Vector2Int c, int w, int h) => c.x >= 0 && c.x < w && c.y >= 0 && c.y < h;
    }
}
