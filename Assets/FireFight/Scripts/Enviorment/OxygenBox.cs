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

        public OxygenBox(Vector3Int min, Vector3Int max, OxygenConfig config, bool startOpen = false)
        {
            // Normalise so Min <= Max on every axis regardless of argument order.
            Min = Vector3Int.Min(min, max);
            Max = Vector3Int.Max(min, max);
            this.config = config;
            IsOpen = startOpen;
        }

        /// <summary>Open or seal the box. Opening lets fresh air back in.</summary>
        public void SetOpen(bool open) => IsOpen = open;

        /// <summary>True if a grid position lies inside this box (inclusive).</summary>
        public bool Contains(Vector3Int p)
        {
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
