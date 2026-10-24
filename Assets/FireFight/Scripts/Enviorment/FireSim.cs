using UnityEngine;

namespace FireFight.Environment
{
    /// <summary>
    /// The fire spread algorithm. Pure logic: give it a grid and a config,
    /// call <see cref="Tick"/>, and it advances the simulation one step.
    ///
    /// Deliberately NOT a MonoBehaviour so the spread rules can be reasoned
    /// about and unit-tested in isolation. The MonoBehaviour layer decides
    /// WHEN to tick; this class decides WHAT happens on a tick.
    /// </summary>
    public class FireSim
    {
        // 6-directional neighbours (no diagonals): +/- X, Y, Z.
        private static readonly Vector3Int[] Neighbours =
        {
            new Vector3Int( 1, 0, 0), new Vector3Int(-1, 0, 0),
            new Vector3Int( 0, 1, 0), new Vector3Int( 0,-1, 0),
            new Vector3Int( 0, 0, 1), new Vector3Int( 0, 0,-1),
        };

        private readonly FireGrid grid;
        private readonly FireConfig config;

        // Optional confined-space oxygen pools. Empty = no oxygen simulation,
        // so fire behaves exactly as before when no boxes are supplied.
        private readonly System.Collections.Generic.List<OxygenBox> oxygenBoxes;

        // Reused each tick to avoid per-tick allocations.
        private readonly float[] heatDelta;

        public FireSim(FireGrid grid, FireConfig config)
            : this(grid, config, null)
        {
        }

        public FireSim(FireGrid grid, FireConfig config,
            System.Collections.Generic.List<OxygenBox> oxygenBoxes)
        {
            this.grid = grid;
            this.config = config;
            this.oxygenBoxes = oxygenBoxes;
            this.heatDelta = new float[grid.Count];
        }

        /// <summary>Find the oxygen box containing a cell, or null if none (open air).</summary>
        private OxygenBox BoxAt(Vector3Int pos)
        {
            if (oxygenBoxes == null) return null;
            for (int i = 0; i < oxygenBoxes.Count; i++)
                if (oxygenBoxes[i].Contains(pos)) return oxygenBoxes[i];
            return null;
        }

        /// <summary>
        /// Advance the simulation one step.
        ///
        /// Two passes so every cell sees the SAME previous state this tick:
        ///   1. Burning cells consume fuel and accumulate heat into neighbours.
        ///   2. Apply heat, cool idle cells, and ignite cells that got hot enough.
        /// Without the split, fire would race across the whole grid in one tick.
        /// </summary>
        public void Tick()
        {
            FireCell[] cells = grid.Cells;
            System.Array.Clear(heatDelta, 0, heatDelta.Length);

            // Pass 1: burning cells burn fuel and heat their neighbours.
            for (int i = 0; i < cells.Length; i++)
            {
                FireCell cell = cells[i];
                if (cell.state != FireState.Burning) continue;

                // Confined-space oxygen: a burning cell inside a box consumes
                // its shared air. If the box has suffocated, the flame dies down
                // instead of consuming fuel (it can relight when air returns).
                OxygenBox box = BoxAt(cell.gridPosition);
                if (box != null)
                {
                    if (!box.HasBreathableAir)
                    {
                        // Starved of oxygen: stop burning, lose heat.
                        cell.state = FireState.Unburned;
                        cell.heat = 0f;
                        continue;
                    }
                    box.ConsumeForOneBurningCell();
                }

                cell.fuel -= config.burnRate;
                if (cell.fuel <= 0f)
                {
                    cell.fuel = 0f;
                    cell.state = FireState.Burned;
                    continue;
                }

                foreach (Vector3Int offset in Neighbours)
                {
                    Vector3Int np = cell.gridPosition + offset;
                    if (!grid.InBounds(np)) continue;

                    // Convection bias: hot air rises, so the cell above a flame
                    // gets more heat and the cell below gets less. Sideways = 1x.
                    float directionMul = 1f;
                    if (offset.y > 0) directionMul = config.upwardHeatMultiplier;
                    else if (offset.y < 0) directionMul = config.downwardHeatMultiplier;

                    heatDelta[grid.Index(np)] += config.heatPerTick * directionMul;
                }
            }

            // Pass 2: apply heat, cool idle cells, ignite eligible ones.
            for (int i = 0; i < cells.Length; i++)
            {
                FireCell cell = cells[i];

                if (heatDelta[i] > 0f)
                {
                    cell.heat += heatDelta[i] * Mathf.Max(0.05f, cell.flammability);
                }
                else if (cell.state == FireState.Unburned && cell.heat > 0f)
                {
                    cell.heat = Mathf.Max(0f, cell.heat - config.coolingRate);
                }

                // Cells above the configured fire ceiling never ignite, so
                // flames stay in a lower band even though smoke rises higher.
                bool belowFireCeiling = config.maxFireLayer < 0
                    || cell.gridPosition.y <= config.maxFireLayer;

                // Confined-space oxygen: a cell in a suffocated box cannot ignite.
                OxygenBox box = BoxAt(cell.gridPosition);
                bool hasAir = box == null || box.HasBreathableAir;

                if (cell.CanIgnite && belowFireCeiling && hasAir
                    && cell.heat >= config.ignitionThreshold)
                {
                    if (Random.value <= config.spreadChance)
                    {
                        cell.state = FireState.Burning;
                    }
                }
            }

            // End of tick: open boxes breathe fresh air back in. Do this last so
            // consumption and ignition this tick saw the pre-replenish oxygen.
            if (oxygenBoxes != null)
            {
                for (int i = 0; i < oxygenBoxes.Count; i++)
                    oxygenBoxes[i].Replenish();
            }
        }
    }
}
