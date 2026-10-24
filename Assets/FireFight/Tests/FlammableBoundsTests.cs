using NUnit.Framework;
using UnityEngine;
using FireFight.Environment;

namespace FireFight.Tests
{
    /// <summary>
    /// Tests the bounds -> grid-cell translation that backs the Flammable bridge.
    /// These exercise the same math FireManager.ForEachCellInBounds uses, against
    /// a plain FireGrid (no MonoBehaviour needed).
    /// </summary>
    public class FlammableBoundsTests
    {
        // Mirror of the manager's bounds iteration, kept here so the math is
        // testable without constructing a MonoBehaviour.
        private static void ForEachCellInBounds(FireGrid grid, Bounds b, System.Action<FireCell> action)
        {
            Vector3Int min = grid.WorldToGrid(b.min);
            Vector3Int max = grid.WorldToGrid(b.max);
            for (int x = min.x; x <= max.x; x++)
                for (int y = min.y; y <= max.y; y++)
                    for (int z = min.z; z <= max.z; z++)
                    {
                        var c = grid.GetCell(new Vector3Int(x, y, z));
                        if (c != null) action(c);
                    }
        }

        [Test]
        public void Bounds_MapsToAtLeastOneCell_WhenInsideGrid()
        {
            var grid = new FireGrid(new Vector3Int(10, 4, 10), 1f, Vector3.zero);
            // A 1m box at the grid centre (origin) should hit at least one cell.
            var b = new Bounds(Vector3.zero, Vector3.one * 0.5f);

            int hit = 0;
            ForEachCellInBounds(grid, b, _ => hit++);

            Assert.GreaterOrEqual(hit, 1, "A box inside the grid should map to >=1 cell.");
        }

        [Test]
        public void Bounds_PaintsFuel_IntoOverlappingCells()
        {
            var grid = new FireGrid(new Vector3Int(10, 4, 10), 1f, Vector3.zero);
            // Start all cells with no fuel (mimics fillWithFuel = false).
            foreach (var c in grid.Cells) c.fuel = 0f;

            var b = new Bounds(Vector3.zero, Vector3.one * 2f);
            ForEachCellInBounds(grid, b, c => c.fuel = 1f);

            int fuelled = 0;
            foreach (var c in grid.Cells) if (c.fuel > 0f) fuelled++;
            Assert.Greater(fuelled, 0, "Painting fuel in bounds should fuel some cells.");
        }

        [Test]
        public void Bounds_OutsideGrid_MapsToNoCells()
        {
            var grid = new FireGrid(new Vector3Int(4, 2, 4), 1f, Vector3.zero);
            // Far away from the (small) grid: should touch nothing.
            var b = new Bounds(new Vector3(1000f, 0f, 1000f), Vector3.one);

            int hit = 0;
            ForEachCellInBounds(grid, b, _ => hit++);

            Assert.AreEqual(0, hit, "An object outside the grid should map to no cells.");
        }

        [Test]
        public void IsAnyBurning_TrueOnlyWhenACellInBoundsBurns()
        {
            var grid = new FireGrid(new Vector3Int(6, 2, 6), 1f, Vector3.zero);
            var b = new Bounds(Vector3.zero, Vector3.one * 1.5f);

            bool before = false;
            ForEachCellInBounds(grid, b, c => { if (c.state == FireState.Burning) before = true; });
            Assert.IsFalse(before, "Nothing should be burning initially.");

            // Light one cell inside the bounds.
            ForEachCellInBounds(grid, b, c => c.state = FireState.Burning);

            bool after = false;
            ForEachCellInBounds(grid, b, c => { if (c.state == FireState.Burning) after = true; });
            Assert.IsTrue(after, "A burning cell inside the bounds should be detected.");
        }
    }
}
