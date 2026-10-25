using System.Collections.Generic;
using UnityEngine;

namespace FireFight.Environment
{
    /// <summary>
    /// Auto-detects confined-space oxygen rooms by FLOOD-FILLING connected air cells.
    ///
    /// Pure logic (not a MonoBehaviour) so it can be unit-tested: give it a grid and
    /// it returns one cell-set per connected pocket of air. Two cells are connected if
    /// they are 6-neighbours (no diagonals) and NEITHER is solid. Solid cells
    /// (<see cref="FireCell.isSolid"/> - walls, closed doors, large props) are barriers,
    /// so a sealed room becomes its own region. Knock a hole in a wall or open a door
    /// (flip those cells non-solid) and re-running detection merges the two pockets.
    ///
    /// The grid edge is also a barrier: the open outdoors beyond the grid is simply
    /// "not a room". Every connected air pocket - including the big outdoor one - is a
    /// region; the FireManager decides which to treat as confined (e.g. by a minimum
    /// size, so the vast outdoor pocket isn't modelled as a suffocating room).
    /// </summary>
    public static class OxygenAutoDetector
    {
        private static readonly Vector3Int[] Neighbours =
        {
            new Vector3Int( 1, 0, 0), new Vector3Int(-1, 0, 0),
            new Vector3Int( 0, 1, 0), new Vector3Int( 0,-1, 0),
            new Vector3Int( 0, 0, 1), new Vector3Int( 0, 0,-1),
        };

        /// <summary>
        /// Flood-fill the grid into connected air regions. Each returned set is one room.
        /// </summary>
        public static List<HashSet<Vector3Int>> DetectRooms(FireGrid grid)
        {
            var regions = new List<HashSet<Vector3Int>>();
            if (grid == null) return regions;

            FireCell[] cells = grid.Cells;
            var visited = new bool[cells.Length];
            var queue = new Queue<Vector3Int>();

            for (int i = 0; i < cells.Length; i++)
            {
                if (visited[i]) continue;
                visited[i] = true;

                FireCell start = cells[i];
                if (start.isSolid) continue; // walls are not air; skip

                // BFS from this air cell across all connected non-solid neighbours.
                var region = new HashSet<Vector3Int>();
                queue.Clear();
                queue.Enqueue(start.gridPosition);
                region.Add(start.gridPosition);

                while (queue.Count > 0)
                {
                    Vector3Int p = queue.Dequeue();
                    for (int n = 0; n < Neighbours.Length; n++)
                    {
                        Vector3Int np = p + Neighbours[n];
                        if (!grid.InBounds(np)) continue;

                        int ni = grid.Index(np);
                        if (visited[ni]) continue;
                        visited[ni] = true;

                        if (cells[ni].isSolid) continue; // barrier: do not cross

                        region.Add(np);
                        queue.Enqueue(np);
                    }
                }

                regions.Add(region);
            }

            return regions;
        }
    }
}
