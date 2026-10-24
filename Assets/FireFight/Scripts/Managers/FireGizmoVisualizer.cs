using UnityEngine;
using FireFight.Environment;

namespace FireFight.Managers
{
    /// <summary>
    /// Draws the fire grid in the Scene view so you can watch it spread without
    /// any art assets. Purely a debug view: it only reads the grid, never
    /// changes it. Keeping this separate from FireManager means rendering and
    /// simulation can evolve independently (e.g. swap in real flame particles
    /// later and just delete this component).
    ///
    /// Attach to the same GameObject as FireManager.
    /// </summary>
    [RequireComponent(typeof(FireManager))]
    public class FireGizmoVisualizer : MonoBehaviour
    {
        [Tooltip("Draw cubes for cells that are heating up but not yet burning.")]
        public bool showHeat = true;

        [Tooltip("Relative size of each drawn cube (1 = full cell).")]
        [Range(0.1f, 1f)]
        public float cubeScale = 0.9f;

        private FireManager manager;

        private void OnDrawGizmos()
        {
            if (manager == null) manager = GetComponent<FireManager>();
            FireGrid grid = manager != null ? manager.Grid : null;
            if (grid == null) return; // grid only exists in Play mode

            float ignition = manager.config.ignitionThreshold;
            FireCell[] cells = grid.Cells;

            for (int i = 0; i < cells.Length; i++)
            {
                FireCell cell = cells[i];
                switch (cell.state)
                {
                    case FireState.Burning:
                        Gizmos.color = Color.red;
                        break;
                    case FireState.Burned:
                        Gizmos.color = Color.black;
                        break;
                    default:
                        if (!showHeat || cell.heat <= 0f) continue;
                        Gizmos.color = Color.Lerp(Color.yellow, Color.red,
                            ignition <= 0f ? 1f : cell.heat / ignition);
                        break;
                }

                Gizmos.DrawCube(grid.GridToWorld(cell.gridPosition),
                    Vector3.one * grid.CellSize * cubeScale);
            }
        }
    }
}
