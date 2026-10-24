using System.Collections.Generic;
using UnityEngine;
using FireFight.Environment;

namespace FireFight.Managers
{
    public class FireManager : MonoBehaviour
    {
        [Header("Grid")]
        [Tooltip("Number of cells along each axis (X, Y, Z).")]
        public Vector3Int gridSize = new Vector3Int(20, 1, 20);

        [Tooltip("Size of each cell in world units (metres).")]
        public float cellSize = 1f;

        [Header("Simulation Speed")]
        [Tooltip("How many simulation steps run per second. Lower = slower fire.")]
        public float ticksPerSecond = 4f;

        [Header("Spread Tuning")]
        [Tooltip("Heat a burning cell pushes into each neighbour per tick.")]
        public float heatPerTick = 0.35f;

        [Tooltip("Heat level at which an unburned cell ignites.")]
        public float ignitionThreshold = 1f;

        [Tooltip("Chance (0..1) per tick that an eligible hot neighbour actually ignites. Adds natural randomness so fire looks organic.")]
        [Range(0f, 1f)]
        public float spreadChance = 0.6f;

        [Tooltip("Fuel a burning cell consumes per tick. Higher = burns out faster.")]
        public float burnRate = 0.05f;

        [Tooltip("Heat lost per tick when a cell is NOT next to fire (natural cooling).")]
        public float coolingRate = 0.1f;

        // The grid, stored flat for speed. Index via Index(x,y,z).
        private FireCell[] cells;
        private float tickTimer;

        // Cached neighbour offsets (6-directional: up/down/left/right/front/back).
        private static readonly Vector3Int[] Neighbours =
        {
            new Vector3Int( 1, 0, 0), new Vector3Int(-1, 0, 0),
            new Vector3Int( 0, 1, 0), new Vector3Int( 0,-1, 0),
            new Vector3Int( 0, 0, 1), new Vector3Int( 0, 0,-1),
        };

        private void Awake()
        {
            BuildGrid();
        }

        /// <summary>Allocate and initialise every cell in the grid.</summary>
        private void BuildGrid()
        {
            cells = new FireCell[gridSize.x * gridSize.y * gridSize.z];
            for (int x = 0; x < gridSize.x; x++)
                for (int y = 0; y < gridSize.y; y++)
                    for (int z = 0; z < gridSize.z; z++)
                        cells[Index(x, y, z)] = new FireCell(new Vector3Int(x, y, z));
        }

        private void Update()
        {
            // Run the simulation at a fixed rate independent of frame rate.
            tickTimer += Time.deltaTime;
            float tickInterval = 1f / Mathf.Max(0.01f, ticksPerSecond);
            while (tickTimer >= tickInterval)
            {
                tickTimer -= tickInterval;
                Tick();
            }
        }

        private void Tick()
        {
            // Pass 1: burning cells push heat into neighbours and burn fuel.
            float[] heatDelta = new float[cells.Length];

            for (int i = 0; i < cells.Length; i++)
            {
                FireCell cell = cells[i];
                if (cell.state != FireState.Burning) continue;

                // Consume fuel.
                cell.fuel -= burnRate;
                if (cell.fuel <= 0f)
                {
                    cell.fuel = 0f;
                    cell.state = FireState.Burned;
                    continue;
                }

                // Heat each neighbour.
                foreach (Vector3Int offset in Neighbours)
                {
                    Vector3Int np = cell.gridPosition + offset;
                    if (!InBounds(np)) continue;
                    heatDelta[Index(np.x, np.y, np.z)] += heatPerTick;
                }
            }

            // Pass 2: apply heat, handle cooling, and ignite cells that got hot enough.
            for (int i = 0; i < cells.Length; i++)
            {
                FireCell cell = cells[i];

                if (heatDelta[i] > 0f)
                {
                    // Flammability scales how fast heat accumulates.
                    cell.heat += heatDelta[i] * Mathf.Max(0.05f, cell.flammability);
                }
                else if (cell.state == FireState.Unburned && cell.heat > 0f)
                {
                    // No nearby fire this tick: cool back down.
                    cell.heat = Mathf.Max(0f, cell.heat - coolingRate);
                }

                // Ignite if hot enough, has fuel, and the random roll passes.
                if (cell.CanIgnite && cell.heat >= ignitionThreshold)
                {
                    if (Random.value <= spreadChance)
                    {
                        cell.state = FireState.Burning;
                    }
                }
            }
        }

        // ---- Public API: this is what other scripts / the player call ----

        /// <summary>Ignite the cell at a grid position. Returns false if out of bounds / no fuel.</summary>
        public bool IgniteCell(Vector3Int gridPos)
        {
            if (!InBounds(gridPos)) return false;
            FireCell cell = cells[Index(gridPos.x, gridPos.y, gridPos.z)];
            if (cell.fuel <= 0f) return false;
            cell.state = FireState.Burning;
            cell.heat = ignitionThreshold;
            return true;
        }

        /// <summary>Ignite whatever cell contains the given world-space point.</summary>
        public bool IgniteWorld(Vector3 worldPos)
        {
            return IgniteCell(WorldToGrid(worldPos));
        }

        /// <summary>Cool / put out a cell by removing heat. Enough cooling stops the burn.</summary>
        public void ExtinguishCell(Vector3Int gridPos, float cooling)
        {
            if (!InBounds(gridPos)) return;
            FireCell cell = cells[Index(gridPos.x, gridPos.y, gridPos.z)];
            cell.heat = Mathf.Max(0f, cell.heat - cooling);
            if (cell.state == FireState.Burning && cell.heat < ignitionThreshold)
            {
                // Cooled below ignition: it stops burning but can relight if fuel remains.
                cell.state = FireState.Unburned;
            }
        }

        public FireCell GetCell(Vector3Int gridPos)
        {
            return InBounds(gridPos) ? cells[Index(gridPos.x, gridPos.y, gridPos.z)] : null;
        }

        /// <summary>How many cells are currently on fire. Useful for "how much is it spreading?".</summary>
        public int CountBurning()
        {
            int n = 0;
            for (int i = 0; i < cells.Length; i++)
                if (cells[i].state == FireState.Burning) n++;
            return n;
        }

        /// <summary>Fraction of the grid that has burned or is burning (0..1).</summary>
        public float BurnedFraction()
        {
            int n = 0;
            for (int i = 0; i < cells.Length; i++)
                if (cells[i].state != FireState.Unburned) n++;
            return cells.Length == 0 ? 0f : (float)n / cells.Length;
        }

        // ---- Coordinate helpers ----

        public Vector3 GridToWorld(Vector3Int gridPos)
        {
            return transform.position + (Vector3)gridPos * cellSize + Vector3.one * (cellSize * 0.5f);
        }

        public Vector3Int WorldToGrid(Vector3 worldPos)
        {
            Vector3 local = worldPos - transform.position;
            return new Vector3Int(
                Mathf.FloorToInt(local.x / cellSize),
                Mathf.FloorToInt(local.y / cellSize),
                Mathf.FloorToInt(local.z / cellSize));
        }

        private int Index(int x, int y, int z)
        {
            return x + gridSize.x * (y + gridSize.y * z);
        }

        private bool InBounds(Vector3Int p)
        {
            return p.x >= 0 && p.x < gridSize.x
                && p.y >= 0 && p.y < gridSize.y
                && p.z >= 0 && p.z < gridSize.z;
        }

        // ---- Scene-view visualization so you can WATCH it spread, no assets needed ----

        private void OnDrawGizmos()
        {
            if (cells == null) return;
            for (int i = 0; i < cells.Length; i++)
            {
                FireCell cell = cells[i];
                switch (cell.state)
                {
                    case FireState.Burning: Gizmos.color = Color.red; break;
                    case FireState.Burned:  Gizmos.color = Color.black; break;
                    default:
                        // Unburned: tint by how hot it's getting (dark -> orange).
                        if (cell.heat <= 0f) continue; // skip cold cells to reduce clutter
                        Gizmos.color = Color.Lerp(Color.yellow, Color.red, cell.heat / ignitionThreshold);
                        break;
                }
                Gizmos.DrawCube(GridToWorld(cell.gridPosition), Vector3.one * cellSize * 0.9f);
            }
        }
    }
}
