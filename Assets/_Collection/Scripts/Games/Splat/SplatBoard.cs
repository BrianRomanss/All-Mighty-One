using System.Collections.Generic;
using AllMightyOne.Cubey;
using UnityEngine;

namespace AllMightyOne.Splat
{
    /// <summary>
    /// Logical model of a Splat level (CharacterControl_Cubey with Splat tiles), mirroring the physics rules:
    /// - the player slides until the next cell is a wall (or the board edge: the scene has outer walls);
    /// - a stop point always halts the slide on top of it;
    /// - painted tiles stay walkable (unlike Cubey, where visited tiles rise), so moves never depend on what is painted;
    /// - the level is won when every floor tile and stop point is painted.
    /// Cells come from CubeyLevelReader (same GridLevelData levels); grid coordinates: x = column, y = row 0 at the top.
    /// </summary>
    public class SplatBoard
    {
        public readonly int Columns, Rows;
        readonly CubeyCell[] _cells;

        public SplatBoard(int columns, int rows, CubeyCell[] cells)
        {
            Columns = columns; Rows = rows; _cells = cells;
        }

        public static SplatBoard From(CubeyBoard board)
        {
            var cells = new CubeyCell[board.CellCount];
            for (int y = 0; y < board.Rows; y++)
                for (int x = 0; x < board.Columns; x++)
                    cells[y * board.Columns + x] = board[new Vector2Int(x, y)];
            return new SplatBoard(board.Columns, board.Rows, cells);
        }

        public bool InBounds(Vector2Int c) => c.x >= 0 && c.x < Columns && c.y >= 0 && c.y < Rows;
        public int Index(Vector2Int c) => c.y * Columns + c.x;
        public CubeyCell this[Vector2Int c] => InBounds(c) ? _cells[Index(c)] : CubeyCell.Wall;
        public int CellCount => _cells.Length;

        public static bool IsCounted(CubeyCell c) => c == CubeyCell.Floor || c == CubeyCell.Stop;

        public int CountedTotal()
        {
            int n = 0;
            foreach (var c in _cells) if (IsCounted(c)) n++;
            return n;
        }

        /// <summary> Cells passed by one swipe from pos (empty when the swipe does not move). The last one is the target. </summary>
        public List<Vector2Int> Slide(Vector2Int pos, Vector2Int step)
        {
            var path = new List<Vector2Int>();
            var c = pos;
            while (true)
            {
                var n = c + step;
                if (this[n] == CubeyCell.Wall) break;
                c = n;
                path.Add(c);
                if (this[c] == CubeyCell.Stop) break;
            }
            return path;
        }

        /// <summary> Positions the player can reach from pos with any number of swipes (pos included). </summary>
        public HashSet<Vector2Int> Reachable(Vector2Int pos)
        {
            var seen = new HashSet<Vector2Int> { pos };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(pos);
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                foreach (var d in CubeyBoard.Directions)
                {
                    var path = Slide(p, d);
                    if (path.Count > 0 && seen.Add(path[^1])) queue.Enqueue(path[^1]);
                }
            }
            return seen;
        }

        /// <summary>
        /// True when every position reachable from start can get back to start. Then no sequence of swipes can trap the
        /// player, so the level can always still be finished if it can be finished at all.
        /// </summary>
        public bool NoTraps(Vector2Int start)
        {
            foreach (var p in Reachable(start))
                if (!Reachable(p).Contains(start)) return false;
            return true;
        }

        /// <summary>
        /// Shortest swipe sequence from pos that ends with a swipe painting at least one unpainted cell (null when no swipe
        /// sequence can paint anything new). painted is not modified.
        /// </summary>
        public List<Vector2Int> NextPaintingMoves(Vector2Int pos, bool[] painted)
        {
            var from = new Dictionary<Vector2Int, (Vector2Int prev, Vector2Int dir)>();
            var queue = new Queue<Vector2Int>();
            var seen = new HashSet<Vector2Int> { pos };
            queue.Enqueue(pos);
            while (queue.Count > 0)
            {
                var p = queue.Dequeue();
                foreach (var d in CubeyBoard.Directions)
                {
                    var path = Slide(p, d);
                    if (path.Count == 0) continue;
                    bool paints = false;
                    foreach (var c in path) if (IsCounted(this[c]) && !painted[Index(c)]) { paints = true; break; }
                    if (paints)
                    {
                        var moves = new List<Vector2Int> { d };
                        for (var q = p; q != pos; q = from[q].prev) moves.Insert(0, from[q].dir);
                        return moves;
                    }
                    var t = path[^1];
                    if (seen.Add(t)) { from[t] = (p, d); queue.Enqueue(t); }
                }
            }
            return null;
        }

        /// <summary> Greedy solution from the current state (null when some tile can no longer be painted). </summary>
        public List<Vector2Int> Solve(Vector2Int pos, bool[] painted)
        {
            var paint = (bool[])painted.Clone();
            int left = 0;
            for (int i = 0; i < _cells.Length; i++) if (IsCounted(_cells[i]) && !paint[i]) left++;
            var solution = new List<Vector2Int>();
            while (left > 0)
            {
                var moves = NextPaintingMoves(pos, paint);
                if (moves == null) return null;
                foreach (var d in moves)
                {
                    var path = Slide(pos, d);
                    foreach (var c in path)
                        if (IsCounted(this[c]) && !paint[Index(c)]) { paint[Index(c)] = true; left--; }
                    pos = path[^1];
                    solution.Add(d);
                }
            }
            return solution;
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            for (int y = 0; y < Rows; y++)
            {
                for (int x = 0; x < Columns; x++)
                    sb.Append(_cells[y * Columns + x] switch
                    {
                        CubeyCell.Floor => '.', CubeyCell.Wall => '#', CubeyCell.Stop => 'o', CubeyCell.Start => 'S', CubeyCell.End => 'E', _ => ' '
                    });
                sb.AppendLine();
            }
            return sb.ToString();
        }
    }
}
