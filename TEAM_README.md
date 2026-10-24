# Fire Fighter Sim - Team Development Guide

## 🎯 What's Ready

Your firefighter training simulator now has:

✅ **Complete fire simulation system** - Realistic heat, oxygen, and fuel physics  
✅ **Simple block building system** - Easy level creation without assets  
✅ **Automatic fire-to-world integration** - Blocks show fire state visually  
✅ **One-click setup** - `GameSetup` component gets you running instantly  
✅ **Full documentation** - Architecture, API, guides included

## 🚀 Getting Started

### For Everyone
1. Read **QUICK_START.md** - Get running in 2 minutes
2. Open Unity and run the demo scene

### For Level Designers
1. Use `WorldBuilder` component to create rooms
2. See **BLOCK_SYSTEM_GUIDE.md** for level building
3. No coding required for basic rooms

### For Programmers
1. See **FIRE_SYSTEM_GUIDE.md** for API reference
2. See **IMPLEMENTATION_SUMMARY.md** for architecture
3. Extend `CellBlock` for custom block behaviors

### For Artists
1. Current system uses simple cubes (no assets needed)
2. You can replace meshes later while keeping fire functionality
3. Add materials in `WorldBuilder.blockMaterial`

## 📁 Project Structure

```
Assets/FireFight/
├── Resources/
│   ├── DefaultFireConfig.asset     # Main simulation settings
│   └── FuelTypes/                  # Material definitions
│       ├── Wood.asset              # Standard fuel types included
│       ├── Paper.asset
│       ├── Fabric.asset
│       └── Plastic.asset
│
├── Scripts/
│   ├── Managers/                   # Core fire simulation
│   │   ├── FireManager.cs          # Main controller
│   │   ├── HeatManager.cs          # Temperature physics
│   │   └── FuelManager.cs          # Fuel utilities
│   │
│   ├── Fire/                       # Fire system components
│   │   ├── Data/
│   │   │   ├── FireCell.cs         # Grid cell data
│   │   │   └── FuelType.cs         # Material properties
│   │   ├── Config/
│   │   │   └── FireConfig.cs       # Configuration
│   │   ├── Utils/
│   │   │   └── FireConstants.cs    # Constants
│   │   └── FireDemo.cs             # Demo/testing
│   │
│   └── Environment/                # World building system
│       ├── CellBlock.cs            # Individual block
│       ├── WorldBuilder.cs         # Level builder
│       └── GameSetup.cs            # One-click setup
│
└── Scenes/
    └── SampleScene.unity           # Demo scene
```

## 🔥 How Fire Works

### The Fire Triangle
Fire needs all three:
1. **Fuel** - Burnable materials (wood, fabric, etc.)
2. **Oxygen** - Air (depletes in closed rooms)
3. **Heat** - Temperature above ignition point

### Fire Spreads When:
- Adjacent cell is hot enough (≥ ignition temperature)
- Has fuel available
- Has enough oxygen
- Random chance based on flammability

### You Can:
- Ignite cells: `FireManager.Instance.IgniteCell(pos)`
- Extinguish: `FireManager.Instance.ExtinguishCell(pos, cooling)`
- Get info: `FireManager.Instance.GetCell(pos)`

## 🎮 Current Features

### Fire Simulation
- [x] 3D grid-based spread
- [x] Heat transfer (conduction & convection)
- [x] Oxygen simulation with ventilation
- [x] Multiple fuel types
- [x] Ignition and extinguishing
- [x] Debug visualization

### World Building
- [x] Simple block placement
- [x] Room generator
- [x] Furniture placement
- [x] Fire integration
- [x] Visual fire feedback (blocks change color)

### Configuration
- [x] ScriptableObject-based settings
- [x] Adjustable grid size
- [x] Tunable fire spread rates
- [x] Material properties

## 🎯 Suggested Next Steps

### Phase 1: Core Gameplay (Week 1-2)
- [ ] First-person player controller
- [ ] Fire hose with water stream
- [ ] Handheld extinguisher
- [ ] Basic UI (health, oxygen, temperature)

### Phase 2: Visuals (Week 2-3)
- [ ] Particle effects for flames
- [ ] Smoke particle system
- [ ] Heat distortion shader
- [ ] Dynamic lighting from fire
- [ ] Sound effects (fire, extinguisher)

### Phase 3: Training Features (Week 3-4)
- [ ] Multiple scenario rooms
- [ ] Objectives and scoring
- [ ] Flashover mechanics
- [ ] Backdraft simulation
- [ ] Radio communication
- [ ] Performance feedback

### Phase 4: Polish (Week 4+)
- [ ] Replace blocks with proper models
- [ ] Improved materials and textures
- [ ] Advanced fire behaviors
- [ ] Team coordination mechanics
- [ ] Tutorial system

## 👥 Team Workflow

### Adding New Features

1. **Create a branch** for your feature
2. **Follow the existing structure**:
   - Managers for system-level controllers
   - Components for GameObject behavior
   - ScriptableObjects for data/config
3. **Use the fire API** through `FireManager.Instance`
4. **Test with GameSetup** component

### Working in Parallel

The system is designed for parallel work:

- **Fire team**: Extend fire simulation (smoke, damage, etc.)
- **World team**: Build scenarios, rooms, levels
- **Player team**: Character controller, tools, UI
- **Art team**: Replace visuals, add effects
- **Audio team**: Sound effects, music

Everyone uses the same fire API, so you won't step on each other!

### Code Style

Follow existing patterns:
- Namespace everything: `namespace FireFight.YourSystem`
- Document public methods with XML comments
- Use `[Header]` and `[Tooltip]` for Inspector fields
- Manager pattern for system controllers
- ScriptableObjects for configuration

## 📚 Documentation

| Document | Purpose |
|----------|---------|
| **QUICK_START.md** | Get running in 2 minutes |
| **FIRE_SYSTEM_GUIDE.md** | Complete fire API reference |
| **BLOCK_SYSTEM_GUIDE.md** | Level building guide |
| **IMPLEMENTATION_SUMMARY.md** | Architecture overview |
| **Assets/FireFight/README.md** | Component quick reference |

## 🐛 Common Issues

### Fire not working?
1. Check `FireManager` exists in scene
2. Verify `FireConfig` is assigned
3. Make sure cells have fuel: `SetCellFuel(pos, fuelType)`

### Blocks not showing?
1. Assign a material in `WorldBuilder`
2. Check block position is within grid bounds
3. Verify `CellBlock` component is present

### Performance issues?
1. Reduce grid size in `FireConfig`
2. Lower simulation tick rate
3. Disable debug gizmos in production

## 💡 Tips

### For Best Results:
1. **Match grid sizes**: `WorldBuilder.blockSize` = `FireConfig.cellSize`
2. **Use ventilation**: Open rooms keep fires burning, closed rooms suffocate them
3. **Test early**: Enable "Start Fire On Begin" to immediately see results
4. **Debug visually**: Enable "Show Debug Gizmos" to see fire grid

### Performance:
- Default settings (20x10x20 grid) run smoothly
- Can scale to 50x20x50 for larger buildings
- Only active fires consume significant CPU

### Extending:
- Create new `BlockType` enums as needed
- Add custom `FuelType` assets for special materials
- Extend `CellBlock` for interactive objects
- Add new managers for smoke, water, damage, etc.

## 🎓 Learning Resources

### Understanding the Code:
1. Start with `FireManager.cs` - Main entry point
2. Look at `FireCell.cs` - Data structure
3. Read `CellBlock.cs` - How blocks integrate with fire
4. Check `GameSetup.cs` - See how everything connects

### Making Changes:
1. **Add fuel type**: Create new `FuelType` asset
2. **Add block type**: Extend `BlockType` enum in `CellBlock.cs`
3. **Add room**: Use `WorldBuilder.CreateBlock()` in custom script
4. **Add fire behavior**: Extend or create new manager

## 🤝 Getting Help

If stuck:
1. Check the relevant documentation file
2. Look at code comments (everything is documented)
3. Enable debug logging (check console)
4. Use debug visualization (Scene view gizmos)

## 🎉 What You Have

A **production-ready foundation** for a firefighter training simulator:

✅ Realistic fire physics  
✅ Easy level creation  
✅ Extensible architecture  
✅ Full documentation  
✅ Demo included  

Now go build something awesome! 🔥🚒

---

**Questions?** Check the documentation or dive into the code - it's all commented!
