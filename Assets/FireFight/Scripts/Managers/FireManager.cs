using UnityEngine;
using FireFight.Environment;

namespace FireFight.Managers
{
    /// <summary>
    /// Unity entry point for the fire simulation. Responsibilities are kept
    /// narrow on purpose:
    ///   - build the grid from Inspector settings on Awake
    ///   - drive the simulation at a fixed tick rate in Update
    ///   - expose a small public API (ignite / extinguish / query)
    ///
    /// The actual storage lives in <see cref="FireGrid"/> and the spread rules
    /// in <see cref="FireSim"/>. This class just coordinates them, so it
    /// stays small and the pieces can be tested independently.
    ///
    /// Completely self-contained: it does not read or depend on scene art
    /// assets. It is its own invisible grid anchored at transform.position.
    /// </summary>
    public class FireManager : MonoBehaviour
    {
        [Header("Grid")]
        [Tooltip("Number of cells along each axis (X, Y, Z). Y > 1 enables smoke to rise.")]
        public Vector3Int gridSize = new Vector3Int(20, 6, 20);

        [Tooltip("Size of each cell in world units (metres).")]
        public float cellSize = 1f;

        [Header("Tuning")]
        public FireConfig config = new FireConfig();
        public SmokeConfig smokeConfig = new SmokeConfig();

        [Header("Fuel")]
        [Tooltip("If true, every cell starts with fuel so fire can spread freely " +
                 "(Phase 1 testing). If false, cells start with NO fuel - air does " +
                 "not burn - and you place fuel yourself via SetFuel. Smoke still " +
                 "travels through air either way.")]
        public bool fillWithFuel = true;

        /// <summary>The underlying grid (read-only access for visualizers, etc.).</summary>
        public FireGrid Grid { get; private set; }

        private FireSim simulation;
        private SmokeSim smokeSimulation;
        private float tickTimer;

        private void Awake()
        {
            Grid = new FireGrid(gridSize, cellSize, transform.position);
            simulation = new FireSim(Grid, config);
            smokeSimulation = new SmokeSim(Grid, smokeConfig);

            // Air does not burn: unless we explicitly fill the grid with fuel,
            // every cell starts with zero fuel so fire cannot spread through
            // empty space. Smoke is unaffected and still travels everywhere.
            if (!fillWithFuel)
            {
                FireCell[] cells = Grid.Cells;
                for (int i = 0; i < cells.Length; i++) cells[i].fuel = 0f;
            }
        }

        private void Update()
        {
            tickTimer += Time.deltaTime;
            while (tickTimer >= config.TickInterval)
            {
                tickTimer -= config.TickInterval;
                simulation.Tick();
                smokeSimulation.Tick();
            }
        }

        // ---- Public API ----

        /// <summary>Ignite the cell at a grid position. False if out of bounds / no fuel.</summary>
        public bool IgniteCell(Vector3Int gridPos)
        {
            FireCell cell = Grid.GetCell(gridPos);
            if (cell == null || cell.fuel <= 0f) return false;
            cell.state = FireState.Burning;
            cell.heat = config.ignitionThreshold;
            return true;
        }

        /// <summary>Ignite whatever cell contains the given world-space point.</summary>
        public bool IgniteWorld(Vector3 worldPos)
        {
            return IgniteCell(Grid.WorldToGrid(worldPos));
        }

        /// <summary>Cool / put out a cell. Enough cooling stops an active burn.</summary>
        public void ExtinguishCell(Vector3Int gridPos, float cooling)
        {
            FireCell cell = Grid.GetCell(gridPos);
            if (cell == null) return;
            cell.heat = Mathf.Max(0f, cell.heat - cooling);
            if (cell.state == FireState.Burning && cell.heat < config.ignitionThreshold)
            {
                cell.state = FireState.Unburned; // can relight if fuel remains
            }
        }

        public FireCell GetCell(Vector3Int gridPos) => Grid.GetCell(gridPos);

        /// <summary>
        /// Set a cell's fuel (0..1). Use this to mark where things can burn when
        /// fillWithFuel is off, e.g. paint fuel onto a region representing
        /// furniture or a confined, oxygen-fed space. A future oxygen system
        /// will hook in here to gate burning by available air.
        /// </summary>
        public void SetFuel(Vector3Int gridPos, float fuel)
        {
            FireCell cell = Grid.GetCell(gridPos);
            if (cell != null) cell.fuel = Mathf.Clamp01(fuel);
        }

        /// <summary>Smoke density (0..1) at a grid position, or 0 if out of bounds.</summary>
        public float GetSmoke(Vector3Int gridPos)
        {
            FireCell cell = Grid.GetCell(gridPos);
            return cell != null ? cell.smoke : 0f;
        }

        /// <summary>Smoke density (0..1) at a world-space point.</summary>
        public float GetSmokeWorld(Vector3 worldPos) => GetSmoke(Grid.WorldToGrid(worldPos));

        /// <summary>Average smoke across the whole grid (0..1). A simple "how smoky is it" metric.</summary>
        public float AverageSmoke()
        {
            FireCell[] cells = Grid.Cells;
            if (cells.Length == 0) return 0f;
            float total = 0f;
            for (int i = 0; i < cells.Length; i++) total += cells[i].smoke;
            return total / cells.Length;
        }

        /// <summary>How many cells are currently on fire.</summary>
        public int CountBurning()
        {
            int n = 0;
            FireCell[] cells = Grid.Cells;
            for (int i = 0; i < cells.Length; i++)
                if (cells[i].state == FireState.Burning) n++;
            return n;
        }

        /// <summary>Fraction of the grid that has burned or is burning (0..1).</summary>
        public float BurnedFraction()
        {
            int n = 0;
            FireCell[] cells = Grid.Cells;
            for (int i = 0; i < cells.Length; i++)
                if (cells[i].state != FireState.Unburned) n++;
            return cells.Length == 0 ? 0f : (float)n / cells.Length;
        }
    }
}
