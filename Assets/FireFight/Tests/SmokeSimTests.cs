using NUnit.Framework;
using UnityEngine;
using FireFight.Environment;

namespace FireFight.Tests
{
    /// <summary>
    /// Edit Mode unit tests for the smoke logic. Run via
    /// Window > General > Test Runner > EditMode.
    /// </summary>
    public class SmokeSimTests
    {
        private static (FireGrid grid, SmokeSim sim, SmokeConfig cfg) MakeSim(
            Vector3Int size, System.Action<SmokeConfig> tune = null)
        {
            var cfg = new SmokeConfig
            {
                smokePerBurningTick = 0.25f,
                smokePerSmolderingTick = 0.05f,
                riseRate = 0.5f,
                spreadRate = 0.1f,
                dissipation = 0f,   // off by default so movement tests are clean
                minSmoke = 0.0001f,
            };
            tune?.Invoke(cfg);

            var grid = new FireGrid(size, 1f, Vector3.zero);
            var sim = new SmokeSim(grid, cfg);
            return (grid, sim, cfg);
        }

        private static float TotalSmoke(FireGrid grid)
        {
            float t = 0f;
            foreach (var c in grid.Cells) t += c.smoke;
            return t;
        }

        [Test]
        public void BurningCell_ProducesSmoke()
        {
            var (grid, sim, cfg) = MakeSim(new Vector3Int(3, 3, 3));
            var start = new Vector3Int(1, 0, 1);
            grid.GetCell(start).state = FireState.Burning;

            sim.Tick();

            Assert.Greater(TotalSmoke(grid), 0f, "A burning cell should create smoke.");
        }

        [Test]
        public void NoFire_ProducesNoSmoke()
        {
            var (grid, sim, _) = MakeSim(new Vector3Int(3, 3, 3));
            sim.Tick();
            Assert.AreEqual(0f, TotalSmoke(grid), "No fire means no smoke.");
        }

        [Test]
        public void Smoke_RisesUpward()
        {
            // Seed smoke in a bottom cell, no fire, and watch it move up.
            var (grid, sim, _) = MakeSim(new Vector3Int(1, 4, 1));
            var bottom = new Vector3Int(0, 0, 0);
            var above = new Vector3Int(0, 1, 0);
            grid.GetCell(bottom).smoke = 1f;

            sim.Tick();

            Assert.Greater(grid.GetCell(above).smoke, 0f,
                "Smoke should move into the cell above.");
            Assert.Less(grid.GetCell(bottom).smoke, 1f,
                "The source cell should have given some smoke upward.");
        }

        [Test]
        public void Smoke_BanksUnderCeiling_SpreadsSideways()
        {
            // Smoke already at the top layer cannot rise, so it must spread.
            var (grid, sim, _) = MakeSim(new Vector3Int(3, 1, 3)); // single layer = everything is "ceiling"
            var centre = new Vector3Int(1, 0, 1);
            grid.GetCell(centre).smoke = 1f;

            sim.Tick();

            var side = grid.GetCell(new Vector3Int(2, 0, 1));
            Assert.Greater(side.smoke, 0f,
                "Smoke under a ceiling should spread to horizontal neighbours.");
        }

        [Test]
        public void Dissipation_ReducesSmokeOverTime()
        {
            var (grid, sim, cfg) = MakeSim(new Vector3Int(1, 1, 1),
                c => c.dissipation = 0.1f);
            grid.GetCell(new Vector3Int(0, 0, 0)).smoke = 0.5f;

            float before = TotalSmoke(grid);
            sim.Tick();
            float after = TotalSmoke(grid);

            Assert.Less(after, before, "Dissipation should reduce total smoke.");
        }

        [Test]
        public void Smoke_IsClampedToOne()
        {
            var (grid, sim, cfg) = MakeSim(new Vector3Int(1, 1, 1),
                c => c.smokePerBurningTick = 2f); // absurd production
            var cell = grid.GetCell(new Vector3Int(0, 0, 0));
            cell.state = FireState.Burning;

            sim.Tick();

            Assert.LessOrEqual(cell.smoke, 1f, "Smoke density must never exceed 1.");
        }

        [Test]
        public void Smoke_DoesNotRise_AboveMaxSmokeLayer()
        {
            // Cap smoke at layer 1 in a 4-tall grid. Seed smoke at the bottom and
            // run many ticks: layer 2 and above must stay empty.
            var (grid, sim, cfg) = MakeSim(new Vector3Int(1, 4, 1),
                c => c.maxSmokeLayer = 1);
            grid.GetCell(new Vector3Int(0, 0, 0)).smoke = 1f;

            for (int i = 0; i < 30; i++) sim.Tick();

            Assert.AreEqual(0f, grid.GetCell(new Vector3Int(0, 2, 0)).smoke,
                "Smoke should never rise above maxSmokeLayer.");
            Assert.AreEqual(0f, grid.GetCell(new Vector3Int(0, 3, 0)).smoke,
                "Smoke should never rise above maxSmokeLayer.");
        }

        [Test]
        public void Movement_ConservesSmoke_WhenNoDissipation()
        {
            // With dissipation off and no fire, moving smoke should not create
            // or destroy it (within floating-point tolerance).
            var (grid, sim, _) = MakeSim(new Vector3Int(3, 3, 3));
            grid.GetCell(new Vector3Int(1, 0, 1)).smoke = 1f;

            float before = TotalSmoke(grid);
            for (int i = 0; i < 5; i++) sim.Tick();
            float after = TotalSmoke(grid);

            Assert.AreEqual(before, after, 0.01f,
                "Smoke should be conserved during movement when nothing dissipates.");
        }
    }
}
