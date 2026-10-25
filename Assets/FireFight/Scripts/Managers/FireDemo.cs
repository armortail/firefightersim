using UnityEngine;

namespace FireFight.Managers
{
    /// <summary>
    /// Fire-system helper on the FireManager GameObject. Logs spread stats each second
    /// and can optionally start a demo fire at the grid centre on Start.
    ///
    /// Ignition is NOT player-driven: there is no key or click to start a fire. Fires are
    /// seeded by RandomFireSpawner (a random authored spawn point per play). The Space key
    /// is reserved for voice push-to-talk (see VoiceInputController).
    /// </summary>
    [RequireComponent(typeof(FireManager))]
    public class FireDemo : MonoBehaviour
    {
        [Tooltip("Debug only: start a fire at the grid centre on play. Leave OFF so " +
                 "RandomFireSpawner is the single fire source.")]
        public bool igniteCentreOnStart = false;

        private FireManager fire;
        private float logTimer;

        private void Awake()
        {
            fire = GetComponent<FireManager>();
        }

        private void Start()
        {
            if (igniteCentreOnStart)
            {
                Vector3Int centre = new Vector3Int(
                    fire.gridSize.x / 2,
                    0,
                    fire.gridSize.z / 2);
                fire.IgniteCell(centre);
            }
        }

        private void Update()
        {
            // Player-initiated ignition has been intentionally removed: fires are started
            // only by RandomFireSpawner (one random authored spawn per play). The player
            // (and NPCs) can fight fire but cannot create it.

            // Log spread stats once per second.
            logTimer += Time.deltaTime;
            if (logTimer >= 1f)
            {
                logTimer = 0f;
                Debug.Log($"Burning cells: {fire.CountBurning()} | Burned fraction: {fire.BurnedFraction():P0}");
            }
        }
    }
}
