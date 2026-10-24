using UnityEngine;

namespace FireFight.Fire
{
    /// <summary>
    /// Represents a single cell in the fire simulation grid
    /// Contains all data needed for fire physics simulation
    /// </summary>
    [System.Serializable]
    public class FireCell
    {
        // Position
        public Vector3Int gridPosition;
        public Vector3 worldPosition;
        
        // Temperature
        [Header("Temperature")]
        [Tooltip("Current temperature in Celsius")]
        public float temperature = FireConstants.AMBIENT_TEMPERATURE;
        
        [Tooltip("Previous temperature (for smoothing)")]
        private float previousTemperature;
        
        // Oxygen
        [Header("Oxygen")]
        [Tooltip("Current oxygen level (0-1, where 1 = normal air)")]
        public float oxygenLevel = FireConstants.NORMAL_OXYGEN_LEVEL;
        
        // Fuel
        [Header("Fuel")]
        [Tooltip("Type of fuel in this cell")]
        public FuelType fuelType;
        
        [Tooltip("Current amount of fuel remaining")]
        public float currentFuelAmount;
        
        [Tooltip("Moisture level affecting ignition")]
        [Range(0f, 1f)]
        public float moistureLevel = 0f;
        
        // Fire state
        [Header("Fire State")]
        public FireState state = FireState.None;
        
        [Tooltip("How long this cell has been burning")]
        public float burnDuration = 0f;
        
        [Tooltip("Time since last spread check")]
        public float timeSinceSpreadCheck = 0f;
        
        // Flags
        public bool isBlocked = false; // Walls, closed doors block heat transfer
        public bool isVentilated = true; // Open areas get fresh oxygen
        
        // Visual reference (optional)
        public GameObject visualObject;
        
        /// <summary>
        /// Initialize a new fire cell
        /// </summary>
        public FireCell(Vector3Int gridPos, Vector3 worldPos)
        {
            gridPosition = gridPos;
            worldPosition = worldPos;
            temperature = FireConstants.AMBIENT_TEMPERATURE;
            previousTemperature = temperature;
        }
        
        /// <summary>
        /// Check if this cell can support fire
        /// </summary>
        public bool CanBurn()
        {
            return fuelType != null 
                && currentFuelAmount > 0 
                && oxygenLevel >= FireConstants.MIN_OXYGEN_FOR_FIRE
                && temperature >= fuelType.ignitionTemperature;
        }
        
        /// <summary>
        /// Check if this cell can ignite
        /// </summary>
        public bool CanIgnite()
        {
            return state == FireState.None
                && fuelType != null 
                && currentFuelAmount > 0 
                && oxygenLevel >= FireConstants.MIN_OXYGEN_FOR_FIRE
                && temperature >= fuelType.ignitionTemperature;
        }
        
        /// <summary>
        /// Ignite this cell
        /// </summary>
        public void Ignite()
        {
            if (CanIgnite())
            {
                state = FireState.Burning;
                burnDuration = 0f;
                Debug.Log($"Cell at {gridPosition} ignited! Fuel: {fuelType.fuelName}");
            }
        }
        
        /// <summary>
        /// Update burning state
        /// </summary>
        public void UpdateBurn(float deltaTime)
        {
            if (state != FireState.Burning && state != FireState.Smoldering)
                return;
            
            // Consume fuel
            currentFuelAmount -= fuelType.burnRate * deltaTime;
            
            // Consume oxygen
            oxygenLevel -= fuelType.oxygenConsumption * deltaTime;
            oxygenLevel = Mathf.Max(0f, oxygenLevel);
            
            // Generate heat
            temperature += fuelType.heatOutput * deltaTime;
            
            burnDuration += deltaTime;
            
            // Check if fire should change state
            if (currentFuelAmount <= 0)
            {
                Extinguish();
            }
            else if (oxygenLevel < FireConstants.MIN_OXYGEN_FOR_FIRE)
            {
                state = FireState.Smoldering;
            }
            else if (state == FireState.Smoldering && oxygenLevel >= FireConstants.MIN_OXYGEN_FOR_FIRE)
            {
                state = FireState.Burning;
            }
        }
        
        /// <summary>
        /// Extinguish the fire in this cell
        /// </summary>
        public void Extinguish()
        {
            state = FireState.Extinguished;
            burnDuration = 0f;
            Debug.Log($"Cell at {gridPosition} extinguished.");
        }
        
        /// <summary>
        /// Cool down this cell (for water/extinguisher effects)
        /// </summary>
        public void ApplyCooling(float coolingAmount)
        {
            temperature -= coolingAmount;
            temperature = Mathf.Max(FireConstants.AMBIENT_TEMPERATURE, temperature);
            
            if (temperature < fuelType.ignitionTemperature * 0.5f)
            {
                Extinguish();
            }
        }
        
        /// <summary>
        /// Add moisture to reduce flammability
        /// </summary>
        public void ApplyMoisture(float amount)
        {
            moistureLevel = Mathf.Clamp01(moistureLevel + amount);
        }
        
        /// <summary>
        /// Get current effective flammability
        /// </summary>
        public float GetEffectiveFlammability()
        {
            if (fuelType == null) return 0f;
            return fuelType.GetEffectiveFlammability(moistureLevel);
        }
    }
}
