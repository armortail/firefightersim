# Fire Simulation System - Setup Guide

## Overview
This is a complete cell-based fire simulation system for Unity, designed for realistic firefighter training simulation.

## Architecture

### **Managers** (Coordinate the system)
- **FireManager** - Main controller, manages the 3D grid and simulation loop
- **HeatManager** - Handles temperature simulation and heat transfer between cells
- **FuelManager** - Manages fuel types and provides utilities

### **Core Data Classes**
- **FireCell** - Individual cell in the 3D grid containing temperature, oxygen, fuel data
- **FuelType** - ScriptableObject defining properties of different materials (wood, paper, etc.)
- **FireConfig** - ScriptableObject with all simulation settings

### **Utilities**
- **FireConstants** - Constants and enums used throughout the system

## Quick Start

### 1. Create Configuration

1. In Unity, right-click in Project window → `Create > FireFight > Fire Config`
2. Name it `MainFireConfig`
3. Adjust settings:
   - **Grid Size**: How many cells (default 20x10x20 = 4000 cells)
   - **Cell Size**: Size in meters (default 1m per cell)
   - **Simulation Ticks**: How fast it updates (10 Hz recommended)

### 2. Create Fuel Types

Fuel type assets are already created in `Assets/FireFight/Resources/FuelTypes/`:
- **Wood** - Medium flammability, steady burn
- **Paper** - High flammability, burns quickly
- **Fabric** - High flammability, moderate burn
- **Plastic** - High heat, toxic smoke

To create custom fuel types:
1. Right-click → `Create > FireFight > Fuel Type`
2. Configure properties:
   - **Ignition Temperature**: How hot to ignite (Celsius)
   - **Flammability**: How easily it catches fire (0-1)
   - **Burn Rate**: How fast fuel is consumed
   - **Heat Output**: Heat generated while burning

### 3. Setup Scene

1. Create empty GameObject named "FireSystem"
2. Add component: `FireManager`
3. Assign your `FireConfig` to the config slot
4. The system will auto-create `HeatManager` and `FuelManager` components

### 4. Add Demo Script (Optional)

1. On same GameObject, add `FireDemo` component
2. Configure demo settings:
   - Enable "Ignite On Start" to see it in action
   - Set area to fill with fuel
3. Play the scene!

## How It Works

### Fire Triangle Simulation

The system simulates the **fire triangle** (Fuel + Oxygen + Heat = Fire):

1. **Heat Transfer**
   - Cells transfer temperature to neighbors (conduction)
   - Hot air rises (convection)
   - Walls reduce heat transfer

2. **Ignition**
   - When cell temperature ≥ ignition point + has fuel + has oxygen → ignites
   - Different materials have different ignition temperatures

3. **Burning**
   - Consumes fuel over time
   - Consumes oxygen
   - Generates heat (raises temperature)
   - Can spread to adjacent cells

4. **Fire Spread**
   - Periodically checks if adjacent cells can ignite
   - Spread probability based on:
     - Target material flammability
     - Target temperature
     - Available oxygen
     - Moisture level

5. **Extinguishing**
   - Fire goes out when: fuel depleted, oxygen too low, or cooled below threshold

## Using the System in Code

### Basic Operations

```csharp
using FireFight.Fire;

// Get the manager
FireManager fireManager = FireManager.Instance;

// Set fuel for a cell at grid position
Vector3Int gridPos = new Vector3Int(10, 0, 10);
FuelType wood = Resources.Load<FuelType>("FuelTypes/Wood");
fireManager.SetCellFuel(gridPos, wood);

// Ignite a cell
fireManager.IgniteCell(gridPos);

// Or ignite at world position
Vector3 worldPos = new Vector3(10f, 0f, 10f);
fireManager.IgniteAtWorldPosition(worldPos);

// Extinguish a cell (e.g., with water)
fireManager.ExtinguishCell(gridPos, coolingAmount: 500f);

// Get cell info
FireCell cell = fireManager.GetCell(gridPos);
Debug.Log($"Temperature: {cell.temperature}°C");
Debug.Log($"Burning: {cell.state == FireState.Burning}");
Debug.Log($"Fuel remaining: {cell.currentFuelAmount}");
```

### Coordinate Conversion

```csharp
// Convert world position to grid
Vector3 worldPos = transform.position;
Vector3Int gridPos = fireManager.WorldToGrid(worldPos);

// Convert grid to world
Vector3Int gridPos = new Vector3Int(5, 0, 5);
Vector3 worldPos = fireManager.GridToWorld(gridPos);
```

### Getting Cell Neighbors

```csharp
Vector3Int gridPos = new Vector3Int(10, 0, 10);
List<FireCell> neighbors = fireManager.GetNeighbors(gridPos);

foreach (FireCell neighbor in neighbors)
{
    Debug.Log($"Neighbor at {neighbor.gridPosition} is {neighbor.temperature}°C");
}
```

## Advanced Features

### Blocking Heat Transfer (Walls)

```csharp
FireCell cell = fireManager.GetCell(gridPos);
cell.isBlocked = true; // This cell blocks heat transfer (wall, door, etc.)
```

### Ventilation Control

```csharp
FireCell cell = fireManager.GetCell(gridPos);
cell.isVentilated = false; // No fresh oxygen (closed room)
// Oxygen will deplete as fire burns, eventually suffocating

cell.isVentilated = true; // Fresh oxygen (open area, broken window)
// Oxygen replenishes over time
```

### Moisture

```csharp
FireCell cell = fireManager.GetCell(gridPos);
cell.ApplyMoisture(0.5f); // 50% moisture reduces flammability
```

### Manual Heat Application

```csharp
HeatManager heatManager = fireManager.GetComponent<HeatManager>();
FireCell cell = fireManager.GetCell(gridPos);

// Apply heat (explosion, torch, etc.)
heatManager.ApplyHeatSource(cell, 500f);

// Apply cooling (water, extinguisher)
heatManager.ApplyCooling(cell, 300f);
```

## Performance Tips

1. **Grid Size**: Larger grids = more cells = more computation
   - 20x10x20 (4,000 cells) is good for most rooms
   - For large buildings, consider 50x20x50 (50,000 cells) with lower tick rate

2. **Tick Rate**: Balance responsiveness vs performance
   - 10 Hz (every 0.1s) is good default
   - 5 Hz for slower, less demanding simulation
   - 20 Hz for very responsive simulation (more CPU)

3. **Simulation Radius**: (Not yet implemented)
   - Future feature: only simulate cells near active fires

4. **Debug Gizmos**: Disable in production
   - Set `showDebugGizmos = false` in FireConfig

## Debugging

### Scene View Visualization

When `showDebugGizmos` is enabled, you'll see:
- **Green cubes**: Normal cells with fuel
- **Orange cubes**: Hot cells (heating up)
- **Red cubes**: Burning cells

### Console Logs

The system logs important events:
- Cell ignition
- Cell extinguished
- System initialization

### Runtime Info

Use the FireDemo script's "I" key to see:
- Total burning cells
- Total hot cells
- Total fuelled cells

## Next Steps

### Recommended Additions:

1. **Visual Effects**
   - Particle systems for flames
   - Particle systems for smoke
   - Heat distortion shader
   - Dynamic lighting from flames

2. **Audio**
   - Burning sounds per fuel type
   - Crackling, roaring based on intensity
   - Positional audio

3. **Player Interaction**
   - Firefighter tools (hose, extinguisher)
   - Water simulation
   - Breaking windows for ventilation
   - Door opening/closing

4. **Training Features**
   - Flashover detection
   - Backdraft simulation
   - Visibility reduction from smoke
   - Toxic smoke damage
   - Decision points and scoring

5. **Performance**
   - LOD system for distant fires
   - Spatial partitioning
   - Multi-threading for large grids

## File Structure

```
Assets/FireFight/
├── Resources/
│   ├── DefaultFireConfig.asset
│   └── FuelTypes/
│       ├── Wood.asset
│       ├── Paper.asset
│       ├── Fabric.asset
│       └── Plastic.asset
├── Scripts/
│   ├── Managers/
│   │   ├── FireManager.cs        (Main controller)
│   │   ├── HeatManager.cs        (Heat simulation)
│   │   └── FuelManager.cs        (Fuel utilities)
│   └── Fire/
│       ├── Data/
│       │   ├── FireCell.cs       (Cell data structure)
│       │   └── FuelType.cs       (Fuel properties)
│       ├── Config/
│       │   └── FireConfig.cs     (Configuration)
│       ├── Utils/
│       │   └── FireConstants.cs  (Constants & enums)
│       └── FireDemo.cs           (Demo/test script)
```

## Support

For questions or issues, check the code comments - every class and method is documented!
