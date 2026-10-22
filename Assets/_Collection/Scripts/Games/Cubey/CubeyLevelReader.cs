using Solo.MOST_IN_ONE;
using UnityEngine;

namespace AllMightyOne.Cubey
{
    /// <summary> Builds a CubeyBoard from a generated level (GridLevelData) and converts cells to/from world space. </summary>
    public static class CubeyLevelReader
    {
        /// <summary>
        /// Reads the board from the level's spawned objects ("Cells" = prefab cells, "EmptyCells" = floor tiles).
        /// Older level prefabs have no CellStates/CellObjects snapshot, so object positions are the source of truth.
        /// </summary>
        public static CubeyBoard Read(GridLevelData level)
        {
            int cols = level.Columns, rows = level.Rows;
            var cells = new CubeyCell[cols * rows]; // default Void
            var board = new CubeyBoard(cols, rows, cells);

            var floorRoot = level.transform.Find("EmptyCells");
            if (floorRoot)
                foreach (Transform t in floorRoot) Put(t, CubeyCell.Floor);

            var cellsRoot = level.transform.Find("Cells");
            if (cellsRoot)
                foreach (Transform t in cellsRoot) Put(t, Classify(t.name));

            return board;

            void Put(Transform t, CubeyCell type)
            {
                var c = WorldToCell(level, t.position);
                if (board.InBounds(c)) cells[board.Index(c)] = type;
            }
        }

        public static Vector2Int StartCell(GridLevelData level) =>
            level.StartCellObject ? WorldToCell(level, level.StartCellObject.transform.position) : level.StartCell;

        // Instances are named "<prefab name> (x,y)" by GridLevelProfile
        public static CubeyCell Classify(string objectName)
        {
            if (objectName.StartsWith("StartBlock")) return CubeyCell.Start;
            if (objectName.StartsWith("EndBlock")) return CubeyCell.End;
            if (objectName.StartsWith("StopPoint")) return CubeyCell.Stop;
            if (objectName.StartsWith("Block")) return CubeyCell.Floor;
            return CubeyCell.Wall;
        }

        public static Vector3 CellToWorld(GridLevelData level, Vector2Int cell)
        {
            float s = level.CellWorldSize;
            int yFlipped = level.Rows - 1 - cell.y;
            Vector3 local = level.CenterPoint
                + Vector3.right * s * (cell.x - (level.Columns - 1) * 0.5f)
                + Vector3.forward * s * (yFlipped - (level.Rows - 1) * 0.5f);
            return level.transform.TransformPoint(local);
        }

        public static Vector2Int WorldToCell(GridLevelData level, Vector3 world)
        {
            float s = Mathf.Max(0.0001f, level.CellWorldSize);
            Vector3 local = level.transform.InverseTransformPoint(world) - level.CenterPoint;
            int x = Mathf.RoundToInt(local.x / s + (level.Columns - 1) * 0.5f);
            int yFlipped = Mathf.RoundToInt(local.z / s + (level.Rows - 1) * 0.5f);
            return new Vector2Int(x, level.Rows - 1 - yFlipped);
        }
    }
}
