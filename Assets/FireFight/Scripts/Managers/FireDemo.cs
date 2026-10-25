using UnityEngine;
using UnityEngine.InputSystem;

namespace FireFight.Managers
{
    /// <summary>
    /// Quick tester for the fire system. Attach to the same GameObject as the
    /// FireManager. Lets you start a fire and watch it spread in Play mode.
    ///
    /// Controls:
    ///   Left mouse - ignite the cell under the mouse (needs a collider to hit)
    ///
    /// Note: the Space key is intentionally NOT used here - it is reserved for the
    /// voice-command push-to-talk (see VoiceInputController).
    /// </summary>
    [RequireComponent(typeof(FireManager))]
    public class FireDemo : MonoBehaviour
    {
        public bool igniteCentreOnStart = true;

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
            Mouse mouse = Mouse.current;

            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                Camera viewCamera = Camera.main;
                if (viewCamera != null)
                {
                    Ray ray = viewCamera.ScreenPointToRay(mouse.position.ReadValue());
                    if (Physics.Raycast(ray, out RaycastHit hit))
                    {
                        fire.IgniteWorld(hit.point);
                        Debug.Log($"Ignited near {hit.point}.");
                    }
                }
            }

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
