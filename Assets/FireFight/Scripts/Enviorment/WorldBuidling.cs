using UnityEngine;

namespace FireFight.Environment
{
    /// <summary>
    /// Builds the game world using a cell/block system
    /// Creates simple blocky environments
    /// </summary>
    public class WorldBuilder : MonoBehaviour
    {
        [Header("World Settings")]
        [Tooltip("Size of each block in world units")]
        public float blockSize = 1f;

        [Tooltip("Material to use for all blocks")]
        public Material blockMaterial;

        [Header("Room Template")]
        [Tooltip("Dimensions of the default room")]
        public Vector3Int roomSize = new Vector3Int(10, 3, 10);

        [Tooltip("Build a test room on start")]
        public bool buildRoomOnStart = true;

        [Tooltip("Add furniture to the room")]
        public bool addFurniture = true;

        private Transform blocksContainer;

        private void Start()
        {
            // container for organization
            blocksContainer = new GameObject("Blocks").transform;
            blocksContainer.SetParent(transform);

            if (buildRoomOnStart)
            {
                BuildSimpleRoom();
            }
        }

        /// <summary>
        /// Build a simple test room
        /// </summary>
        public void BuildSimpleRoom()
        {
            ClearWorld();

            Vector3Int size = roomSize;

            // Build floor
            for (int x = 0; x < size.x; x++)
            {
                for (int z = 0; z < size.z; z++)
                {
                    CreateBlock(new Vector3Int(x, 0, z), BlockType.Floor);
                }
            }

            // Build walls
            for (int y = 1; y < size.y; y++)
            {
                // Front and back walls
                for (int x = 0; x < size.x; x++)
                {
                    CreateBlock(new Vector3Int(x, y, 0), BlockType.Wall);
                    CreateBlock(new Vector3Int(x, y, size.z - 1), BlockType.Wall);
                }

                // Left and right walls
                for (int z = 1; z < size.z - 1; z++)
                {
                    CreateBlock(new Vector3Int(0, y, z), BlockType.Wall);
                    CreateBlock(new Vector3Int(size.x - 1, y, z), BlockType.Wall);
                }
            }

            Debug.Log($"WorldBuilder: Built room {size.x}x{size.y}x{size.z}");
        }

        /// <summary>
        /// Create a block at the given grid position
        /// </summary>
        public CellBlock CreateBlock(Vector3Int gridPos, BlockType type)
        {
            // Create GameObject
            GameObject blockObj = new GameObject($"Block_{gridPos.x}_{gridPos.y}_{gridPos.z}");
            blockObj.transform.SetParent(blocksContainer);

            // Position in world
            Vector3 worldPos = GridToWorld(gridPos);
            blockObj.transform.position = worldPos;
            blockObj.transform.localScale = Vector3.one * blockSize;

            // Add components
            CellBlock block = blockObj.AddComponent<CellBlock>();
            block.gridPosition = gridPos;
            block.SetBlockType(type);

            // Set material
            MeshRenderer renderer = blockObj.GetComponent<MeshRenderer>();
            if (blockMaterial != null)
            {
                renderer.material = blockMaterial;
            }
            else
            {
                // Create default material if none assigned
                Material defaultMat = new Material(Shader.Find("Standard"));
                defaultMat.color = Color.white;
                defaultMat.enableInstancing = true; // For better performance
                renderer.material = defaultMat;
            }

            // Add collider for physics
            if (type == BlockType.Floor || type == BlockType.Wall || type == BlockType.Furniture)
            {
                BoxCollider collider = blockObj.AddComponent<BoxCollider>();
            }

            return block;
        }

        /// <summary>
        /// Destroy block at grid position
        /// </summary>
        public void DestroyBlock(Vector3Int gridPos)
        {
            string blockName = $"Block_{gridPos.x}_{gridPos.y}_{gridPos.z}";
            Transform blockTransform = blocksContainer.Find(blockName);

            if (blockTransform != null)
            {
                Destroy(blockTransform.gameObject);
            }
        }

        /// <summary>
        /// Clear all blocks
        /// </summary>
        public void ClearWorld()
        {
            if (blocksContainer != null)
            {
                foreach (Transform child in blocksContainer)
                {
                    Destroy(child.gameObject);
                }
            }
        }

        /// <summary>
        /// Convert grid position to world position
        /// </summary>
        private Vector3 GridToWorld(Vector3Int gridPos)
        {
            return new Vector3(
                gridPos.x * blockSize,
                gridPos.y * blockSize,
                gridPos.z * blockSize
            );
        }

        /// <summary>
        /// Build a custom room from a 3D array
        /// </summary>
        public void BuildFromLayout(BlockType[,,] layout)
        {
            ClearWorld();

            Vector3Int size = new Vector3Int(
                layout.GetLength(0),
                layout.GetLength(1),
                layout.GetLength(2)
            );

            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    for (int z = 0; z < size.z; z++)
                    {
                        BlockType type = layout[x, y, z];
                        if (type != BlockType.Empty)
                        {
                            CreateBlock(new Vector3Int(x, y, z), type);
                        }
                    }
                }
            }

            Debug.Log($"WorldBuilder: Built custom layout {size.x}x{size.y}x{size.z}");
        }
    }
}
