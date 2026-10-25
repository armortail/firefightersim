using UnityEngine;

namespace FireFight.Managers
{
    /// <summary>
    /// Deterministically starts the demo fire one frame after the scene loads.
    ///
    /// Why one frame late: <see cref="Flammable.Start"/> is the only thing that paints
    /// fuel into the grid (via <see cref="FireManager.AddFuelInBounds"/>), and an igniter
    /// running in its own <c>Start</c> races those calls - if it wins, the cell it lights
    /// has no fuel and <see cref="FireManager.IgniteCell"/> silently no-ops. Deferring the
    /// ignition to the first <c>Update</c> guarantees every <c>Flammable.Start</c> has
    /// already run, so the seed's cells are fuelled before we light them.
    ///
    /// Why the manager (not <c>seed.Ignite()</c>): <see cref="Flammable.Ignite"/> is guarded
    /// by the seed's private <c>registered</c> flag and does nothing if the seed failed to
    /// register (no manager found, or no collider/renderer). This igniter instead recomputes
    /// the seed's world bounds itself and calls <see cref="FireManager.IgniteBounds"/> directly,
    /// then verifies the ignition took. On any failure it falls back to the grid-centre cell.
    /// </summary>
    public class SeedIgniter : MonoBehaviour
    {
        [Tooltip("The flammable to ignite as the demo fire source. Chosen by the builder as the " +
                 "flammable nearest the central structure. If null, the grid-centre fallback is used.")]
        public Flammable seed;

        [Tooltip("The fire manager that owns the grid. Wired by the builder; falls back to a scene lookup.")]
        public FireManager fireManager;

        private bool done;

        private void Update()
        {
            if (done) return;
            done = true;

            if (fireManager == null)
                fireManager = FindFirstObjectByType<FireManager>();

            if (fireManager == null)
            {
                Debug.LogWarning("[FireFight] SeedIgniter found no FireManager; no demo fire will start.", this);
                return;
            }

            if (seed != null && TryComputeSeedBounds(seed, out Bounds seedBounds))
            {
                fireManager.IgniteBounds(seedBounds);
                if (fireManager.IsAnyBurningInBounds(seedBounds))
                    return; // Demo fire started on the seed object. Done.

                Debug.LogWarning(
                    $"[FireFight] SeedIgniter: igniting seed '{seed.name}' took no effect " +
                    "(no fuel in its cells, or out of grid). Falling back to grid centre.", this);
            }
            else
            {
                Debug.LogWarning("[FireFight] SeedIgniter: no usable seed. Falling back to grid centre.", this);
            }

            // Fallback: ignite the centre cell on the floor row.
            var centre = new Vector3Int(fireManager.gridSize.x / 2, 0, fireManager.gridSize.z / 2);
            if (!fireManager.IgniteCell(centre))
            {
                Debug.LogWarning(
                    "[FireFight] SeedIgniter: grid-centre cell had no fuel either; the generated scene " +
                    "seeded no ignitable fuel near centre.", this);
            }
        }

        /// <summary>
        /// Recompute the seed's world bounds the same way <c>Flammable</c> does
        /// (collider preferred, else renderer). Does NOT depend on the seed's private
        /// <c>worldBounds</c> or its <c>registered</c> flag.
        /// </summary>
        private static bool TryComputeSeedBounds(Flammable s, out Bounds bounds)
        {
            var col = s.GetComponentInChildren<Collider>();
            if (col != null) { bounds = col.bounds; return true; }

            var rend = s.GetComponentInChildren<Renderer>();
            if (rend != null) { bounds = rend.bounds; return true; }

            bounds = default;
            return false;
        }
    }
}
