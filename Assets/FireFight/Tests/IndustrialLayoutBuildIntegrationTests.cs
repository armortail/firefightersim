using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using FireFight.EditorTools;
using FireFight.Managers;

namespace FireFight.Tests
{
    /// <summary>
    /// Integration test: run the generator and assert the SAVED scene satisfies the size-up
    /// requirements (free-standing central structure with clearance, fire wired + tagged,
    /// play-mode highlight, player + camera, baked NavMesh, deterministic seed igniter).
    /// Heavier than the unit suite (it opens a scene), so it lives in the EditMode test asmdef.
    /// </summary>
    public class IndustrialLayoutBuildIntegrationTests
    {
        private const float CLEARANCE = 6f; // mirror of IndustrialLayoutBuilder.CLEARANCE

        [Test]
        public void BuildBatch_ProducesFunctionalSizeUpScene()
        {
            IndustrialLayoutBuilder.BuildBatch();

            // Exactly one FireManager.
            var managers = Object.FindObjectsByType<FireManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(1, managers.Length, "Expected exactly one FireManager.");
            var mgr = managers[0];

            // Flammables tagged in the saved scene.
            var flammables = Object.FindObjectsByType<Flammable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.GreaterOrEqual(flammables.Length, 1, "Expected at least one Flammable.");

            // Play-mode highlight present.
            var highlights = Object.FindObjectsByType<FlammableHighlight>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.GreaterOrEqual(highlights.Length, 1, "Expected at least one FlammableHighlight.");

            // Player tagged Player with a FirefighterMovement whose camera is wired.
            var players = GameObject.FindGameObjectsWithTag("Player");
            Assert.GreaterOrEqual(players.Length, 1, "Expected a GameObject tagged 'Player'.");
            var player = players[0];
            var mover = player.GetComponent("FirefighterMovement") as MonoBehaviour;
            Assert.IsNotNull(mover, "Player should have a FirefighterMovement.");
            var camField = mover.GetType().GetField("playerCamera",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            Assert.IsNotNull(camField, "FirefighterMovement should expose playerCamera.");
            Assert.IsNotNull(camField.GetValue(mover), "playerCamera should be wired.");

            // Exactly one SeedIgniter with non-null manager (seed may be null only if no
            // flammable was tagged, but we asserted >=1 above, so seed should be set).
            var igniters = Object.FindObjectsByType<SeedIgniter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.AreEqual(1, igniters.Length, "Expected exactly one SeedIgniter.");
            Assert.IsNotNull(igniters[0].fireManager, "SeedIgniter.fireManager should be wired.");
            Assert.IsNotNull(igniters[0].seed, "SeedIgniter.seed should be wired (flammables exist).");

            // Grid covers the structure + clearance ring.
            var structure = GameObject.Find("CentralStructure");
            Assert.IsNotNull(structure, "Expected a CentralStructure object.");
            Bounds sb = CombinedRendererBounds(structure);
            Assert.GreaterOrEqual(mgr.gridSize.x * mgr.cellSize, sb.size.x + 2f * CLEARANCE - 1e-2f,
                "Fire grid X must cover structure + clearance ring.");
            Assert.GreaterOrEqual(mgr.gridSize.z * mgr.cellSize, sb.size.z + 2f * CLEARANCE - 1e-2f,
                "Fire grid Z must cover structure + clearance ring.");

            // Central structure geometry centred on origin X/Z and seated at y=0.
            Assert.Less(Mathf.Abs(sb.center.x), 0.5f, "Structure should be centred on X=0.");
            Assert.Less(Mathf.Abs(sb.center.z), 0.5f, "Structure should be centred on Z=0.");
            Assert.Less(Mathf.Abs(sb.min.y), 0.5f, "Structure base should sit at y=0.");

            // Nothing non-ground intrudes the clearance ring on any side: no Flammable centre
            // lands inside the ring (burnables are pushed out of the walkaround).
            float ringX = sb.extents.x + CLEARANCE;
            float ringZ = sb.extents.z + CLEARANCE;
            foreach (var fl in flammables)
            {
                Vector3 c = CombinedRendererBounds(fl.gameObject).center;
                bool insideRing = Mathf.Abs(c.x - sb.center.x) <= ringX && Mathf.Abs(c.z - sb.center.z) <= ringZ;
                Assert.IsFalse(insideRing,
                    $"Flammable '{fl.name}' intrudes the clearance ring; the walkaround must stay clear.");
            }

            // Baked NavMesh exists.
            Assert.Greater(NavMesh.CalculateTriangulation().vertices.Length, 0,
                "Expected a baked NavMesh.");
        }

        private static Bounds CombinedRendererBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.zero);
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }
    }
}
