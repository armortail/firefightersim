using UnityEngine;
using FireFight.Fire;

namespace FireFight.Environment
{
    /// <summary>
    /// Main game setup script - initializes fire system and world
    /// Attach this to a GameObject in your scene to get started quickly
    /// </summary>
    public class GameSetup : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Fire configuration to use (leave empty to use default)")]
        public FireConfig fireConfig;
        
        [Tooltip("Material for blocks (leave empty for default)")]
        public Material blockMaterial;
        
        [Header("Setup Options")]
        [Tooltip("Automatically setup fire system")]
        public bool setupFireSystem = true;
        
        [Tooltip("Automatically build test room")]
        public bool buildTestRoom = true;
        
        [Tooltip("Add furniture to room")]
        public bool addFurniture = true;
        
        [Tooltip("Start a fire on scene start")]
        public bool startFireOnBegin = false;
        
        [Tooltip("Position to start fire (if enabled)")]
        public Vector3Int fireStartPosition = new Vector3Int(5, 0, 5);
        
        private FireManager fireManager;
        private WorldBuilder worldBuilder;
        
        private void Awake()
        {
            Debug.Log("GameSetup: Initializing...");
            
            // Setup fire system
            if (setupFireSystem)
            {
                SetupFireSystem();
            }
            
            // Setup world builder
            SetupWorldBuilder();
        }
        
        private void Start()
        {
            // Build world after fire system is ready
            if (buildTestRoom)
            {
                worldBuilder.BuildSimpleRoom();
            }
            
            // Start fire if requested
            if (startFireOnBegin)
            {
                Invoke(nameof(StartInitialFire), 1f);
            }
            
            Debug.Log("GameSetup: Ready! Fire system and world are initialized.");
            Debug.Log("TIP: Add FireDemo component to see keyboard controls.");
        }
        
        /// <summary>
        /// Setup the fire simulation system
        /// </summary>
        private void SetupFireSystem()
        {
            // Check if FireManager already exists
            fireManager = FindObjectOfType<FireManager>();
            
            if (fireManager == null)
            {
                // Create fire system GameObject
                GameObject fireSystemObj = new GameObject("FireSystem");
                fireSystemObj.transform.SetParent(transform);
                
                // Add FireManager
                fireManager = fireSystemObj.AddComponent<FireManager>();
                
                // Load or assign config
                if (fireConfig == null)
                {
                    fireConfig = Resources.Load<FireConfig>("DefaultFireConfig");
                    if (fireConfig == null)
                    {
                        Debug.LogWarning("GameSetup: No FireConfig found! Fire system may not work correctly.");
                    }
                }
                
                // Assign config via reflection (since it's a SerializeField)
                if (fireConfig != null)
                {
                    var configField = typeof(FireManager).GetField("config", 
                        System.Reflection.BindingFlags.NonPublic | 
                        System.Reflection.BindingFlags.Instance);
                    configField?.SetValue(fireManager, fireConfig);
                }
                
                Debug.Log("GameSetup: Created FireManager");
            }
            else
            {
                Debug.Log("GameSetup: Found existing FireManager");
            }
        }
        
        /// <summary>
        /// Setup the world builder
        /// </summary>
        private void SetupWorldBuilder()
        {
            worldBuilder = FindObjectOfType<WorldBuilder>();
            
            if (worldBuilder == null)
            {
                GameObject worldObj = new GameObject("World");
                worldObj.transform.SetParent(transform);
                
                worldBuilder = worldObj.AddComponent<WorldBuilder>();
                worldBuilder.blockSize = fireConfig != null ? fireConfig.cellSize : 1f;
                worldBuilder.blockMaterial = blockMaterial;
                worldBuilder.buildRoomOnStart = false; // We'll build it manually
                worldBuilder.addFurniture = addFurniture;
                
                Debug.Log("GameSetup: Created WorldBuilder");
            }
        }
        
        /// <summary>
        /// Start initial fire for demo
        /// </summary>
        private void StartInitialFire()
        {
            if (fireManager != null)
            {
                // Set fuel if not already set
                FireCell cell = fireManager.GetCell(fireStartPosition);
                if (cell != null && cell.fuelType == null)
                {
                    FuelType wood = Resources.Load<FuelType>("FuelTypes/Wood");
                    fireManager.SetCellFuel(fireStartPosition, wood);
                }
                
                // Ignite
                fireManager.IgniteCell(fireStartPosition);
                Debug.Log($"GameSetup: Started fire at {fireStartPosition}");
            }
        }
    }
}
