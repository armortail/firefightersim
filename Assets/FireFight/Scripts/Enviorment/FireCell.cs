using UnityEngine;

namespace FireFight.Environment
{
    /// <summary>
    /// The state a single cell can be in during the fire simulation.
    /// </summary>
    public enum FireState
    {
        Unburned, // has fuel, not yet on fire
        Burning,  // currently on fire, spreading heat to neighbors
        Burned    // fuel used up, can no longer burn
    }

    /// <summary>
    /// Data for a single cell in the fire grid.
    ///
    /// This is a plain C# class (NOT a MonoBehaviour) on purpose: the grid may
    /// hold thousands of cells, and MonoBehaviours are expensive. Anything the
    /// player sees (flames, glow) is handled by a separate visualizer that just
    /// reads this data. The fire logic never touches your teammates' assets.
    /// </summary>
    public class FireCell
    {
        /// <summary>Position of this cell inside the grid (not world space).</summary>
        public Vector3Int gridPosition;

        public FireState state = FireState.Unburned;

        public float fuel = 1f;
        public float flammability = 0.5f;

        public float heat = 0f;

        /// <summary>
        /// Smoke density in this cell (0..1). Produced by burning/smouldering
        /// cells, rises to the cell above, spreads under ceilings, and slowly
        /// dissipates. Independent of fire: smoke travels through air, fire does
        /// not. Used for visibility and hazard gameplay.
        /// </summary>
        public float smoke = 0f;

        public FireCell(Vector3Int gridPosition)
        {
            this.gridPosition = gridPosition;
        }

        /// <summary>True if this cell can still catch fire.</summary>
        public bool CanIgnite => state == FireState.Unburned && fuel > 0f;
    }
}
