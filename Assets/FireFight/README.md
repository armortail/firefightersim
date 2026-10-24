# Fire Simulation System

A complete, realistic cell-based fire simulation system for Unity firefighter training.

## ✨ Features

- **3D Cell-Based Grid** - Voxel simulation with configurable resolution
- **Realistic Fire Physics**
  - Heat transfer (conduction, convection)
  - Oxygen simulation with ventilation
  - Fuel types with different burn characteristics
  - Fire spread based on temperature, oxygen, and flammability
- **Manager Architecture** - Clean, modular, extensible design
- **Scriptable Objects** - Easy configuration and fuel type creation
- **Debug Visualization** - See fire spread in Scene view

## 🚀 Quick Start

1. **Open Scene**: `Assets/FireFight/Scenes/SampleScene.unity`

2. **Create Fire System**:
   - Create Empty GameObject → Name it "FireSystem"
   - Add Component → `FireManager`
   - Assign `DefaultFireConfig` from Resources folder

3. **Add Demo** (Optional):
   - Add Component → `FireDemo` to same object
   - Enable "Ignite On Start"
   - Press Play!

4. **Controls**:
   - **Space**: Ignite fire
   - **E**: Extinguish all
   - **I**: Show info in console

## 📁 File Structure

```
FireFight/
├── Resources/
│   ├── DefaultFireConfig.asset     # Main configuration
│   └── FuelTypes/                  # Fuel type definitions
│       ├── Wood.asset
│       ├── Paper.asset
│       ├── Fabric.asset
│       └── Plastic.asset
└── Scripts/
    ├── Managers/                   # System controllers
    │   ├── FireManager.cs          # Main manager
    │   ├── HeatManager.cs          # Heat simulation
    │   └── FuelManager.cs          # Fuel utilities
    └── Fire/
        ├── Data/
        │   ├── FireCell.cs         # Cell data
        │   └── FuelType.cs         # Fuel properties
        ├── Config/
        │   └── FireConfig.cs       # Configuration
        ├── Utils/
        │   └── FireConstants.cs    # Constants
        └── FireDemo.cs             # Demo script
```

## 📖 Documentation

See **FIRE_SYSTEM_GUIDE.md** in the project root for:
- Detailed architecture explanation
- Code examples
- API reference
- Performance tips
- How to extend the system

## 🔥 How Fire Works

1. **Ignition**: Cell needs temperature ≥ ignition point + fuel + oxygen
2. **Burning**: Consumes fuel & oxygen, generates heat
3. **Spread**: Heat transfers to neighbors, potentially igniting them
4. **Extinguish**: Runs out of fuel, oxygen, or is cooled down

## 🎮 Using in Code

```csharp
using FireFight.Fire;

// Get manager
FireManager fire = FireManager.Instance;

// Set fuel
Vector3Int pos = new Vector3Int(10, 0, 10);
FuelType wood = Resources.Load<FuelType>("FuelTypes/Wood");
fire.SetCellFuel(pos, wood);

// Ignite
fire.IgniteCell(pos);

// Extinguish (e.g., water)
fire.ExtinguishCell(pos, coolingAmount: 500f);

// Get info
FireCell cell = fire.GetCell(pos);
Debug.Log($"Temp: {cell.temperature}°C, Burning: {cell.state}");
```

## ⚙️ Configuration

Edit `FireConfig` asset to tune:
- Grid size and cell size
- Simulation speed (ticks per second)
- Heat transfer rate
- Fire spread probability
- Cooling rate
- Oxygen replenishment

## 🎨 Customization

### Create New Fuel Types

1. Right-click → `Create > FireFight > Fuel Type`
2. Configure properties:
   - Ignition temperature
   - Flammability
   - Burn rate
   - Heat output
   - Smoke production

### Extend Managers

All managers are extensible MonoBehaviours. Add your own:
- `SmokeManager` for smoke simulation
- `WaterManager` for water physics
- `DamageManager` for structural damage

## 🏗️ Next Steps

Recommended additions for full training sim:
- [ ] Particle effects (flames, smoke)
- [ ] Audio (crackling, roaring)
- [ ] Player firefighter controller
- [ ] Water hose & extinguisher tools
- [ ] Flashover & backdraft events
- [ ] Smoke visibility reduction
- [ ] Training scenarios & scoring

## 📝 License

Part of the FireFighter Sim hackathon project.
