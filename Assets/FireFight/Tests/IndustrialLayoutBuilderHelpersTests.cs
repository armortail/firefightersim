using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using FireFight.EditorTools;

namespace FireFight.Tests
{
    /// <summary>
    /// Unit tests for the builder's pure static seams: the central-structure picker and the
    /// fire-grid sizer. The grid-coverage invariant is the regression guard for the earlier
    /// "[8,48] clamp at cellSize=1 could leave a big structure uncovered" bug.
    /// </summary>
    public class IndustrialLayoutBuilderHelpersTests
    {
        private static GameObject Named(string n)
        {
            var go = new GameObject(n);
            return go;
        }

        [TearDown]
        public void Cleanup()
        {
            // GameObjects created in tests are destroyed so they don't leak into the scene.
            foreach (var go in Object.FindObjectsByType<GameObject>(FindObjectsSortMode.None))
                if (go.name.StartsWith("TEST_")) Object.DestroyImmediate(go);
        }

        // ---- Grid coverage invariant ----

        private static void AssertCoversStructurePlusRing(Vector3 size)
        {
            var (grid, cellSize) = IndustrialLayoutBuilder.ComputeFireGrid(size);

            float needX = size.x + 2f * IndustrialLayoutBuilder.CLEARANCE;
            float needZ = size.z + 2f * IndustrialLayoutBuilder.CLEARANCE;

            Assert.GreaterOrEqual(grid.x * cellSize, needX - 1e-3f,
                $"Grid X ({grid.x}*{cellSize}) must cover structure+ring ({needX}).");
            Assert.GreaterOrEqual(grid.z * cellSize, needZ - 1e-3f,
                $"Grid Z ({grid.z}*{cellSize}) must cover structure+ring ({needZ}).");

            Assert.GreaterOrEqual(grid.x, 8);
            Assert.LessOrEqual(grid.x, IndustrialLayoutBuilder.MAX_CELLS);
            Assert.GreaterOrEqual(grid.z, 8);
            Assert.LessOrEqual(grid.z, IndustrialLayoutBuilder.MAX_CELLS);
            Assert.AreEqual(6, grid.y, "Grid Y should stay 6 so smoke can rise.");
        }

        [Test] public void Grid_CoversSmallStructure() => AssertCoversStructurePlusRing(new Vector3(4f, 3f, 4f));
        [Test] public void Grid_CoversMediumStructure() => AssertCoversStructurePlusRing(new Vector3(15f, 6f, 15f));
        [Test] public void Grid_CoversLargeHangar() => AssertCoversStructurePlusRing(new Vector3(40f, 12f, 36f));
        [Test] public void Grid_CoversVeryLargeHangar() => AssertCoversStructurePlusRing(new Vector3(60f, 15f, 20f));

        [Test]
        public void Grid_DegenerateStructure_StillClampsToMinimum()
        {
            var (grid, cellSize) = IndustrialLayoutBuilder.ComputeFireGrid(new Vector3(0.01f, 0.01f, 0.01f));
            Assert.AreEqual(8, grid.x);
            Assert.AreEqual(8, grid.z);
            Assert.AreEqual(1f, cellSize, 1e-3f);
        }

        // ---- Picker ----

        [Test]
        public void Picker_PrefersFullNamedPrefab()
        {
            var small = Named("TEST_Hangar_v1_full");
            var huge = Named("TEST_Hangar_v3");
            var candidates = new List<(GameObject, Vector3)>
            {
                (huge, new Vector3(50f, 10f, 50f)),
                (small, new Vector3(10f, 5f, 10f)),
            };
            var chosen = IndustrialLayoutBuilder.PickCentralPrefab(candidates);
            Assert.AreSame(small, chosen, "A '_full'-named prefab should win over a larger one.");
        }

        [Test]
        public void Picker_FallsBackToLargestFootprint()
        {
            var a = Named("TEST_Hangar_v2");
            var b = Named("TEST_Hangar_v3");
            var candidates = new List<(GameObject, Vector3)>
            {
                (a, new Vector3(10f, 5f, 10f)),
                (b, new Vector3(30f, 8f, 25f)),
            };
            var chosen = IndustrialLayoutBuilder.PickCentralPrefab(candidates);
            Assert.AreSame(b, chosen, "Without a '_full' name, the largest X*Z footprint should win.");
        }

        [Test]
        public void Picker_EmptyList_ReturnsNull()
        {
            Assert.IsNull(IndustrialLayoutBuilder.PickCentralPrefab(new List<(GameObject, Vector3)>()));
            Assert.IsNull(IndustrialLayoutBuilder.PickCentralPrefab(null));
        }
    }
}
