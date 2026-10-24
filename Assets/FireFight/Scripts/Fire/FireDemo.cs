using UnityEngine;

namespace FireFight.Fire
{
    /// <summary>
    /// Demo script to test the fire simulation system
    /// Attach this to a GameObject in your scene along with FireManager
    /// </summary>
    public class FireDemo : MonoBehaviour
    {
        [Header("Demo Settings")]
        [Tooltip("Automatically ignite a cell on start")]
        public bool igniteOnStart = true;
        
        [Tooltip("Position to ignite (grid coordinates)")]
        public Vector3Int ignitionPoint = new Vector3Int(10, 0, 10);
        
        [Tooltip("Fuel type to use for demo cells")]
        public FuelType demoFuelType;
        
        [Header("Setup Area")]
        [Tooltip("Fill this area with fuel for testing")]
        public bool fillAreaWithFuel = true;
        
        [Tooltip("Min corner of area to fill")]
        public Vector3Int areaMin = new Vector3Int(8, 0, 8);
        
        [Tooltip("Max corner of area to fill")]
        public Vector3Int areaMax = new Vector3Int(12, 2, 12);
        
        [Header("Runtime Controls")]
        [Tooltip("Press this key to ignite at ignition point")]
        public KeyCode igniteKey = KeyCode.Space;
        
        [Tooltip("Press this key to extinguish all fires")]
        public KeyCode extinguishKey = KeyCode.E;
        
        private void Start()
        {
            // Wait for FireManager to initialize
            Invoke(nameof(Initialize), 0.5f);
        }
        
        private void Initialize()
        {
            if (FireManager.Instance == null)
            {
                Debug.LogError("FireDemo: FireManager not found! Make sure FireManager is in the scene.");
                return;
            }
            
            // Load default fuel type if not set
            if (demoFuelType == null)
            {
                demoFuelType = Resources.Load<FuelType>("FuelTypes/Wood");
                if (demoFuelType == null)
                {
                    Debug.LogWarning("FireDemo: Could not load default Wood fuel type");
                    return;
                }
            }
            
            // Setup test area
            if (fillAreaWithFuel)
            {
                SetupTestArea();
            }
            
            // Ignite on start
            if (igniteOnStart)
            {
                IgniteAtPoint(ignitionPoint);
            }
            
            Debug.Log("FireDemo: Initialized! Press Space to ignite, E to extinguish all");
        }
        
        private void Update()
        {
            if (Input.GetKeyDown(igniteKey))
            {
                IgniteAtPoint(ignitionPoint);
            }
            
            if (Input.GetKeyDown(extinguishKey))
            {
                ExtinguishAll();
            }
            
            // Debug info
            if (Input.GetKeyDown(KeyCode.I))
            {
                ShowInfo();
            }
        }
        
        /// <summary>
        /// Fill an area with fuel for testing
        /// </summary>
        private void SetupTestArea()
        {
            int cellCount = 0;
            
            for (int x = areaMin.x; x <= areaMax.x; x++)
            {
                for (int y = areaMin.y; y <= areaMax.y; y++)
                {
                    for (int z = areaMin.z; z <= areaMax.z; z++)
                    {
                        Vector3Int pos = new Vector3Int(x, y, z);
                        FireManager.Instance.SetCellFuel(pos, demoFuelType);
                        cellCount++;
                    }
                }
            }
            
            Debug.Log($"FireDemo: Filled {cellCount} cells with {demoFuelType.fuelName}");
        }
        
        /// <summary>
        /// Ignite at a specific grid position
        /// </summary>
        private void IgniteAtPoint(Vector3Int gridPos)
        {
            if (FireManager.Instance == null) return;
            
            // Make sure cell has fuel
            FireManager.Instance.SetCellFuel(gridPos, demoFuelType);
            
            // Apply heat to ignite
            FireCell cell = FireManager.Instance.GetCell(gridPos);
            if (cell != null)
            {
                cell.temperature = cell.fuelType.ignitionTemperature + 50f;
                FireManager.Instance.IgniteCell(gridPos);
                Debug.Log($"FireDemo: Ignited cell at {gridPos}");
            }
        }
        
        /// <summary>
        /// Extinguish all active fires
        /// </summary>
        private void ExtinguishAll()
        {
            if (FireManager.Instance == null) return;
            
            Vector3Int size = FireManager.Instance.GridSize;
            int extinguished = 0;
            
            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    for (int z = 0; z < size.z; z++)
                    {
                        FireCell cell = FireManager.Instance.GetCell(new Vector3Int(x, y, z));
                        if (cell.state == FireState.Burning || cell.state == FireState.Smoldering)
                        {
                            FireManager.Instance.ExtinguishCell(cell.gridPosition, 1000f);
                            extinguished++;
                        }
                    }
                }
            }
            
            Debug.Log($"FireDemo: Extinguished {extinguished} cells");
        }
        
        /// <summary>
        /// Show debug info
        /// </summary>
        private void ShowInfo()
        {
            if (FireManager.Instance == null) return;
            
            Vector3Int size = FireManager.Instance.GridSize;
            int burningCells = 0;
            int hotCells = 0;
            int fuelledCells = 0;
            
            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    for (int z = 0; z < size.z; z++)
                    {
                        FireCell cell = FireManager.Instance.GetCell(new Vector3Int(x, y, z));
                        
                        if (cell.state == FireState.Burning) burningCells++;
                        if (cell.temperature > FireConstants.AMBIENT_TEMPERATURE + 50f) hotCells++;
                        if (cell.fuelType != null && cell.currentFuelAmount > 0) fuelledCells++;
                    }
                }
            }
            
            Debug.Log($"FireDemo Info:\n" +
                     $"- Grid Size: {size}\n" +
                     $"- Burning Cells: {burningCells}\n" +
                     $"- Hot Cells: {hotCells}\n" +
                     $"- Fuelled Cells: {fuelledCells}");
        }
        
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 300, 200));
            GUILayout.Label("=== Fire Demo Controls ===");
            GUILayout.Label($"[{igniteKey}] Ignite at {ignitionPoint}");
            GUILayout.Label($"[{extinguishKey}] Extinguish All");
            GUILayout.Label($"[I] Show Info");
            GUILayout.Label("");
            GUILayout.Label("Check Scene view for visualization");
            GUILayout.EndArea();
        }
    }
}
