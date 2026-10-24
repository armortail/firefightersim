using UnityEngine;

namespace FireFight.Fire
{
    /// <summary>
    /// Configuration settings for the fire simulation
    /// Create via: Assets > Create > FireFight > Fire Config
    /// </summary>
    [CreateAssetMenu(fileName = "Fire Config", menuName = "FireFight/Fire Config")]
    public class FireConfig : ScriptableObject
    {
        [Header("Grid Settings")]
        [Tooltip("Size of each cell in meters")]
        public float cellSize = 1f;
        
        [Tooltip("Grid dimensions (X, Y, Z)")]
        public Vector3Int gridSize = new Vector3Int(20, 10, 20);
        
        [Header("Simulation Settings")]
        [Tooltip("How many times per second to update fire simulation")]
        [Range(1, 60)]
        public int simulationTicksPerSecond = 10;
        
        [Tooltip("Enable heat transfer between cells")]
        public bool enableHeatTransfer = true;
        
        [Tooltip("Enable oxygen simulation")]
        public bool enableOxygenSimulation = true;
        
        [Tooltip("Enable fire spread")]
        public bool enableFireSpread = true;
        
        [Header("Heat Transfer")]
        [Tooltip("How quickly heat transfers between cells")]
        [Range(0.01f, 1f)]
        public float heatTransferRate = 0.1f;
        
        [Tooltip("How quickly cells cool down")]
        [Range(0.8f, 0.99f)]
        public float coolingRate = 0.95f;
        
        [Tooltip("Heat transfer is reduced through walls")]
        [Range(0f, 1f)]
        public float wallInsulationFactor = 0.3f;
        
        [Header("Fire Spread")]
        [Tooltip("Base chance of fire spreading to adjacent cell")]
        [Range(0f, 1f)]
        public float baseSpreadChance = 0.05f;
        
        [Tooltip("How often to check for fire spread (seconds)")]
        [Range(0.1f, 2f)]
        public float spreadCheckInterval = 0.5f;
        
        [Header("Oxygen Simulation")]
        [Tooltip("How quickly oxygen is replenished in ventilated areas")]
        [Range(0f, 0.1f)]
        public float oxygenReplenishRate = 0.01f;
        
        [Tooltip("How quickly oxygen spreads between cells")]
        [Range(0f, 1f)]
        public float oxygenDiffusionRate = 0.2f;
        
        [Header("Performance")]
        [Tooltip("Only simulate cells within this distance from active fires")]
        public float simulationRadius = 20f;
        
        [Tooltip("Enable multi-threading for simulation")]
        public bool enableMultithreading = false;
        
        [Header("Debug")]
        [Tooltip("Show debug visualization in Scene view")]
        public bool showDebugGizmos = true;
        
        [Tooltip("Color for normal cells in debug view")]
        public Color debugNormalColor = new Color(0, 1, 0, 0.2f);
        
        [Tooltip("Color for burning cells in debug view")]
        public Color debugBurningColor = new Color(1, 0, 0, 0.8f);
        
        [Tooltip("Color for hot cells in debug view")]
        public Color debugHotColor = new Color(1, 0.5f, 0, 0.5f);
    }
}
