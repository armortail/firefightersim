using UnityEngine;

namespace FireFight.Fire
{
    /// <summary>
    /// Constants and enums for the fire simulation system
    /// </summary>
    public static class FireConstants
    {
        // Temperature constants (Celsius)
        public const float AMBIENT_TEMPERATURE = 20f;
        public const float FLASHOVER_TEMPERATURE = 600f;
        public const float MIN_IGNITION_TEMPERATURE = 200f;
        
        // Oxygen constants (0-1 scale, where 1 = 21% O2 in air)
        public const float NORMAL_OXYGEN_LEVEL = 1f;
        public const float MIN_OXYGEN_FOR_FIRE = 0.6f; // Below ~13% O2, fire struggles
        public const float SUFFOCATION_LEVEL = 0.3f;
        
        // Heat transfer constants
        public const float HEAT_TRANSFER_RATE = 0.1f;
        public const float COOLING_RATE = 0.95f; // Multiplier per second
        public const float COMBUSTION_HEAT = 100f; // Heat added per second when burning
        
        // Fire spread
        public const float BASE_SPREAD_CHANCE = 0.05f;
        public const float SPREAD_CHECK_INTERVAL = 0.5f; // Check spread every 0.5 seconds
        
        // Grid settings
        public const float DEFAULT_CELL_SIZE = 1f; // 1 meter per cell
    }
    
    /// <summary>
    /// Fire state for a cell
    /// </summary>
    public enum FireState
    {
        None,           // No fire
        Igniting,       // Heating up, about to catch fire
        Burning,        // Actively burning
        Smoldering,     // Low oxygen, struggling to burn
        Extinguished    // Was on fire, now out
    }
    
    /// <summary>
    /// Material categories for fuel
    /// </summary>
    public enum MaterialCategory
    {
        Wood,
        Fabric,
        Paper,
        Plastic,
        Liquid,
        Gas,
        Metal,
        Stone,
        NonFlammable
    }
}
