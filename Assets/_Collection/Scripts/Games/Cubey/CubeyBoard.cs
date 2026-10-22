using System.Collections.Generic;
using UnityEngine;

namespace AllMightyOne.Cubey
{
    public enum CubeyCell : byte { Void, Floor, Wall, Stop, Start, End }

    /// <summary>
    /// Logical model of a Cubey level, mirroring CharacterControl_Cubey's physics rules:
    /// - the player slides until the next cell is solid (wall, start, or an already visited floor/stop cell);
    /// - an unvisited stop point halts the slide on top of it;
    /// - void cells are slid over; the board edge acts as a wall (the scene has outer walls);
    /// - passing over the End cell ends the game (win only if it is the last counted cell).
    /// Grid coordinates match GridLevelData (x = column, y = row, row 0 at the top).
    /// </summary>
    public class CubeyBoard
    {
        public static readonly Vector2Int[] Directions = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

        public readonly int Columns, Rows;
        readonly CubeyCell[] _cells;

        public CubeyBoard(int columns, int rows, CubeyCell[] cells)
        {
            Columns = columns; Rows = rows; _cells = cells;
        }

        public bool InBounds(Vector2Int c) => c.x >= 0 && c.x < Columns && c.y >= 0 && c.y < Rows;
        public int Index(Vector2Int c) => c.y * Columns + c.x;
        public CubeyCell this[Vector2Int c] => InBounds(c) ? _cells[Index(c)] : CubeyCell.Void;
        public int CellCount => _cells.Length;

        public static bool IsCounted(CubeyCell c) => c == CubeyCell.Floor || c == CubeyCell.Stop || c == CubeyCell.End;

        public int CountedTotal()
        {
            int n = 0;
            foreach (var c in _cells) if (IsCounted(c)) n++;
            return n;
        }

        // Screen/swipe direction (x right, y up) -> grid step (row index grows downwards)
        public static Vector2Int SwipeToGrid(Vector2 swipe) => new(Mathf.RoundToInt(swipe.x), -Mathf.RoundToInt(swipe.y));
        public static Vector2 GridToSwipe(Vector2Int step) => new(step.x, -step.y);

        public enum MoveResult { None, Moved, Win, Lose }

        public struct Move
        {
            public MoveResult Result;
            public Vector2Int Target;
            public List<Vector2Int> Entered; // newly counted cells, in order
        }

        bool Solid(Vector2Int c, bool[] visited, Vector2Int current)
        {
            if (c == current) return false;
            var t = this[c];
            if (t == CubeyCell.Wall || t == CubeyCell.Start) return true;
            return (t == CubeyCell.Floor || t == CubeyCell.Stop) && visited[Index(c)];
        }

        /// <summary> Simulates one swipe. visited is not modified. </summary>
        public Move Slide(Vector2Int pos, Vector2Int step, bool[] visited, int remaining)
        {
            var move = new Move { Result = MoveResult.None, Target = pos, Entered = new List<Vector2Int>() };
            var c = pos;
            var path = new List<Vector2Int>();
            while (true)
            {
                var n = c + step;
                if (!InBounds(n) || Solid(n, visited, pos)) break; // the scene surrounds the board with "Wall Out" walls
                var t = this[n];
                c = n;
                path.Add(n);
                if (t == CubeyCell.Stop && !visited[Index(n)]) break;
            }
            if (c == pos) return move;

            // walk the path: count cells, End ends the game immediately
            int left = remaining;
            foreach (var p in path)
            {
                var t = this[p];
                if (IsCounted(t) && !visited[Index(p)] && !move.Entered.Contains(p))
                {
                    move.Entered.Add(p);
                    left--;
                }
                if (t == CubeyCell.End)
                {
                    move.Target = p;
                    move.Result = left <= 0 ? MoveResult.Win : MoveResult.Lose;
                    return move;
                }
            }
            move.Target = c;
            move.Result = left <= 0 ? MoveResult.Win : MoveResult.Moved;
            return move;
        }

        public int LastSearchNodes { get; private set; }
        /// <summary> True when the last Solve() explored every state and proved there is no solution. </summary>
        public bool LastSearchExhausted { get; private set; }

        /// <summary> Depth-first search for a winning sequence of swipes. Returns null if none found within the node budget. </summary>
        public List<Vector2Int> Solve(Vector2Int pos, bool[] visited, int remaining, int nodeBudget = 200000)
        {
            var dead = new HashSet<string>();
            var solution = new List<Vector2Int>();
            int nodes = 0;
            var vis = (bool[])visited.Clone();
            bool found = Dfs(pos, vis, remaining);
            LastSearchNodes = nodes;
            LastSearchExhausted = !found && nodes <= nodeBudget;
            return found ? solution : null;

            bool Dfs(Vector2Int p, bool[] v, int rem)
            {
                if (++nodes > nodeBudget) return false;
                string key = Key(p, v);
                if (dead.Contains(key)) return false;
                foreach (var d in Directions)
                {
                    var m = Slide(p, d, v, rem);
                    if (m.Result == MoveResult.None || m.Result == MoveResult.Lose) continue;
                    solution.Add(d);
                    if (m.Result == MoveResult.Win) return true;
                    foreach (var e in m.Entered) v[Index(e)] = true;
                    if (Dfs(m.Target, v, rem - m.Entered.Count)) return true;
                    foreach (var e in m.Entered) v[Index(e)] = false;
                    solution.RemoveAt(solution.Count - 1);
                    if (nodes > nodeBudget) return false;
                }
                dead.Add(key);
                return false;
            }
        }

        public bool HasAnyMove(Vector2Int pos, bool[] visited, int remaining)
        {
            foreach (var d in Directions)
                if (Slide(pos, d, visited, remaining).Result != MoveResult.None) return true;
            return false;
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Columns; x++)
                    sb.Append(_cells[y * Columns + x] switch
                    {
                        CubeyCell.Floor => '.', CubeyCell.Wall => '#', CubeyCell.Stop => 'o',
                        CubeyCell.Start => 'S', CubeyCell.End => 'E', _ => ' '
                    });
                sb.AppendLine();
            }
            return sb.ToString();
        }

        string Key(Vector2Int p, bool[] v)
        {
            var chars = new char[v.Length + 1];
            chars[0] = (char)(p.y * Columns + p.x + 32);
            for (int i = 0; i < v.Length; i++) chars[i + 1] = v[i] ? '1' : '0';
            return new string(chars);
        }
    }
}
