using UnityEngine;

namespace FireFight.Fire
{
    /// <summary>
    /// Manages heat transfer and temperature simulation
    /// Handles conduction, convection, and radiation
    /// </summary>
    public class HeatManager : MonoBehaviour
    {
        /// <summary>
        /// Update heat transfer for all cells
        /// </summary>
        public void UpdateHeatTransfer(FireCell[,,] cells, FireConfig config, float deltaTime)
        {
            Vector3Int size = config.gridSize;
            
            // Create temp array to avoid feedback during same frame
            float[,,] newTemperatures = new float[size.x, size.y, size.z];
            
            // Calculate new temperatures
            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    for (int z = 0; z < size.z; z++)
                    {
                        FireCell cell = cells[x, y, z];
                        float temp = cell.temperature;
                        
                        // Heat from neighbors (conduction)
                        temp += CalculateHeatFromNeighbors(cell, cells, config) * deltaTime;
                        
                        // Convection (hot air rises)
                        temp += CalculateConvection(cell, cells, config, x, y, z) * deltaTime;
                        
                        // Cooling
                        temp = Mathf.Lerp(temp, FireConstants.AMBIENT_TEMPERATURE, 
                            (1f - config.coolingRate) * deltaTime);
                        
                        newTemperatures[x, y, z] = temp;
                    }
                }
            }
            
            // Apply new temperatures
            for (int x = 0; x < size.x; x++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    for (int z = 0; z < size.z; z++)
                    {
                        cells[x, y, z].temperature = newTemperatures[x, y, z];
                    }
                }
            }
        }
        
        /// <summary>
        /// Calculate heat transfer from neighboring cells
        /// </summary>
        private float CalculateHeatFromNeighbors(FireCell cell, FireCell[,,] cells, FireConfig config)
        {
            float heatGain = 0f;
            Vector3Int pos = cell.gridPosition;
            
            // Check all 6 adjacent neighbors
            Vector3Int[] directions = new Vector3Int[]
            {
                Vector3Int.up,
                Vector3Int.down,
                Vector3Int.left,
                Vector3Int.right,
                Vector3Int.forward,
                Vector3Int.back
            };
            
            foreach (Vector3Int dir in directions)
            {
                Vector3Int neighborPos = pos + dir;
                
                // Check bounds
                if (!IsValidPosition(neighborPos, config.gridSize))
                    continue;
                
                FireCell neighbor = cells[neighborPos.x, neighborPos.y, neighborPos.z];
                
                // Temperature difference drives heat transfer
                float tempDiff = neighbor.temperature - cell.temperature;
                
                if (tempDiff > 0) // Only transfer if neighbor is hotter
                {
                    float transferRate = config.heatTransferRate;
                    
                    // Reduce transfer if blocked (walls, etc.)
                    if (cell.isBlocked || neighbor.isBlocked)
                    {
                        transferRate *= config.wallInsulationFactor;
                    }
                    
                    heatGain += tempDiff * transferRate;
                }
            }
            
            return heatGain;
        }
        
        /// <summary>
        /// Calculate convection effect (hot air rises)
        /// </summary>
        private float CalculateConvection(FireCell cell, FireCell[,,] cells, FireConfig config, int x, int y, int z)
        {
            // Hot air rises - transfer heat upward more effectively
            if (y < config.gridSize.y - 1)
            {
                FireCell aboveCell = cells[x, y + 1, z];
                float tempDiff = cell.temperature - aboveCell.temperature;
                
                if (tempDiff > 50f && cell.temperature > FireConstants.AMBIENT_TEMPERATURE + 100f)
                {
                    // Hot air rises, so upper cell gets heated more
                    return -tempDiff * config.heatTransferRate * 0.5f; // Negative = losing heat upward
                }
            }
            
            return 0f;
        }
        
        /// <summary>
        /// Apply external heat source to a cell (e.g., igniter, explosion)
        /// </summary>
        public void ApplyHeatSource(FireCell cell, float heatAmount)
        {
            if (cell != null)
            {
                cell.temperature += heatAmount;
            }
        }
        
        /// <summary>
        /// Apply cooling to a cell (e.g., water, extinguisher)
        /// </summary>
        public void ApplyCooling(FireCell cell, float coolingAmount)
        {
            if (cell != null)
            {
                cell.ApplyCooling(coolingAmount);
            }
        }
        
        private bool IsValidPosition(Vector3Int pos, Vector3Int gridSize)
        {
            return pos.x >= 0 && pos.x < gridSize.x
                && pos.y >= 0 && pos.y < gridSize.y
                && pos.z >= 0 && pos.z < gridSize.z;
        }
    }
}
