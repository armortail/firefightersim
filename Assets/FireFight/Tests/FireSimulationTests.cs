using NUnit.Framework;
using UnityEngine;
using FireFight.Environment;

namespace FireFight.Tests
{
    /// <summary>
    /// Edit Mode unit tests for the fire spread logic. These run WITHOUT entering
    /// Play mode because FireGrid and FireSim are plain C# classes.
    ///
    /// Open: Window > General > Test Runner > EditMode > Run All.
    /// </summary>
    public class FireSimulationTests
    {
        // Helper: a fresh grid + sim with predictable settings.
        private static (FireGrid grid, FireSim sim, FireConfig cfg) MakeSim(
            Vector3Int size, System.Action<FireConfig> tune = null)
        {
            var cfg = new FireConfig
            {
                heatPerTick = 0.5f,
                ignitionThreshold = 1f,
                spreadChance = 1f,   // deterministic: always ignite when hot enough
                burnRate = 0.1f,
                coolingRate = 0.1f,
            };
            tune?.Invoke(cfg);

            var grid = new FireGrid(size, 1f, Vector3.zero);
            var sim = new FireSim(grid, cfg);
            return (grid, sim, cfg);
        }

        private static void Ignite(FireGrid grid, Vector3Int pos, FireConfig cfg)
        {
            var cell = grid.GetCell(pos);
            cell.state = FireState.Burning;
            cell.heat = cfg.ignitionThreshold;
        }

        private static int CountBurning(FireGrid grid)
        {
            int n = 0;
            foreach (var c in grid.Cells) if (c.state == FireState.Burning) n++;
            return n;
        }

        [Test]
        public void NewGrid_AllCellsUnburned()
        {
            var (grid, _, _) = MakeSim(new Vector3Int(5, 1, 5));
            foreach (var cell in grid.Cells)
                Assert.AreEqual(FireState.Unburned, cell.state);
        }

        [Test]
        public void Tick_DoesNotIgniteAnything_WhenNoFirePresent()
        {
            var (grid, sim, _) = MakeSim(new Vector3Int(5, 1, 5));
            sim.Tick();
            Assert.AreEqual(0, CountBurning(grid));
        }

        [Test]
        public void Fire_DoesNotSpread_Instantly_OnFirstTick_BeyondImmediateNeighbours()
        {
            // THE KEY BEHAVIOUR: lighting one cell must not engulf the whole grid.
            var (grid, sim, cfg) = MakeSim(new Vector3Int(9, 1, 9));
            Vector3Int centre = new Vector3Int(4, 0, 4);
            Ignite(grid, centre, cfg);

            sim.Tick();

            // A cell two steps away cannot possibly be burning after one tick.
            var far = grid.GetCell(new Vector3Int(4, 0, 6));
            Assert.AreEqual(FireState.Unburned, far.state,
                "Fire reached a far cell in a single tick - it is spreading instantly.");
        }

        [Test]
        public void Fire_Spreads_ToNeighbour_OverTime()
        {
            // heatPerTick (0.5) < threshold (1.0), so a neighbour needs ~2 ticks.
            var (grid, sim, cfg) = MakeSim(new Vector3Int(5, 1, 5));
            Vector3Int centre = new Vector3Int(2, 0, 2);
            Ignite(grid, centre, cfg);

            Vector3Int neighbour = new Vector3Int(3, 0, 2);
            Assert.AreEqual(FireState.Unburned, grid.GetCell(neighbour).state);

            // Run enough ticks for heat to build past the threshold.
            for (int i = 0; i < 5; i++) sim.Tick();

            Assert.AreEqual(FireState.Burning, grid.GetCell(neighbour).state,
                "Neighbour should have caught fire after several ticks.");
        }

        [Test]
        public void BurningCell_EventuallyBurnsOut()
        {
            var (grid, sim, cfg) = MakeSim(new Vector3Int(3, 1, 3),
                c => c.burnRate = 0.25f); // burns out in ~4 ticks
            Vector3Int centre = new Vector3Int(1, 0, 1);
            Ignite(grid, centre, cfg);

            for (int i = 0; i < 10; i++) sim.Tick();

            Assert.AreEqual(FireState.Burned, grid.GetCell(centre).state,
                "A burning cell should become Burned once its fuel is gone.");
        }

        [Test]
        public void CellWithNoFuel_NeverIgnites()
        {
            var (grid, sim, cfg) = MakeSim(new Vector3Int(3, 1, 3));
            Vector3Int centre = new Vector3Int(1, 0, 1);
            Ignite(grid, centre, cfg);

            // Drain a neighbour's fuel entirely.
            Vector3Int neighbour = new Vector3Int(2, 0, 1);
            grid.GetCell(neighbour).fuel = 0f;

            for (int i = 0; i < 10; i++) sim.Tick();

            Assert.AreNotEqual(FireState.Burning, grid.GetCell(neighbour).state,
                "A cell with no fuel must never catch fire.");
        }

        [Test]
        public void SpreadChanceZero_FireNeverSpreads()
        {
            var (grid, sim, cfg) = MakeSim(new Vector3Int(5, 1, 5),
                c => c.spreadChance = 0f);
            Vector3Int centre = new Vector3Int(2, 0, 2);
            Ignite(grid, centre, cfg);

            for (int i = 0; i < 10; i++) sim.Tick();

            // Only the originally-lit cell should ever have been burning.
            foreach (var cell in grid.Cells)
            {
                if (cell.gridPosition == centre) continue;
                Assert.AreEqual(FireState.Unburned, cell.state,
                    "With spreadChance 0, no neighbour should ever ignite.");
            }
        }

        [Test]
        public void Fire_DoesNotSpread_AboveMaxFireLayer()
        {
            // Fire allowed only up to layer 1. A burning cell at y=1 should never
            // ignite the cell at y=2, even though heat reaches it.
            var (grid, sim, cfg) = MakeSim(new Vector3Int(3, 4, 3),
                c => c.maxFireLayer = 1);
            Ignite(grid, new Vector3Int(1, 1, 1), cfg);

            for (int i = 0; i < 20; i++) sim.Tick();

            var above = grid.GetCell(new Vector3Int(1, 2, 1));
            Assert.AreNotEqual(FireState.Burning, above.state,
                "Fire should never ignite a cell above maxFireLayer.");
        }

        [Test]
        public void WorldToGrid_And_GridToWorld_RoundTrip()
        {
            var grid = new FireGrid(new Vector3Int(10, 1, 10), 2f, new Vector3(5, 0, 5));
            Vector3Int original = new Vector3Int(3, 0, 7);
            Vector3 world = grid.GridToWorld(original);
            Vector3Int back = grid.WorldToGrid(world);
            Assert.AreEqual(original, back);
        }
    }
}
