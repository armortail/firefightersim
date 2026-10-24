using UnityEngine;
using System.Collections.Generic;

namespace FireFight.Fire
{
    /// <summary>
    /// Manages fuel types and provides utilities for fuel management
    /// </summary>
    public class FuelManager : MonoBehaviour
    {
        [Header("Default Fuel Types")]
        [SerializeField] private FuelType defaultWood;
        [SerializeField] private FuelType defaultFabric;
        [SerializeField] private FuelType defaultPaper;
        [SerializeField] private FuelType defaultPlastic;
        
        private Dictionary<string, FuelType> fuelRegistry = new Dictionary<string, FuelType>();
        
        private void Awake()
        {
            RegisterDefaultFuelTypes();
        }
        
        /// <summary>
        /// Register default fuel types
        /// </summary>
        private void RegisterDefaultFuelTypes()
        {
            if (defaultWood != null) RegisterFuelType(defaultWood);
            if (defaultFabric != null) RegisterFuelType(defaultFabric);
            if (defaultPaper != null) RegisterFuelType(defaultPaper);
            if (defaultPlastic != null) RegisterFuelType(defaultPlastic);
        }
        
        /// <summary>
        /// Register a fuel type for easy lookup
        /// </summary>
        public void RegisterFuelType(FuelType fuelType)
        {
            if (fuelType != null && !fuelRegistry.ContainsKey(fuelType.fuelName))
            {
                fuelRegistry[fuelType.fuelName] = fuelType;
                Debug.Log($"FuelManager: Registered fuel type '{fuelType.fuelName}'");
            }
        }
        
        /// <summary>
        /// Get a fuel type by name
        /// </summary>
        public FuelType GetFuelType(string fuelName)
        {
            if (fuelRegistry.TryGetValue(fuelName, out FuelType fuelType))
            {
                return fuelType;
            }
            
            Debug.LogWarning($"FuelManager: Fuel type '{fuelName}' not found");
            return null;
        }
        
        /// <summary>
        /// Get default fuel type for a material category
        /// </summary>
        public FuelType GetDefaultFuelForCategory(MaterialCategory category)
        {
            switch (category)
            {
                case MaterialCategory.Wood:
                    return defaultWood;
                case MaterialCategory.Fabric:
                    return defaultFabric;
                case MaterialCategory.Paper:
                    return defaultPaper;
                case MaterialCategory.Plastic:
                    return defaultPlastic;
                default:
                    return null;
            }
        }
        
        /// <summary>
        /// Auto-detect and assign fuel type based on GameObject
        /// </summary>
        public FuelType DetectFuelType(GameObject obj)
        {
            // Try to detect from object name or tags
            string objName = obj.name.ToLower();
            
            if (objName.Contains("wood") || objName.Contains("furniture"))
                return defaultWood;
            if (objName.Contains("fabric") || objName.Contains("curtain") || objName.Contains("cloth"))
                return defaultFabric;
            if (objName.Contains("paper") || objName.Contains("book"))
                return defaultPaper;
            if (objName.Contains("plastic"))
                return defaultPlastic;
            
            // Default to wood if nothing else matches
            return defaultWood;
        }
        
        /// <summary>
        /// Get all registered fuel types
        /// </summary>
        public List<FuelType> GetAllFuelTypes()
        {
            return new List<FuelType>(fuelRegistry.Values);
        }
    }
}
