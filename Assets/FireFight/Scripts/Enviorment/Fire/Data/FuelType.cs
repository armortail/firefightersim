using UnityEngine;

namespace FireFight.Fire
{
    /// <summary>
    /// Scriptable Object defining properties of different fuel types
    /// Create instances via: Assets > Create > FireFight > Fuel Type
    /// </summary>
    [CreateAssetMenu(fileName = "New Fuel Type", menuName = "FireFight/Fuel Type")]
    public class FuelType : ScriptableObject
    {
        [Header("Identification")]
        [Tooltip("Name of this fuel type (e.g., 'Oak Wood', 'Cotton Fabric')")]
        public string fuelName = "Unknown";
        
        [Tooltip("Category this fuel belongs to")]
        public MaterialCategory category = MaterialCategory.Wood;
        
        [Header("Ignition Properties")]
        [Tooltip("Temperature (Celsius) required to ignite this material")]
        [Range(100f, 1000f)]
        public float ignitionTemperature = 300f;
        
        [Tooltip("How easily this material catches fire (0-1)")]
        [Range(0f, 1f)]
        public float flammability = 0.5f;
        
        [Header("Burning Properties")]
        [Tooltip("Initial fuel amount when full")]
        [Range(0f, 200f)]
        public float maxFuelAmount = 100f;
        
        [Tooltip("How fast fuel is consumed per second when burning")]
        [Range(0.1f, 20f)]
        public float burnRate = 2f;
        
        [Tooltip("Heat produced per second while burning")]
        [Range(0f, 500f)]
        public float heatOutput = 150f;
        
        [Tooltip("How much oxygen is consumed per second while burning")]
        [Range(0f, 0.1f)]
        public float oxygenConsumption = 0.02f;
        
        [Header("Smoke Properties")]
        [Tooltip("Amount of smoke produced per second")]
        [Range(0f, 10f)]
        public float smokeProduction = 1f;
        
        [Tooltip("Toxicity of smoke produced (affects players)")]
        [Range(0f, 1f)]
        public float smokeToxicity = 0.3f;
        
        [Header("Visual/Audio")]
        [Tooltip("Color tint for flames from this material")]
        public Color flameColor = new Color(1f, 0.5f, 0f);
        
        [Tooltip("Sound to play when burning (optional)")]
        public AudioClip burningSound;
        
        /// <summary>
        /// Get effective flammability based on moisture and other factors
        /// </summary>
        public float GetEffectiveFlammability(float moistureLevel = 0f)
        {
            return Mathf.Max(0f, flammability * (1f - moistureLevel * 0.5f));
        }
    }
}
