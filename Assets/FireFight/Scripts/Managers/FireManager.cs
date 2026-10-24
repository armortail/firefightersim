using UnityEngine;
using System.Collections.Generic;

namespace FireFight.Fire
{
    /// <summary>
    /// Main manager for the fire simulation system
    /// Controls the grid, updates cells, and coordinates other managers
    /// </summary>
    public class FireManager : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private FireConfig config;
        
        [Header("Managers")]
        [SerializeField] private HeatManager heatManager;
        [SerializeField] private FuelManager fuelManager;
        
        [Header("Grid")]
        [SerializeField] private Vector3 gridOrigin = Vector3.zero;
        
        // Grid data
        private FireCell[,,] cells;
        private List<FireCell> activeFires = new List<FireCell>();
        private float timeSinceLastUpdate = 0f;
        private float updateInterval;
        
        // Singleton
        public static FireManager Instance { get; private set; }
        
        // Properties
        public FireConfig Config => config;
        public Vector3Int GridSize => config.gridSize;
        public float CellSize => config.cellSize;
        
        #region Unity Lifecycle
        
        private void Awake()
        {
            // Singleton setup
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            
            // Validate configuration
            if (config == null)
            {
                Debug.LogError("FireManager: No FireConfig assigned!");
                enabled = false;
                return;
            }
            
            // Create managers if they don't exist
            SetupManagers();
            
            // Initialize grid
            InitializeGrid();
            
            // Calculate update interval
            updateInterval = 1f / config.simulationTicksPerSecond;
        }
        
        private void Update()
        {
            timeSinceLastUpdate += Time.deltaTime;
            
            if (timeSinceLastUpdate >= updateInterval)
            {
                UpdateSimulation(timeSinceLastUpdate);
                timeSinceLastUpdate = 0f;
            }
        }
        
        private void OnDrawGizmos()
        {
            if (!config || !config.showDebugGizmos || cells == null) return;
            
            DrawGridGizmos();
        }
        
        #endregion
        
        #region Initialization
        
        private void SetupManagers()
        {
            // Get or create HeatManager
            if (heatManager == null)
            {
                heatManager = GetComponent<HeatManager>();
                if (heatManager == null)
                {
                    heatManager = gameObject.AddComponent<HeatManager>();
                }
            }
            
            // Get or create FuelManager
            if (fuelManager == null)
            {
                fuelManager = GetComponent<FuelManager>();
                if (fuelManager == null)
                {
                    fuelManager = gameObject.AddComponent<FuelManager>();
                }
            }
        }
        
        private void InitializeGrid()
        {
            Vector3Int size = config.gridSize;
            cells = new FireCell[size.x, size.y, size.z];
            
            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    for (int z = 0; z < size.z; z++)
                    {
                        Vector3Int gridPos = new Vector3Int(x, y, z);
                        Vector3 worldPos = GridToWorld(gridPos);
                        cells[x, y, z] = new FireCell(gridPos, worldPos);
                    }
                }
            }
            
            Debug.Log($"FireManager: Initialized grid {size.x}x{size.y}x{size.z} = {size.x * size.y * size.z} cells");
        }
        
        #endregion
        
        #region Simulation Update
        
        private void UpdateSimulation(float deltaTime)
        {
            // Update active fires
            UpdateActiveFires(deltaTime);
            
            // Heat transfer
            if (config.enableHeatTransfer && heatManager != null)
            {
                heatManager.UpdateHeatTransfer(cells, config, deltaTime);
            }
            
            // Oxygen simulation
            if (config.enableOxygenSimulation)
            {
                UpdateOxygen(deltaTime);
            }
            
            // Fire spread
            if (config.enableFireSpread)
            {
                UpdateFireSpread(deltaTime);
            }
        }
        
        private void UpdateActiveFires(float deltaTime)
        {
            for (int i = activeFires.Count - 1; i >= 0; i--)
            {
                FireCell cell = activeFires[i];
                cell.UpdateBurn(deltaTime);
                
                // Remove if no longer burning
                if (cell.state != FireState.Burning && cell.state != FireState.Smoldering)
                {
                    activeFires.RemoveAt(i);
                }
            }
        }
        
        private void UpdateOxygen(float deltaTime)
        {
            // Simple oxygen replenishment for ventilated areas
            Vector3Int size = config.gridSize;
            
            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    for (int z = 0; z < size.z; z++)
                    {
                        FireCell cell = cells[x, y, z];
                        
                        if (cell.isVentilated && cell.oxygenLevel < FireConstants.NORMAL_OXYGEN_LEVEL)
                        {
                            cell.oxygenLevel += config.oxygenReplenishRate * deltaTime;
                            cell.oxygenLevel = Mathf.Min(FireConstants.NORMAL_OXYGEN_LEVEL, cell.oxygenLevel);
                        }
                    }
                }
            }
        }
        
        private void UpdateFireSpread(float deltaTime)
        {
            foreach (FireCell burningCell in activeFires)
            {
                burningCell.timeSinceSpreadCheck += deltaTime;
                
                if (burningCell.timeSinceSpreadCheck >= config.spreadCheckInterval)
                {
                    burningCell.timeSinceSpreadCheck = 0f;
                    CheckSpreadFromCell(burningCell);
                }
            }
        }
        
        private void CheckSpreadFromCell(FireCell burningCell)
        {
            // Get neighbors
            List<FireCell> neighbors = GetNeighbors(burningCell.gridPosition);
            
            foreach (FireCell neighbor in neighbors)
            {
                if (neighbor.CanIgnite())
                {
                    float spreadChance = CalculateSpreadChance(burningCell, neighbor);
                    
                    if (Random.value < spreadChance)
                    {
                        IgniteCell(neighbor);
                    }
                }
            }
        }
        
        private float CalculateSpreadChance(FireCell source, FireCell target)
        {
            float chance = config.baseSpreadChance;
            
            // Flammability affects spread
            chance *= target.GetEffectiveFlammability();
            
            // Temperature affects spread
            float tempFactor = target.temperature / target.fuelType.ignitionTemperature;
            chance *= Mathf.Clamp01(tempFactor);
            
            // Oxygen affects spread
            chance *= target.oxygenLevel;
            
            return chance;
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Ignite a cell at the given grid position
        /// </summary>
        public void IgniteCell(Vector3Int gridPos)
        {
            if (!IsValidGridPosition(gridPos)) return;
            
            FireCell cell = GetCell(gridPos);
            IgniteCell(cell);
        }
        
        /// <summary>
        /// Ignite a specific cell
        /// </summary>
        public void IgniteCell(FireCell cell)
        {
            if (cell == null || !cell.CanIgnite()) return;
            
            cell.Ignite();
            if (!activeFires.Contains(cell))
            {
                activeFires.Add(cell);
            }
        }
        
        /// <summary>
        /// Ignite a cell at a world position
        /// </summary>
        public void IgniteAtWorldPosition(Vector3 worldPos)
        {
            Vector3Int gridPos = WorldToGrid(worldPos);
            IgniteCell(gridPos);
        }
        
        /// <summary>
        /// Set fuel type for a cell
        /// </summary>
        public void SetCellFuel(Vector3Int gridPos, FuelType fuelType)
        {
            if (!IsValidGridPosition(gridPos)) return;
            
            FireCell cell = GetCell(gridPos);
            cell.fuelType = fuelType;
            cell.currentFuelAmount = fuelType != null ? fuelType.maxFuelAmount : 0f;
        }
        
        /// <summary>
        /// Extinguish a cell
        /// </summary>
        public void ExtinguishCell(Vector3Int gridPos, float coolingAmount = 500f)
        {
            if (!IsValidGridPosition(gridPos)) return;
            
            FireCell cell = GetCell(gridPos);
            cell.ApplyCooling(coolingAmount);
        }
        
        /// <summary>
        /// Get a cell at grid position
        /// </summary>
        public FireCell GetCell(Vector3Int gridPos)
        {
            if (!IsValidGridPosition(gridPos)) return null;
            return cells[gridPos.x, gridPos.y, gridPos.z];
        }
        
        /// <summary>
        /// Get a cell at world position
        /// </summary>
        public FireCell GetCellAtWorldPosition(Vector3 worldPos)
        {
            return GetCell(WorldToGrid(worldPos));
        }
        
        /// <summary>
        /// Get all neighbors of a cell (6-direction)
        /// </summary>
        public List<FireCell> GetNeighbors(Vector3Int gridPos)
        {
            List<FireCell> neighbors = new List<FireCell>();
            
            // 6 directions: up, down, left, right, forward, back
            Vector3Int[] directions = new Vector3Int[]
            {
                Vector3Int.up,
                Vector3Int.down,
                Vector3Int.left,
                Vector3Int.right,
                Vector3Int.forward,
                Vector3Int.back
            };
            
            foreach (Vector3Int dir in directions)
            {
                Vector3Int neighborPos = gridPos + dir;
                if (IsValidGridPosition(neighborPos))
                {
                    neighbors.Add(GetCell(neighborPos));
                }
            }
            
            return neighbors;
        }
        
        #endregion
        
        #region Coordinate Conversion
        
        public Vector3 GridToWorld(Vector3Int gridPos)
        {
            return gridOrigin + new Vector3(
                gridPos.x * config.cellSize,
                gridPos.y * config.cellSize,
                gridPos.z * config.cellSize
            );
        }
        
        public Vector3Int WorldToGrid(Vector3 worldPos)
        {
            Vector3 localPos = worldPos - gridOrigin;
            return new Vector3Int(
                Mathf.FloorToInt(localPos.x / config.cellSize),
                Mathf.FloorToInt(localPos.y / config.cellSize),
                Mathf.FloorToInt(localPos.z / config.cellSize)
            );
        }
        
        public bool IsValidGridPosition(Vector3Int gridPos)
        {
            return gridPos.x >= 0 && gridPos.x < config.gridSize.x
                && gridPos.y >= 0 && gridPos.y < config.gridSize.y
                && gridPos.z >= 0 && gridPos.z < config.gridSize.z;
        }
        
        #endregion
        
        #region Debug Visualization
        
        private void DrawGridGizmos()
        {
            Vector3Int size = config.gridSize;
            
            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    for (int z = 0; z < size.z; z++)
                    {
                        FireCell cell = cells[x, y, z];
                        
                        // Choose color based on state
                        Color color = config.debugNormalColor;
                        
                        if (cell.state == FireState.Burning)
                        {
                            color = config.debugBurningColor;
                        }
                        else if (cell.temperature > FireConstants.AMBIENT_TEMPERATURE + 50f)
                        {
                            color = config.debugHotColor;
                        }
                        else if (cell.fuelType == null)
                        {
                            continue; // Don't draw empty cells
                        }
                        
                        Gizmos.color = color;
                        Gizmos.DrawCube(cell.worldPosition, Vector3.one * config.cellSize * 0.9f);
                    }
                }
            }
        }
        
        #endregion
    }
}
