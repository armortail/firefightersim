using UnityEngine;

namespace FireFight.Environment
{
    /// <summary>
    /// A confined region of the grid with a single SHARED oxygen pool
    /// (Option A). Models a sealed room: fire inside consumes oxygen, and when
    /// the air runs out the fire suffocates. "Opening" the box (a door breaks,
    /// a window is vented) replenishes oxygen and the fire flares back up — the
    /// classic backdraft / flashover beat for firefighter training.
    ///
    /// Pure data + rules, no MonoBehaviour, so it is unit-testable. The grid
    /// region is an inclusive min..max box in grid coordinates.
    /// </summary>
    public class OxygenBox
    {
        public Vector3Int Min { get; }
        public Vector3Int Max { get; }

        /// <summary>Shared oxygen for the whole box (0..1). 1 = fresh air.</summary>
        public float Oxygen { get; private set; } = 1f;

        /// <summary>
        /// When open, oxygen replenishes toward 1 each tick (door/window vented).
        /// When sealed, oxygen only depletes as fire consumes it.
        /// </summary>
        public bool IsOpen { get; private set; }

        private readonly OxygenConfig config;

        // When non-null, the box is an ARBITRARY shape (flood-filled room) and
        // membership is a set lookup. When null, the box is the rectangular
        // Min..Max region (the original behaviour the tests rely on).
        private readonly System.Collections.Generic.HashSet<Vector3Int> cells;

        public OxygenBox(Vector3Int min, Vector3Int max, OxygenConfig config, bool startOpen = false)
        {
            // Normalise so Min <= Max on every axis regardless of argument order.
            Min = Vector3Int.Min(min, max);
            Max = Vector3Int.Max(min, max);
            this.config = config;
            IsOpen = startOpen;
            cells = null; // rectangular box
        }

        /// <summary>
        /// Build a box from an explicit set of grid cells (an auto-detected room of any
        /// shape). Min/Max are the set's bounding box for convenience, but membership is
        /// the exact set - so an L-shaped room doesn't leak oxygen across its bounding box.
        /// </summary>
        public OxygenBox(System.Collections.Generic.HashSet<Vector3Int> cells,
            OxygenConfig config, bool startOpen = false)
        {
            this.cells = cells;
            this.config = config;
            IsOpen = startOpen;

            // Compute the bounding box for Min/Max (and handle the empty case).
            if (cells != null && cells.Count > 0)
            {
                var min = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
                var max = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);
                foreach (var c in cells)
                {
                    min = Vector3Int.Min(min, c);
                    max = Vector3Int.Max(max, c);
                }
                Min = min;
                Max = max;
            }
            else
            {
                Min = Vector3Int.zero;
                Max = Vector3Int.zero;
            }
        }

        /// <summary>Number of cells in this box (room volume). For rectangular boxes, the box volume.</summary>
        public int CellCount =>
            cells != null
                ? cells.Count
                : (Max.x - Min.x + 1) * (Max.y - Min.y + 1) * (Max.z - Min.z + 1);

        /// <summary>Open or seal the box. Opening lets fresh air back in.</summary>
        public void SetOpen(bool open) => IsOpen = open;

        /// <summary>True if a grid position lies inside this box.</summary>
        public bool Contains(Vector3Int p)
        {
            if (cells != null) return cells.Contains(p);

            // Rectangular box (original behaviour).
            return p.x >= Min.x && p.x <= Max.x
                && p.y >= Min.y && p.y <= Max.y
                && p.z >= Min.z && p.z <= Max.z;
        }

        /// <summary>
        /// True if there is enough oxygen for fire to keep burning / ignite here.
        /// </summary>
        public bool HasBreathableAir => Oxygen >= config.sustainThreshold;

        /// <summary>Remove oxygen as a burning cell consumes it (uses this box's config).</summary>
        public void ConsumeForOneBurningCell()
        {
            Oxygen = Mathf.Max(0f, Oxygen - config.consumptionPerBurningCell);
        }

        /// <summary>
        /// End-of-tick update: an open box breathes fresh air back in. A sealed
        /// box does nothing here (it only loses oxygen via Consume).
        /// </summary>
        public void Replenish()
        {
            if (IsOpen)
                Oxygen = Mathf.Min(1f, Oxygen + config.replenishRate);
        }
    }
}
