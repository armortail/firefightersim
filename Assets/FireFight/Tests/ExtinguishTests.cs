using NUnit.Framework;
using UnityEngine;
using FireFight.Environment;

namespace FireFight.Tests
{
    /// <summary>
    /// Edit Mode tests for water / wetness extinguishing: wet cells get smothered,
    /// resist re-igniting, and dry out over time so they can burn again.
    /// </summary>
    public class ExtinguishTests
    {
        private static FireConfig MakeConfig()
        {
            return new FireConfig
            {
                heatPerTick = 0.5f,
                ignitionThreshold = 1f,
                spreadChance = 1f,
                burnRate = 0.02f,
                coolingRate = 0.1f,
                evaporationRate = 0.02f,
                wetExtinguishThreshold = 0.5f,
            };
        }

        private static int CountBurning(FireGrid grid)
        {
            int n = 0;
            foreach (var c in grid.Cells) if (c.state == FireState.Burning) n++;
            return n;
        }

        [Test]
        public void EffectiveFlammability_DropsWithWetness()
        {
            var cell = new FireCell(Vector3Int.zero) { flammability = 0.8f, wetness = 0f };
            Assert.AreEqual(0.8f, cell.EffectiveFlammability, 1e-4f);

            cell.wetness = 0.5f;
            Assert.AreEqual(0.4f, cell.EffectiveFlammability, 1e-4f, "Half wet => half flammability.");

            cell.wetness = 1f;
            Assert.AreEqual(0f, cell.EffectiveFlammability, 1e-4f, "Fully wet => cannot catch.");
        }

        [Test]
        public void WetBurningCell_IsSmothered()
        {
            var cfg = MakeConfig();
            var grid = new FireGrid(new Vector3Int(1, 1, 1), 1f, Vector3.zero);
            var cell = grid.GetCell(Vector3Int.zero);
            cell.fuel = 1f;
            cell.state = FireState.Burning;
            cell.wetness = 0.6f; // above wetExtinguishThreshold (0.5)

            var sim = new FireSim(grid, cfg);
            sim.Tick();

            Assert.AreEqual(FireState.Unburned, cell.state,
                "A burning cell wetter than the threshold should be put out.");
            Assert.Greater(cell.fuel, 0f, "Smothered cell keeps its fuel (can relight once dry).");
        }

        [Test]
        public void WetCell_ResistsReignition()
        {
            // A fully wet, fuelled cell being heated by a burning neighbour should
            // NOT ignite while it stays wet.
            var cfg = MakeConfig();
            var grid = new FireGrid(new Vector3Int(2, 1, 1), 1f, Vector3.zero);
            foreach (var c in grid.Cells) c.fuel = 1f;

            grid.GetCell(new Vector3Int(0, 0, 0)).state = FireState.Burning;
            var target = grid.GetCell(new Vector3Int(1, 0, 0));
            target.wetness = 1f; // soaked

            var sim = new FireSim(grid, cfg);
            // Few ticks: evaporation (0.02/tick) barely dries it, so it stays wet.
            for (int i = 0; i < 3; i++) sim.Tick();

            Assert.AreEqual(FireState.Unburned, target.state,
                "A soaked cell should resist igniting while still wet.");
        }

        [Test]
        public void WetnessEvaporates_OverTime()
        {
            var cfg = MakeConfig();
            var grid = new FireGrid(new Vector3Int(1, 1, 1), 1f, Vector3.zero);
            var cell = grid.GetCell(Vector3Int.zero);
            cell.wetness = 0.2f;

            var sim = new FireSim(grid, cfg);
            float before = cell.wetness;
            sim.Tick();

            Assert.Less(cell.wetness, before, "Wetness should evaporate each tick.");
        }

        [Test]
        public void DriedCell_CanBurnAgain()
        {
            // Lightly wet cell dries out, then a burning neighbour relights it.
            var cfg = MakeConfig();
            cfg.evaporationRate = 0.5f; // dries fast for a short test
            var grid = new FireGrid(new Vector3Int(2, 1, 1), 1f, Vector3.zero);
            foreach (var c in grid.Cells) c.fuel = 1f;

            grid.GetCell(new Vector3Int(0, 0, 0)).state = FireState.Burning;
            var target = grid.GetCell(new Vector3Int(1, 0, 0));
            target.wetness = 0.4f; // below smother threshold, dries in ~1 tick

            var sim = new FireSim(grid, cfg);
            for (int i = 0; i < 10; i++) sim.Tick();

            Assert.AreEqual(FireState.Burning, target.state,
                "Once dry, the cell should catch fire from its burning neighbour.");
        }
    }
}
