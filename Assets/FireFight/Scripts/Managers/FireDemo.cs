using UnityEngine;
using UnityEngine.InputSystem;
using FireFight.Managers;

namespace FireFight.Managers
{
    /// <summary>
    /// Quick tester for the fire system. Attach to the same GameObject as the
    /// FireManager. Lets you start a fire and watch it spread in Play mode.
    ///
    /// Controls:
    ///   Space - ignite the centre cell
    ///   Left mouse - ignite the cell under the mouse (needs a collider to hit)
    ///   R - reset the scene's fire by reloading is not done here; just re-enter Play
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
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
           
            if (keyboard.spaceKey.wasPressedThisFrame && keyboard != null)
            {
                Vector3Int centre = new Vector3Int(
                    fire.gridSize.x / 2, 0, fire.gridSize.z / 2);
                fire.IgniteCell(centre);
                Debug.Log("Ignited centre cell.");
            }

            if (mouse.leftButton.wasPressedThisFrame && mouse != null)
            {
                Ray ray = Camera.main != null ? Camera.main.ScreenPointToRay(Input.mousePosition) : default;
                if (Camera.main != null && Physics.Raycast(ray, out RaycastHit hit))
                {
                    fire.IgniteWorld(hit.point);
                    Debug.Log($"Ignited near {hit.point}.");
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
