using UnityEngine;

namespace FireFight.Environment
{
    /// <summary>
    /// The smoke simulation. Pure logic (not a MonoBehaviour) so it can be unit
    /// tested in isolation, mirroring <see cref="FireSim"/>.
    ///
    /// Behaviour each tick:
    ///   1. Production  - Burning cells emit smoke; Burned cells smoulder a little.
    ///   2. Rise        - smoke moves preferentially into the cell above (Y+1).
    ///   3. Spread      - smoke that cannot rise fans out sideways (fills a room
    ///                    top-down, like real smoke banking under a ceiling).
    ///   4. Dissipation - every cell loses a little smoke so it clears over time.
    ///
    /// Smoke travels through AIR (unlike fire, which needs fuel). The only thing
    /// that blocks smoke is the edge of the grid; solid obstacles can be added
    /// later by treating certain cells as impassable.
    /// </summary>
    public class SmokeSim
    {
        // Horizontal neighbours only (smoke spreads sideways under a ceiling).
        private static readonly Vector3Int[] Horizontal =
        {
            new Vector3Int( 1, 0, 0), new Vector3Int(-1, 0, 0),
            new Vector3Int( 0, 0, 1), new Vector3Int( 0, 0,-1),
        };

        private static readonly Vector3Int Up = new Vector3Int(0, 1, 0);

        private readonly FireGrid grid;
        private readonly SmokeConfig config;

        // Double-buffered smoke so all moves read the SAME previous state.
        private readonly float[] next;

        public SmokeSim(FireGrid grid, SmokeConfig config)
        {
            this.grid = grid;
            this.config = config;
            this.next = new float[grid.Count];
        }

        public void Tick()
        {
            FireCell[] cells = grid.Cells;

            // ---- 1. Production (writes directly into the live smoke field) ----
            for (int i = 0; i < cells.Length; i++)
            {
                FireCell cell = cells[i];
                if (cell.state == FireState.Burning)
                {
                    cell.smoke = Mathf.Min(1f, cell.smoke + config.smokePerBurningTick);
                }
                else if (cell.state == FireState.Burned)
                {
                    // Burned cells smoulder only for a limited time, then go cold so
                    // the scene can clear. -1 means smoulder forever (legacy behaviour).
                    bool stillSmouldering = config.smoulderDurationTicks < 0
                        || cell.smoulderedTicks < config.smoulderDurationTicks;
                    if (stillSmouldering)
                    {
                        cell.smoke = Mathf.Min(1f, cell.smoke + config.smokePerSmolderingTick);
                        cell.smoulderedTicks++;
                    }
                }
            }

            // ---- 2 & 3. Movement into the "next" buffer ----
            // Start next = current, then move portions between cells.
            for (int i = 0; i < cells.Length; i++) next[i] = cells[i].smoke;

            for (int i = 0; i < cells.Length; i++)
            {
                FireCell cell = cells[i];
                float amount = cell.smoke;
                if (amount <= config.minSmoke) continue;

                Vector3Int abovePos = cell.gridPosition + Up;

                // Smoke can rise if there is a cell above AND we are below the
                // configured ceiling (maxSmokeLayer). At or above that layer,
                // smoke banks sideways as if it hit a physical ceiling.
                bool belowCeiling = config.maxSmokeLayer < 0
                    || cell.gridPosition.y < config.maxSmokeLayer;
                bool canRise = belowCeiling && grid.InBounds(abovePos);

                if (canRise)
                {
                    // Rise: move a share straight up.
                    float rising = amount * config.riseRate;
                    next[i] -= rising;
                    next[grid.Index(abovePos)] += rising;
                }
                else
                {
                    // Hit the ceiling (top of grid): fan out sideways instead.
                    foreach (Vector3Int offset in Horizontal)
                    {
                        Vector3Int np = cell.gridPosition + offset;
                        if (!grid.InBounds(np)) continue;
                        float moving = amount * config.spreadRate;
                        next[i] -= moving;
                        next[grid.Index(np)] += moving;
                    }
                }
            }

            // ---- 4. Commit buffer + dissipation + clamp ----
            for (int i = 0; i < cells.Length; i++)
            {
                float s = next[i] - config.dissipation;
                if (s < config.minSmoke) s = 0f;
                else if (s > 1f) s = 1f;
                cells[i].smoke = s;
            }
        }
    }
}
