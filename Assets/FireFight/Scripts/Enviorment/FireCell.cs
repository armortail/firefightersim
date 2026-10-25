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

        /// <summary>
        /// Wetness (0..1) left behind by water/extinguishing. While wet, the cell
        /// resists igniting (its effective flammability is reduced) and it cannot
        /// stay burning. Evaporates slowly each tick so the cell can eventually
        /// dry out and burn again. This stops fire snapping back the instant you
        /// stop spraying.
        /// </summary>
        public float wetness = 0f;

        /// <summary>
        /// Smother coating (0..1) left behind by foam, CO2, a fire blanket, sand,
        /// or dry powder. Models attacking the OXYGEN side of the fire tetrahedron:
        /// the coating seals the fuel off from air. While coated above the config
        /// threshold, the cell cannot stay burning and cannot ignite, regardless of
        /// heat. Unlike wetness it does not cool; it blocks air. It decays slowly
        /// (foam collapses, CO2 disperses) so protection is temporary unless
        /// reapplied. Kept separate from wetness so a cell can be both wet AND
        /// foamed, and so tools can model each agent independently.
        /// </summary>
        public float coating = 0f;

        /// <summary>
        /// How many ticks this cell has been smouldering since it became
        /// <see cref="FireState.Burned"/>. The smoke sim only emits smoulder smoke
        /// while this is under a configured limit, so a burned-out cell eventually
        /// stops smoking and the scene can clear. Reset is unnecessary: a cell only
        /// reaches Burned once (fuel is spent) unless explicitly re-fuelled.
        /// </summary>
        public int smoulderedTicks = 0;

        /// <summary>
        /// True if this cell is blocked by a solid obstacle (a wall, a closed door,
        /// or a large prop like a fuel tanker / container). Solid cells are NOT air:
        /// the oxygen room auto-detector treats them as barriers that separate rooms,
        /// and fire cannot exist in them. Set by the FireManager from physics overlap
        /// against an obstacle LayerMask, and toggled by doors/windows at runtime.
        /// </summary>
        public bool isSolid = false;

        public FireCell(Vector3Int gridPosition)
        {
            this.gridPosition = gridPosition;
        }

        /// <summary>True if this cell can still catch fire.</summary>
        public bool CanIgnite => state == FireState.Unburned && fuel > 0f;

        /// <summary>
        /// Effective flammability after wetness AND coating suppression. A fully
        /// wet OR fully coated cell has ~zero flammability; a dry, uncoated cell
        /// uses its base value. The two suppressors stack multiplicatively so
        /// applying both (e.g. water then foam) is at least as effective as either.
        /// </summary>
        public float EffectiveFlammability =>
            flammability
            * (1f - Mathf.Clamp01(wetness))
            * (1f - Mathf.Clamp01(coating));
    }
}
