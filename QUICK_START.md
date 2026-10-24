# 🚀 Quick Start Guide - Fire Fighter Sim

## Get Running in 2 Minutes!

### Option 1: One-Click Setup (Recommended)

1. **Open Unity** and load your scene
2. **Create Empty GameObject**:
   - Name it: `Game`
3. **Add Component**: `GameSetup`
4. **Check these boxes in Inspector**:
   - ✅ Setup Fire System
   - ✅ Build Test Room  
   - ✅ Add Furniture
   - ✅ Start Fire On Begin (for demo)
5. **Press Play!**

You'll see a simple room with fire spreading across the floor. 🔥

### Option 2: Manual Setup (More Control)

#### Step 1: Create Fire System
1. Create Empty GameObject → Name: `FireSystem`
2. Add Component → `FireManager`
3. In Inspector, assign `DefaultFireConfig` from `Assets/FireFight/Resources/`

#### Step 2: Create World
1. Create Empty GameObject → Name: `World`
2. Add Component → `WorldBuilder`
3. Check `Build Room On Start`
4. Check `Add Furniture`

#### Step 3: Press Play!

## What You'll See

```
┌─────────────────────────────────────┐
│  🚪                           🪟    │
│                                     │
│     🪑        🔥🔥        🪑       │
│              🪑🔥🪑                │
│     🪑                   🪑       │
│                                     │
│                                     │
│                  📦📦              │
│                  📦📦              │
└─────────────────────────────────────┘
```

- **Gray walls** (non-flammable)
- **Brown floor** (flammable wood)
- **Brown blocks** (furniture - flammable)
- **Opening at front** (door)
- **Opening at back** (window for ventilation)

## Controls (When FireDemo Added)

Add `FireDemo` component to see controls:

- **Space**: Ignite fire at center
- **E**: Extinguish all fires
- **I**: Show info in console

## Scene View Debugging

In Scene view, you'll see colored cubes:
- 🟢 **Green**: Normal cells with fuel
- 🟠 **Orange**: Hot cells (heating up)
- 🔴 **Red**: Burning cells

Toggle this with `Show Debug Gizmos` in FireConfig.

## For Your Team

### Level Designers
Use `WorldBuilder` to create rooms:
```csharp
WorldBuilder builder = GetComponent<WorldBuilder>();
builder.roomSize = new Vector3Int(15, 3, 12); // Custom size
builder.BuildSimpleRoom();
```

### Programmers  
Access fire system:
```csharp
FireManager fire = FireManager.Instance;
fire.IgniteCell(new Vector3Int(5, 0, 5));
FireCell cell = fire.GetCell(pos);
Debug.Log($"Temperature: {cell.temperature}°C");
```

### Artists (Future)
Replace block meshes:
1. Keep `CellBlock` component
2. Replace `MeshFilter` mesh with your model
3. Fire simulation still works!

## File Structure

```
Assets/FireFight/
├── Resources/
│   ├── DefaultFireConfig.asset    ← Main settings
│   └── FuelTypes/                 ← Material types
├── Scripts/
│   ├── Managers/                  ← Fire system
│   └── Environment/               ← Block building
└── Scenes/
    └── SampleScene.unity          ← Your scene
```

## Documentation

- **FIRE_SYSTEM_GUIDE.md** - Complete fire system docs
- **BLOCK_SYSTEM_GUIDE.md** - Block building system docs  
- **IMPLEMENTATION_SUMMARY.md** - Architecture overview

## Common Issues

### "FireManager not found"
- Make sure you have a GameObject with `FireManager` component
- Or enable "Setup Fire System" in `GameSetup`

### "No FireConfig assigned"
- Assign `DefaultFireConfig` from Resources folder
- Or create new: Right-click → `Create > FireFight > Fire Config`

### "Fire not spreading"
- Check that blocks have fuel: `fire.SetCellFuel(pos, fuelType)`
- Enable "Show Debug Gizmos" to see which cells have fuel

### "Can't see blocks"
- Blocks need a material - assign one in WorldBuilder
- Or they'll create a default white material

## Next Steps

1. ✅ **Get it running** (you're here!)
2. 🎨 **Add visuals**: Materials, particles, lighting
3. 🎮 **Add gameplay**: Player movement, fire hose
4. 🔊 **Add audio**: Fire sounds, ambient
5. 🏆 **Add training**: Scenarios, scoring, objectives

## Need Help?

Check the detailed guides:
- Architecture questions → `IMPLEMENTATION_SUMMARY.md`
- Fire system API → `FIRE_SYSTEM_GUIDE.md`  
- Building levels → `BLOCK_SYSTEM_GUIDE.md`

---

**You're all set!** The fire simulation is running and your team can start building scenarios. 🔥🚒
