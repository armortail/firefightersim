# Block/Cell System Guide

## Overview

A simple, blocky building system that integrates seamlessly with the fire simulation. Perfect for rapid prototyping and team collaboration.

## Components

### 1. **CellBlock** - Individual Block
Each block in the world:
- Automatically links to fire simulation
- Changes color based on fire state (burning = red, hot = orange)
- Has a block type (floor, wall, furniture, etc.)
- Simple cube mesh (no assets needed)

### 2. **WorldBuilder** - Level Builder
Creates blocky environments:
- Simple room generator
- Custom layout support
- Automatic fire system integration
- Organized hierarchy

### 3. **GameSetup** - One-Click Setup
Drop this on a GameObject and you're ready:
- Auto-creates fire system
- Builds test room
- Configures everything
- Optional auto-ignition for testing

## Quick Start (2 Steps!)

### Step 1: Add GameSetup to Scene

1. Create empty GameObject named "Game"
2. Add component: `GameSetup`
3. Check these boxes:
   - ✅ Setup Fire System
   - ✅ Build Test Room
   - ✅ Add Furniture
   - ✅ Start Fire On Begin (optional, for testing)

### Step 2: Press Play!

That's it! You'll see:
- A simple room with wooden floor
- Walls around the perimeter
- Some furniture blocks
- Fire spreading across the floor (if enabled)

## How It Works

### Block Types

```csharp
public enum BlockType
{
    Floor,      // Wooden floor (flammable)
    Wall,       // Stone walls (non-flammable)
    Ceiling,    // Ceiling blocks
    Furniture,  // Tables, chairs, etc. (flammable)
    Door,       // Door blocks (flammable)
    Window,     // Window blocks (non-flammable)
    Empty       // No block
}
```

### Visual Feedback

Blocks automatically change color based on fire state:
- **Normal**: Original block color
- **Hot** (heating up): Orange tint
- **Burning**: Pulsing red/orange
- **Smoldering**: Dark red
- **Extinguished**: Black (charred)

### Fire Integration

Each `CellBlock`:
1. Automatically finds its position in the fire grid
2. Links to the corresponding `FireCell`
3. Updates visuals based on fire state
4. No manual setup needed!

## Building Custom Levels

### Method 1: WorldBuilder in Inspector

```csharp
// Attach WorldBuilder to GameObject
public class MyLevel : MonoBehaviour
{
    void Start()
    {
        WorldBuilder builder = GetComponent<WorldBuilder>();
        builder.roomSize = new Vector3Int(20, 3, 15); // Custom size
        builder.BuildSimpleRoom();
    }
}
```

### Method 2: Programmatic Building

```csharp
using FireFight.Environment;

public class LevelGenerator : MonoBehaviour
{
    void Start()
    {
        WorldBuilder builder = GetComponent<WorldBuilder>();
        
        // Build floor
        for (int x = 0; x < 20; x++) {
            for (int z = 0; z < 20; z++) {
                builder.CreateBlock(new Vector3Int(x, 0, z), BlockType.Floor);
            }
        }
        
        // Add furniture
        builder.CreateBlock(new Vector3Int(10, 1, 10), BlockType.Furniture);
        
        // Add walls around perimeter
        for (int x = 0; x < 20; x++) {
            builder.CreateBlock(new Vector3Int(x, 1, 0), BlockType.Wall);
            builder.CreateBlock(new Vector3Int(x, 1, 19), BlockType.Wall);
        }
    }
}
```

### Method 3: From Layout Array

```csharp
void BuildCustomRoom()
{
    WorldBuilder builder = GetComponent<WorldBuilder>();
    
    // Define a 3D layout
    BlockType[,,] layout = new BlockType[10, 3, 10];
    
    // Fill with floor
    for (int x = 0; x < 10; x++)
        for (int z = 0; z < 10; z++)
            layout[x, 0, z] = BlockType.Floor;
    
    // Add walls
    for (int y = 1; y < 3; y++) {
        for (int x = 0; x < 10; x++) {
            layout[x, y, 0] = BlockType.Wall;
            layout[x, y, 9] = BlockType.Wall;
        }
        for (int z = 1; z < 9; z++) {
            layout[0, y, z] = BlockType.Wall;
            layout[9, y, z] = BlockType.Wall;
        }
    }
    
    // Add furniture
    layout[5, 1, 5] = BlockType.Furniture;
    
    builder.BuildFromLayout(layout);
}
```

## Team Collaboration

This system is designed for team work:

### For Level Designers
- Use `WorldBuilder` to create rooms
- Blocks automatically work with fire
- No scripting needed for basic levels
- Can test fire spread immediately

### For Programmers
- Extend `CellBlock` for special blocks
- Add new `BlockType` enums as needed
- Hook into fire events
- Add gameplay mechanics

### For Artists (Future)
- Replace `CellBlock` mesh with custom models
- Add textures via materials
- Blocks will maintain fire functionality
- Just swap the MeshFilter!

## Extending the System

### Custom Block Behaviors

```csharp
using FireFight.Environment;

public class ExplodingBarrel : CellBlock
{
    public float explosionRadius = 3f;
    
    private void Update()
    {
        base.Update(); // Keep fire visual updates
        
        // Explode when temperature gets too high
        if (linkedFireCell != null && 
            linkedFireCell.temperature > 500f)
        {
            Explode();
        }
    }
    
    void Explode()
    {
        // Create explosion effect
        // Ignite nearby cells
        // Destroy this block
    }
}
```

### Interactive Blocks

```csharp
public class DoorBlock : CellBlock
{
    private bool isOpen = false;
    
    public void Toggle()
    {
        isOpen = !isOpen;
        
        // When closed, blocks heat transfer
        if (linkedFireCell != null)
        {
            linkedFireCell.isBlocked = !isOpen;
        }
        
        // Update visual (slide/rotate door)
    }
    
    void OnMouseDown()
    {
        Toggle();
    }
}
```

## Example Scenarios

### Training Room 1: Kitchen Fire
```csharp
void BuildKitchen()
{
    WorldBuilder builder = GetComponent<WorldBuilder>();
    
    // Floor
    for (int x = 0; x < 8; x++)
        for (int z = 0; z < 8; z++)
            builder.CreateBlock(new Vector3Int(x, 0, z), BlockType.Floor);
    
    // Stove (furniture that will catch fire)
    builder.CreateBlock(new Vector3Int(1, 1, 1), BlockType.Furniture);
    
    // Cabinets (wooden, flammable)
    for (int x = 0; x < 3; x++)
        builder.CreateBlock(new Vector3Int(x, 1, 0), BlockType.Furniture);
    
    // Start fire on stove
    FireManager.Instance.IgniteCell(new Vector3Int(1, 1, 1));
}
```

### Training Room 2: Bedroom Fire
```csharp
void BuildBedroom()
{
    WorldBuilder builder = GetComponent<WorldBuilder>();
    builder.roomSize = new Vector3Int(12, 3, 10);
    builder.BuildSimpleRoom();
    
    // Bed (highly flammable)
    builder.CreateBlock(new Vector3Int(2, 1, 2), BlockType.Furniture);
    builder.CreateBlock(new Vector3Int(3, 1, 2), BlockType.Furniture);
    
    // Dresser
    builder.CreateBlock(new Vector3Int(8, 1, 1), BlockType.Furniture);
    builder.CreateBlock(new Vector3Int(8, 2, 1), BlockType.Furniture);
    
    // Start fire on bed (common scenario)
    FireManager.Instance.IgniteCell(new Vector3Int(2, 1, 2));
}
```

## Performance

- Lightweight: Each block is just a simple mesh with one component
- Efficient: Only active (burning) blocks update visuals
- Scalable: Can handle hundreds of blocks easily
- Room for optimization: Add LOD, occlusion culling later

## Default Room Layout

The `WorldBuilder.BuildSimpleRoom()` creates:
- **Floor**: 10x10 wooden blocks (flammable)
- **Walls**: 3 blocks high, stone (non-flammable)
- **Door**: 2-block opening in front wall
- **Window**: 1-block opening in back wall (ventilation!)
- **Furniture**: Table in center, chairs around it, cabinet against wall

All furniture is flammable and will burn!

## Tips

1. **Match Grid Sizes**: Make sure `WorldBuilder.blockSize` matches `FireConfig.cellSize` (default 1.0)

2. **Ventilation**: Windows and doors allow oxygen to replenish
   - Closed room = fire eventually suffocates
   - Open room = fire keeps burning

3. **Testing**: Enable "Start Fire On Begin" in GameSetup to immediately see fire spread

4. **Debugging**: Enable "Show Debug Gizmos" in FireConfig to see fire grid overlay

5. **Materials**: Assign a material in WorldBuilder for better visuals (or create one at runtime)

## File Structure

```
Assets/FireFight/Scripts/Environment/
├── CellBlock.cs        - Individual block component
├── WorldBuilder.cs     - Level building tools
└── GameSetup.cs        - One-click scene setup
```

## Next Steps

Now that you have a working block system:

1. **Create Training Scenarios**: Use WorldBuilder to make different room types
2. **Add Gameplay**: Player movement, fire hose, extinguisher
3. **Improve Visuals**: Add materials, textures, lighting
4. **Add Audio**: Block sounds, fire crackling
5. **Advanced Blocks**: Doors that open/close, windows that break, explosive objects

The system is simple on purpose - easy for your team to understand and extend!
