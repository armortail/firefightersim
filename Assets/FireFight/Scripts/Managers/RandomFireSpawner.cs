using UnityEngine;

namespace FireFight.Managers
{
    /// <summary>
    /// Starts the fire at ONE of several authored spawn points, chosen at random each play.
    /// Only one activates per run - never two at once.
    ///
    /// How to set the exact spawn points:
    ///   Put an (empty) GameObject at each spot you want a fire to be able to start - e.g.
    ///   a stove, a couch, a wastebasket - and drag them into the <see cref="spawnPoints"/>
    ///   list in the Inspector. On play this picks one at random and ignites the fuel around
    ///   that point. Add two (or more) and you get the "random which one" behaviour.
    ///
    /// Like <see cref="SeedIgniter"/>, ignition is deferred to the first Update so every
    /// <see cref="Flammable.Start"/> has already painted its fuel into the grid - otherwise
    /// the chosen cell has no fuel and ignition silently no-ops. It ignites a small box
    /// around the point (not a single cell) so it still catches when the exact cell is air
    /// but a fuelled object sits right next to it.
    /// </summary>
    public class RandomFireSpawner : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The fire system to ignite. Found automatically if left empty.")]
        [SerializeField] private FireManager fireManager;

        [Header("Spawn points")]
        [Tooltip("Candidate fire locations. One is chosen at random on play. Place a GameObject " +
                 "at each exact spot and drag them here (two or more for random selection).")]
        [SerializeField] private Transform[] spawnPoints;

        [Tooltip("Radius (world units) of the ignition burst around the chosen point. Larger = " +
                 "more forgiving if the exact cell is empty air next to a fuelled object.")]
        [SerializeField, Min(0f)] private float igniteRadius = 1.5f;

        [Header("Randomness")]
        [Tooltip("If true, use the seed below for a repeatable choice (handy for testing). " +
                 "If false, the choice is different every play.")]
        [SerializeField] private bool useFixedSeed = false;

        [SerializeField] private int randomSeed = 0;

        [Header("Debug")]
        [SerializeField] private bool logChoice = true;

        /// <summary>The spawn point chosen this run (null until ignition happens).</summary>
        public Transform ChosenSpawn { get; private set; }

        private bool done;

        private void Update()
        {
            if (done) return;
            done = true; // ignite exactly once, on the first Update (after Flammable.Start)

            if (fireManager == null) fireManager = FindFirstObjectByType<FireManager>();
            if (fireManager == null)
            {
                Debug.LogWarning("[FireFight] RandomFireSpawner found no FireManager; no fire will start.", this);
                return;
            }

            Transform chosen = ChooseSpawn();
            if (chosen == null)
            {
                Debug.LogWarning("[FireFight] RandomFireSpawner has no valid spawn points assigned.", this);
                return;
            }

            ChosenSpawn = chosen;
            IgniteAt(chosen.position);
        }

        /// <summary>Pick one non-null spawn point at random (optionally seeded).</summary>
        private Transform ChooseSpawn()
        {
            if (spawnPoints == null || spawnPoints.Length == 0) return null;

            // Collect valid (non-null) candidates so empty array slots don't skew the roll.
            int validCount = 0;
            for (int i = 0; i < spawnPoints.Length; i++)
                if (spawnPoints[i] != null) validCount++;
            if (validCount == 0) return null;

            int pick = useFixedSeed
                ? new System.Random(randomSeed).Next(validCount)
                : Random.Range(0, validCount);

            // Walk to the pick-th valid entry.
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                if (spawnPoints[i] == null) continue;
                if (pick == 0) return spawnPoints[i];
                pick--;
            }
            return null;
        }

        /// <summary>Ignite a small box around a world point so nearby fuel catches.</summary>
        private void IgniteAt(Vector3 worldPoint)
        {
            var bounds = new Bounds(worldPoint, Vector3.one * (igniteRadius * 2f));
            fireManager.IgniteBounds(bounds);

            bool lit = fireManager.IsAnyBurningInBounds(bounds);
            if (!lit)
            {
                // The box had no fuelled cells - try the single cell at the point as a last resort.
                lit = fireManager.IgniteWorld(worldPoint);
            }

            if (logChoice)
            {
                string where = ChosenSpawn != null ? ChosenSpawn.name : worldPoint.ToString();
                if (lit) Debug.Log($"[FireFight] Fire started at spawn point '{where}'.", this);
                else Debug.LogWarning($"[FireFight] RandomFireSpawner chose '{where}' but found no fuel " +
                                      "there to ignite (no Flammable object within the ignite radius).", this);
            }
        }

        // Visualise the spawn points + ignite radius in the editor so you can place them exactly.
        private void OnDrawGizmos()
        {
            if (spawnPoints == null) return;
            foreach (var p in spawnPoints)
            {
                if (p == null) continue;
                Gizmos.color = new Color(1f, 0.4f, 0.05f, 0.9f);
                Gizmos.DrawWireSphere(p.position, igniteRadius);
                Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.35f);
                Gizmos.DrawSphere(p.position, 0.25f);
            }
        }
    }
}
