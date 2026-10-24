using UnityEngine;
using FireFight.Fire;

namespace FireFight.Environment
{
    /// <summary>
    /// Represents a single block/cell in the game world
    /// Simple blocky style building system that integrates with fire simulation
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshFilter))]
    public class CellBlock : MonoBehaviour
    {
        [Header("Block Properties")]
        [Tooltip("Type of this block")]
        public BlockType blockType = BlockType.Floor;
        
        [Tooltip("Fuel type if this block is flammable")]
        public FuelType fuelType;
        
        [Tooltip("Grid position of this block")]
        public Vector3Int gridPosition;
        
        [Header("Visual")]
        [Tooltip("Color of this block")]
        public Color blockColor = Color.gray;
        
        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock propertyBlock;
        private FireCell linkedFireCell;
        
        private void Awake()
        {
            meshRenderer = GetComponent<MeshRenderer>();
            propertyBlock = new MaterialPropertyBlock();
            
            // Create simple cube mesh if none exists
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (meshFilter.sharedMesh == null)
            {
                meshFilter.mesh = CreateCubeMesh();
            }
            
            UpdateVisual();
        }
        
        private void Start()
        {
            // Link to fire system if it exists
            if (FireManager.Instance != null)
            {
                LinkToFireSystem();
            }
        }
        
        private void Update()
        {
            // Update visual based on fire state
            if (linkedFireCell != null)
            {
                UpdateFireVisual();
            }
        }
        
        /// <summary>
        /// Link this block to the fire simulation system
        /// </summary>
        private void LinkToFireSystem()
        {
            FireManager fire = FireManager.Instance;
            
            // Convert world position to grid position
            gridPosition = fire.WorldToGrid(transform.position);
            
            // Get the fire cell at this position
            linkedFireCell = fire.GetCell(gridPosition);
            
            // Set fuel type in fire system if flammable
            if (fuelType != null && linkedFireCell != null)
            {
                fire.SetCellFuel(gridPosition, fuelType);
            }
        }
        
        /// <summary>
        /// Update visual appearance
        /// </summary>
        private void UpdateVisual()
        {
            if (meshRenderer == null) return;
            
            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_Color", blockColor);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }
        
        /// <summary>
        /// Update visual based on fire state
        /// </summary>
        private void UpdateFireVisual()
        {
            if (linkedFireCell == null || meshRenderer == null) return;
            
            Color visualColor = blockColor;
            
            // Change color based on fire state
            switch (linkedFireCell.state)
            {
                case FireState.Burning:
                    // Pulsing red/orange
                    float pulse = Mathf.PingPong(Time.time * 2f, 1f);
                    visualColor = Color.Lerp(Color.red, new Color(1f, 0.5f, 0f), pulse);
                    break;
                    
                case FireState.Smoldering:
                    visualColor = new Color(0.3f, 0.1f, 0.1f); // Dark red
                    break;
                    
                case FireState.Extinguished:
                    visualColor = Color.black; // Charred
                    break;
                    
                default:
                    // Heat-based coloring
                    if (linkedFireCell.temperature > FireConstants.AMBIENT_TEMPERATURE + 100f)
                    {
                        float heat = (linkedFireCell.temperature - FireConstants.AMBIENT_TEMPERATURE) / 200f;
                        visualColor = Color.Lerp(blockColor, new Color(1f, 0.3f, 0f), Mathf.Clamp01(heat));
                    }
                    break;
            }
            
            meshRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor("_Color", visualColor);
            meshRenderer.SetPropertyBlock(propertyBlock);
        }
        
        /// <summary>
        /// Create a simple cube mesh
        /// </summary>
        private Mesh CreateCubeMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "CellBlock Cube";
            
            // Vertices for a unit cube
            Vector3[] vertices = new Vector3[]
            {
                // Front face
                new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f),
                
                // Back face
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, -0.5f),
                
                // Top face
                new Vector3(-0.5f, 0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f),
                
                // Bottom face
                new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, -0.5f, 0.5f),
                
                // Left face
                new Vector3(-0.5f, -0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, 0.5f),
                new Vector3(-0.5f, 0.5f, -0.5f),
                
                // Right face
                new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, 0.5f)
            };
            
            // Triangles
            int[] triangles = new int[]
            {
                // Front
                0, 2, 1, 0, 3, 2,
                // Back
                4, 6, 5, 4, 7, 6,
                // Top
                8, 10, 9, 8, 11, 10,
                // Bottom
                12, 14, 13, 12, 15, 14,
                // Left
                16, 18, 17, 16, 19, 18,
                // Right
                20, 22, 21, 20, 23, 22
            };
            
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            
            return mesh;
        }
        
        /// <summary>
        /// Set the block type and update properties
        /// </summary>
        public void SetBlockType(BlockType type)
        {
            blockType = type;
            
            // Set default properties based on type
            switch (type)
            {
                case BlockType.Floor:
                    blockColor = new Color(0.6f, 0.4f, 0.2f); // Brown
                    fuelType = Resources.Load<FuelType>("FuelTypes/Wood");
                    break;
                    
                case BlockType.Wall:
                    blockColor = new Color(0.8f, 0.8f, 0.8f); // Light gray
                    fuelType = null; // Non-flammable by default
                    break;
                    
                case BlockType.Ceiling:
                    blockColor = new Color(0.9f, 0.9f, 0.9f); // White
                    fuelType = null;
                    break;
                    
                case BlockType.Furniture:
                    blockColor = new Color(0.5f, 0.3f, 0.1f); // Dark brown
                    fuelType = Resources.Load<FuelType>("FuelTypes/Wood");
                    break;
                    
                case BlockType.Door:
                    blockColor = new Color(0.4f, 0.2f, 0.1f); // Darker brown
                    fuelType = Resources.Load<FuelType>("FuelTypes/Wood");
                    break;
                    
                case BlockType.Window:
                    blockColor = new Color(0.5f, 0.7f, 0.9f, 0.3f); // Transparent blue
                    fuelType = null;
                    break;
            }
            
            UpdateVisual();
        }
    }
    
    /// <summary>
    /// Types of blocks in the game
    /// </summary>
    public enum BlockType
    {
        Floor,
        Wall,
        Ceiling,
        Furniture,
        Door,
        Window,
        Empty
    }
}
