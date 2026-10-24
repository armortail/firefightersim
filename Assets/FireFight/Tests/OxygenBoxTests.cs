using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using FireFight.Environment;

namespace FireFight.Tests
{
    /// <summary>
    /// Edit Mode tests for confined-space oxygen (OxygenBox) behaviour:
    /// a sealed room suffocates fire, and opening it lets fire flare back up.
    /// </summary>
    public class OxygenBoxTests
    {
        private static FireConfig MakeFireConfig()
        {
            return new FireConfig
            {
                heatPerTick = 0.5f,
                ignitionThreshold = 1f,
                spreadChance = 1f,
                burnRate = 0.01f,            // slow burn so suffocation is the cause of death
                coolingRate = 0.1f,
                upwardHeatMultiplier = 1f,
                downwardHeatMultiplier = 1f,
            };
        }

        private static OxygenConfig MakeOxygenConfig()
        {
            return new OxygenConfig
            {
                consumptionPerBurningCell = 0.25f, // drains fast for a short test
                replenishRate = 0.25f,
                sustainThreshold = 0.15f,
            };
        }

        [Test]
        public void Box_NormalisesMinMax_RegardlessOfOrder()
        {
            var box = new OxygenBox(new Vector3Int(5, 5, 5), new Vector3Int(1, 1, 1), MakeOxygenConfig());
            Assert.IsTrue(box.Contains(new Vector3Int(3, 3, 3)));
            Assert.AreEqual(new Vector3Int(1, 1, 1), box.Min);
            Assert.AreEqual(new Vector3Int(5, 5, 5), box.Max);
        }

        [Test]
        public void SealedBox_SuffocatesFire()
        {
            var grid = new FireGrid(new Vector3Int(3, 1, 3), 1f, Vector3.zero);
            foreach (var c in grid.Cells) c.fuel = 1f;

            // Box covers the whole grid, starts SEALED.
            var box = new OxygenBox(new Vector3Int(0, 0, 0), new Vector3Int(2, 0, 2),
                MakeOxygenConfig(), startOpen: false);
            var boxes = new List<OxygenBox> { box };

            var sim = new FireSim(grid, MakeFireConfig(), boxes);
            grid.GetCell(new Vector3Int(1, 0, 1)).state = FireState.Burning;

            // Run long enough for oxygen to drain below the sustain threshold.
            for (int i = 0; i < 20; i++) sim.Tick();

            Assert.Less(box.Oxygen, MakeOxygenConfig().sustainThreshold,
                "A sealed box with fire should run low on oxygen.");
            Assert.AreEqual(0, CountBurning(grid),
                "Fire should suffocate (stop burning) in a sealed, airless box.");
        }

        [Test]
        public void OpeningBox_LetsFireReignite_Backdraft()
        {
            var grid = new FireGrid(new Vector3Int(3, 1, 3), 1f, Vector3.zero);
            foreach (var c in grid.Cells) c.fuel = 1f;

            var box = new OxygenBox(new Vector3Int(0, 0, 0), new Vector3Int(2, 0, 2),
                MakeOxygenConfig(), startOpen: false);
            var boxes = new List<OxygenBox> { box };

            var sim = new FireSim(grid, MakeFireConfig(), boxes);
            grid.GetCell(new Vector3Int(1, 0, 1)).state = FireState.Burning;

            // Suffocate it first.
            for (int i = 0; i < 20; i++) sim.Tick();
            Assert.AreEqual(0, CountBurning(grid), "Precondition: fire is out.");

            // Open the box: fresh air returns and the still-hot, fuelled cell relights.
            box.SetOpen(true);
            // Re-light the seed (in a real scene embers / a neighbour would do this);
            // here we confirm the box now PERMITS burning again.
            grid.GetCell(new Vector3Int(1, 0, 1)).state = FireState.Burning;
            grid.GetCell(new Vector3Int(1, 0, 1)).heat = 1f;

            for (int i = 0; i < 3; i++) sim.Tick();

            Assert.Greater(box.Oxygen, 0f, "An open box should breathe air back in.");
            Assert.Greater(CountBurning(grid), 0,
                "With the box open and air restored, fire should burn again.");
        }

        [Test]
        public void NoBoxes_FireUnaffected()
        {
            // Sanity: passing null/empty boxes keeps the original behaviour.
            var grid = new FireGrid(new Vector3Int(3, 1, 3), 1f, Vector3.zero);
            foreach (var c in grid.Cells) c.fuel = 1f;

            var sim = new FireSim(grid, MakeFireConfig(), new List<OxygenBox>());
            grid.GetCell(new Vector3Int(1, 0, 1)).state = FireState.Burning;

            for (int i = 0; i < 5; i++) sim.Tick();

            Assert.Greater(CountBurning(grid), 0,
                "Without oxygen boxes, fire should spread normally (no suffocation).");
        }

        private static int CountBurning(FireGrid grid)
        {
            int n = 0;
            foreach (var c in grid.Cells) if (c.state == FireState.Burning) n++;
            return n;
        }
    }
}
