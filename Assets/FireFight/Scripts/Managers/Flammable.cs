using UnityEngine;
using UnityEngine.Events;
using FireFight.Environment;

namespace FireFight.Managers
{
    /// <summary>
    /// Bridges a physical scene object to the invisible fire grid.
    ///
    /// Attach to anything that should be able to burn (a barrel, crate, oil tank).
    /// On start it paints fuel into the grid cells its collider occupies, so fire
    /// can only exist where real objects are. Each tick it asks the FireManager
    /// whether any of those cells are burning and mirrors that into <see cref="inflames"/>.
    ///
    /// This is the ONLY place that talks to both the GameObject and the fire grid,
    /// so the simulation stays decoupled from scene art. Designers hook visuals
    /// (flame particles, charred material, audio) through the UnityEvents without
    /// touching simulation code.
    /// </summary>
    public class Flammable : MonoBehaviour
    {
        [Header("Fire Manager")]
        [Tooltip("The fire system this object registers with. If left empty, it finds one in the scene.")]
        public FireManager fireManager;

        [Header("Fuel Properties")]
        [Tooltip("Starting fuel for the cells this object occupies (0..1). Higher = burns longer.")]
        [Range(0f, 1f)]
        public float fuel = 1f;

        [Tooltip("How easily this object catches fire (0..1). Oil tank high, metal container low.")]
        [Range(0f, 1f)]
        public float flammability = 0.5f;

        [Header("State (read-only at runtime)")]
        [Tooltip("True while any cell this object occupies is on fire. Used to measure how much of the scene is burning.")]
        public bool inflames = false;

        [Tooltip("True once every cell this object occupies has finished burning.")]
        public bool burnedOut = false;

        [Header("Events")]
        [Tooltip("Fired the moment this object catches fire. Hook flame particles / audio here.")]
        public UnityEvent onIgnite = new UnityEvent();

        [Tooltip("Fired once this object has fully burned out. Hook charred material / collapse here.")]
        public UnityEvent onBurnedOut = new UnityEvent();

        // Cached world-space bounds of this object's cells. Recomputed on start.
        private Bounds worldBounds;
        private bool registered;

        private void Start()
        {
            if (fireManager == null)
                fireManager = FindFirstObjectByType<FireManager>();

            if (fireManager == null)
            {
                Debug.LogWarning($"Flammable on '{name}' found no FireManager; it will not burn.", this);
                enabled = false;
                return;
            }

            if (!TryComputeBounds(out worldBounds))
            {
                Debug.LogWarning($"Flammable on '{name}' has no Collider/Renderer to size its fuel volume.", this);
                enabled = false;
                return;
            }

            // Paint this object's fuel into the grid so fire can exist here.
            fireManager.AddFuelInBounds(worldBounds, fuel, flammability);
            fireManager.RegisterFlammable(this);
            registered = true;
        }

        private void OnDestroy()
        {
            if (fireManager != null) fireManager.UnregisterFlammable(this);
        }

        private void Update()
        {
            if (!registered) return;

            bool nowBurning = fireManager.IsAnyBurningInBounds(worldBounds);

            // Rising edge: just caught fire.
            if (nowBurning && !inflames)
            {
                inflames = true;
                onIgnite?.Invoke();
            }
            // Falling edge: fire went out (extinguished or burned down) without full burnout.
            else if (!nowBurning && inflames)
            {
                inflames = false;
            }

            // Fully consumed check (fires once).
            if (!burnedOut && fireManager.IsFullyBurnedInBounds(worldBounds))
            {
                burnedOut = true;
                inflames = false;
                onBurnedOut?.Invoke();
            }
        }

        /// <summary>Light this object on fire directly (e.g. from a spark / ignition source).</summary>
        public void Ignite()
        {
            if (registered) fireManager.IgniteBounds(worldBounds);
        }

        /// <summary>Compute a world-space volume for this object, preferring collider over renderer.</summary>
        private bool TryComputeBounds(out Bounds bounds)
        {
            var col = GetComponentInChildren<Collider>();
            if (col != null) { bounds = col.bounds; return true; }

            var rend = GetComponentInChildren<Renderer>();
            if (rend != null) { bounds = rend.bounds; return true; }

            bounds = default;
            return false;
        }

        // Visualise the fuel volume in the editor.
        private void OnDrawGizmosSelected()
        {
            if (TryComputeBounds(out Bounds b))
            {
                Gizmos.color = inflames ? Color.red : new Color(1f, 0.6f, 0f, 0.5f);
                Gizmos.DrawWireCube(b.center, b.size);
            }
        }
    }
}
