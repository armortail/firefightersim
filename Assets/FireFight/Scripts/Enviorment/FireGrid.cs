using UnityEngine;

namespace FireFight.Environment
{
    /// <summary>
    /// Owns the 3D array of <see cref="FireCell"/>s and all coordinate math.
    /// Pure storage + addressing: it knows nothing about how fire spreads.
    ///
    /// Not a MonoBehaviour so it can be created, inspected, and unit-tested
    /// without Unity's lifecycle. The MonoBehaviour layer owns an instance.
    /// </summary>
    public class FireGrid
    {
        public Vector3Int Size { get; }
        public float CellSize { get; }

        /// <summary>World-space position of the grid's (0,0,0) corner.</summary>
        public Vector3 Origin { get; }

        /// <summary>Total number of cells.</summary>
        public int Count => cells.Length;

        private readonly FireCell[] cells;

        public FireGrid(Vector3Int size, float cellSize, Vector3 origin)
        {
            Size = size;
            CellSize = cellSize;
            Origin = origin;

            cells = new FireCell[size.x * size.y * size.z];
            for (int x = 0; x < size.x; x++)
                for (int y = 0; y < size.y; y++)
                    for (int z = 0; z < size.z; z++)
                        cells[Index(x, y, z)] = new FireCell(new Vector3Int(x, y, z));
        }

        /// <summary>Direct access to the backing array (read-only usage expected).</summary>
        public FireCell[] Cells => cells;

        public FireCell GetCell(Vector3Int p)
        {
            return InBounds(p) ? cells[Index(p.x, p.y, p.z)] : null;
        }

        public bool InBounds(Vector3Int p)
        {
            return p.x >= 0 && p.x < Size.x
                && p.y >= 0 && p.y < Size.y
                && p.z >= 0 && p.z < Size.z;
        }

        public int Index(int x, int y, int z)
        {
            return x + Size.x * (y + Size.y * z);
        }

        public int Index(Vector3Int p) => Index(p.x, p.y, p.z);

        public Vector3 GridToWorld(Vector3Int gridPos)
        {
            return Origin + (Vector3)gridPos * CellSize + Vector3.one * (CellSize * 0.5f);
        }

        public Vector3Int WorldToGrid(Vector3 worldPos)
        {
            Vector3 local = worldPos - Origin;
            return new Vector3Int(
                Mathf.FloorToInt(local.x / CellSize),
                Mathf.FloorToInt(local.y / CellSize),
                Mathf.FloorToInt(local.z / CellSize));
        }
    }
}
