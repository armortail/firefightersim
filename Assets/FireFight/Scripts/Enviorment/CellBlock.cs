using UnityEngine;

namespace FireFight.Environment
{
    /// <summary>
    /// Represents a single block/cell in the game world
    /// Simple blocky style building system
    /// </summary>
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshFilter))]
    public class CellBlock : MonoBehaviour
    {
        [Header("Block Properties")]
        [Tooltip("Type of this block")]
        public BlockType blockType = BlockType.Floor;

        [Tooltip("Grid position of this block")]
        public Vector3Int gridPosition;

        [Header("Visual")]
        [Tooltip("Color of this block")]
        public Color blockColor = Color.gray;

        private MeshRenderer meshRenderer;
        private MaterialPropertyBlock propertyBlock;

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

            // FIX: Ensure material exists to prevent pink
            if (meshRenderer.sharedMaterial == null)
            {
                Material defaultMat = new Material(Shader.Find("Standard"));
                defaultMat.color = Color.white;
                meshRenderer.material = defaultMat;
            }

            UpdateVisual();
        }

        /// <summary>
        /// Update visual appearance
        /// </summary>
        public void UpdateVisual()
        {
            if (meshRenderer == null) return;

            // Directly set material color (more reliable)
            if (meshRenderer.material != null)
            {
                meshRenderer.material.color = blockColor;
            }
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
        /// set the block type and update properties
        /// </summary>
        public void SetBlockType(BlockType type)
        {
            blockType = type;

            // Set default properties based on type
            switch (type)
            {
                case BlockType.Floor:
                    blockColor = new Color(0.5f, 0.3f, 0.1f); // everything is BROWNNNN
                    break;

                case BlockType.Wall:
                    blockColor = new Color(0.5f, 0.3f, 0.1f);
                    break;

                case BlockType.Ceiling:
                    blockColor = new Color(0.5f, 0.3f, 0.1f);
                    break;

                case BlockType.Furniture:
                    blockColor = new Color(0.5f, 0.3f, 0.1f);
                    break;

                case BlockType.Door:
                    blockColor = new Color(0.5f, 0.3f, 0.1f);
                    break;

                case BlockType.Window:
                    blockColor = new Color(0.5f, 0.3f, 0.1f); 
                    break;
            }

            UpdateVisual();
        }
    }
    //block types
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
