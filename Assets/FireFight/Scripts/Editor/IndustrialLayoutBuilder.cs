using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FireFight.Managers;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace FireFight.EditorTools
{
    /// <summary>
    /// Procedurally assembles a functional firefighter SIZE-UP training scene using ONLY the
    /// prefabs imported under Assets/RPG_FPS_game_assets_industrial and Assets/Base_Man.
    ///
    /// The scene is designed for a 360° walkaround: a single free-standing central structure
    /// (a hangar) sits at world origin with a guaranteed clearance ring on all four sides, so
    /// a first-person firefighter can circle it and read the hazards. Burnable objects are
    /// tagged with Flammable in the SAVED scene and carry a play-mode FlammableHighlight cue,
    /// a FireManager + SeedIgniter start and spread a demo fire on Play, and NPCs on a baked
    /// NavMesh demonstrate the command/movement system.
    ///
    /// The SAVED scene is the deliverable: nothing here depends on a human clicking a menu
    /// after generation. The scene is written to a FIXED path and overwritten each run.
    ///
    /// Menu: Tools > FireFight > Build Industrial Layout
    /// </summary>
    public static class IndustrialLayoutBuilder
    {
        private const string MenuRoot = "Tools/FireFight/";
        private const string AssetRoot = "Assets/RPG_FPS_game_assets_industrial";
        private const string SceneFolder = "Assets/FireFight/Scenes";
        private const string SceneName = "IndustrialLayout";
        private const string BaseManPrefabPath = "Assets/Base_Man/Prefab/Rrefab.prefab";

        // Layout tuning ---------------------------------------------------------------
        private const int GroundTilesX = 12;   // ground tiles along X
        private const int GroundTilesZ = 12;   // ground tiles along Z
        private const int RandomSeed = 1234;   // deterministic result

        // Fire grid / clearance tuning ------------------------------------------------
        internal const float CLEARANCE = 6f;    // walkable ring around the central structure (m)
        internal const int MAX_CELLS = 48;      // per-axis fire-grid cell budget

        /// <summary>Headless entry point for CI / -executeMethod. No dialogs.</summary>
        public static void BuildBatch()
        {
            try
            {
                Build();
                Debug.Log("[FireFight] BuildBatch completed successfully.");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                throw;
            }
        }

        [MenuItem(MenuRoot + "Build Industrial Layout")]
        public static void BuildMenu()
        {
            if (GraphicsSettings.currentRenderPipeline is not UniversalRenderPipelineAsset)
            {
                EditorUtility.DisplayDialog("Build Industrial Layout",
                    "Active render pipeline is not URP. Aborting.", "OK");
                return;
            }

            if (!AssetDatabase.IsValidFolder(AssetRoot))
            {
                EditorUtility.DisplayDialog("Build Industrial Layout",
                    $"Could not find imported assets at '{AssetRoot}'.", "OK");
                return;
            }

            if (!EditorUtility.DisplayDialog("Build Industrial Layout",
                    "This builds a firefighter size-up scene from your imported " +
                    "RPG_FPS_game_assets_industrial + Base_Man prefabs only.\n\n" +
                    "This OVERWRITES Assets/FireFight/Scenes/IndustrialLayout.unity. On a shared " +
                    "repo, commit or stash that scene first so a teammate's in-flight edits are safe.\n\n" +
                    "Built-in (pink) materials in the pack will be auto-converted to URP first.\n\n" +
                    "Continue?", "Build", "Cancel"))
            {
                return;
            }

            try
            {
                Build();
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("Build Industrial Layout",
                    "Done! A firefighter size-up scene was created and saved to:\n\n" +
                    $"{SceneFolder}/{SceneName}.unity" +
                    "\n\nPress Play: walk the 360 around the central hangar with WASD + mouse, " +
                    "spot the glowing burnables, and watch the demo fire spread.", "OK");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("Build Industrial Layout",
                    "Build failed: " + e.Message + "\n\nSee Console for details.", "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void Build()
        {
            EditorUtility.DisplayProgressBar("Industrial Layout", "Upgrading imported materials to URP...", 0.05f);
            UpgradeImportedMaterialsToUrp();

            EditorUtility.DisplayProgressBar("Industrial Layout", "Creating scene...", 0.1f);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var random = new System.Random(RandomSeed);
            var root = new GameObject("IndustrialLayout");

            // --- Catalog the imported prefabs by category --------------------------------
            var catalog = new PrefabCatalog(AssetRoot);
            if (catalog.GroundFloors.Count == 0)
                throw new Exception("No road floor prefabs found to build walkable ground from.");

            // --- Lighting & sky (uses the project's URP settings) ------------------------
            EditorUtility.DisplayProgressBar("Industrial Layout", "Lighting...", 0.15f);
            CreateLighting(root);

            // --- Ground ------------------------------------------------------------------
            EditorUtility.DisplayProgressBar("Industrial Layout", "Laying walkable ground...", 0.2f);
            var groundRoot = new GameObject("Ground");
            groundRoot.transform.SetParent(root.transform);

            GameObject sampleFloor = catalog.GroundFloors[0];
            Vector3 tileSize = GetPrefabSize(sampleFloor);
            if (tileSize.x < 0.01f || tileSize.z < 0.01f) tileSize = new Vector3(4f, 0f, 4f);

            float originX = -(GroundTilesX * tileSize.x) * 0.5f;
            float originZ = -(GroundTilesZ * tileSize.z) * 0.5f;
            var occupied = new List<Bounds>();

            for (int gx = 0; gx < GroundTilesX; gx++)
            {
                for (int gz = 0; gz < GroundTilesZ; gz++)
                {
                    GameObject floorPrefab = catalog.GroundFloors[random.Next(catalog.GroundFloors.Count)];
                    Vector3 pos = new Vector3(
                        originX + (gx + 0.5f) * tileSize.x,
                        0f,
                        originZ + (gz + 0.5f) * tileSize.z);
                    var tile = InstantiatePrefab(floorPrefab, groundRoot.transform, pos, Quaternion.identity);
                    MarkStatic(tile);
                }
            }

            float areaMinX = originX, areaMaxX = originX + GroundTilesX * tileSize.x;
            float areaMinZ = originZ, areaMaxZ = originZ + GroundTilesZ * tileSize.z;

            // --- Central free-standing structure (before roads, pushes its clearance ring
            //     into 'occupied' so nothing else can intrude the walkaround) --------------
            EditorUtility.DisplayProgressBar("Industrial Layout", "Placing central structure...", 0.3f);
            PlaceCentralStructure(catalog, root.transform, occupied, out Bounds structureBounds);
            Bounds clearanceRing = MakeClearanceRing(structureBounds);

            // --- Roads: a cross framing the structure (skips the clearance ring) ---------
            EditorUtility.DisplayProgressBar("Industrial Layout", "Placing roads...", 0.4f);
            PlaceRoads(catalog, root.transform, areaMinX, areaMaxX, areaMinZ, areaMaxZ, occupied, clearanceRing);

            // --- Perimeter fence ---------------------------------------------------------
            EditorUtility.DisplayProgressBar("Industrial Layout", "Building perimeter fence...", 0.5f);
            PlacePerimeterFence(catalog, root.transform, areaMinX, areaMaxX, areaMinZ, areaMaxZ, occupied);

            // --- Secondary buildings along the back edge only ----------------------------
            EditorUtility.DisplayProgressBar("Industrial Layout", "Placing buildings...", 0.55f);
            PlaceBuildings(catalog, root.transform, random, areaMinX, areaMaxX, areaMaxZ, occupied);

            // --- Large structures: oil tanks & containers --------------------------------
            EditorUtility.DisplayProgressBar("Industrial Layout", "Placing large structures...", 0.6f);
            PlaceLargeStructures(catalog, root.transform, random, areaMinX, areaMaxX, areaMinZ, areaMaxZ, occupied);

            // --- Scatter props -----------------------------------------------------------
            EditorUtility.DisplayProgressBar("Industrial Layout", "Scattering props...", 0.68f);
            ScatterProps(catalog, root.transform, random, areaMinX, areaMaxX, areaMinZ, areaMaxZ, occupied);

            // --- Tag flammables in the SAVED scene + play-mode highlight + pick seed ------
            EditorUtility.DisplayProgressBar("Industrial Layout", "Tagging flammables...", 0.74f);
            var tagged = FlammableTagger.TagFlammables(
                root.GetComponentsInChildren<Transform>(true).Select(t => t.gameObject),
                useUndo: false);

            Flammable seed = null;
            float best = float.MaxValue;
            foreach (var fl in tagged)
            {
                var hl = fl.gameObject.AddComponent<FlammableHighlight>();
                hl.igniteParticlePrefab = catalog.SmokeParticle
                                          ?? (catalog.Particles.Count > 0 ? catalog.Particles[0] : null);
                hl.spawnParticleOnIgnite = hl.igniteParticlePrefab != null;

                // Seed = flammable nearest the structure centre (origin after recentring).
                Vector3 c = GetInstanceBounds(fl.gameObject).center;
                float d = new Vector3(c.x, 0f, c.z).sqrMagnitude;
                if (d < best) { best = d; seed = fl; }
            }
            Debug.Log($"[FireFight] Tagged {tagged.Count} flammables; seed = " +
                      (seed != null ? seed.name : "<none>"));

            // --- Fire system (manager + gizmo + deterministic igniter + demo controls) ----
            EditorUtility.DisplayProgressBar("Industrial Layout", "Wiring fire system...", 0.8f);
            CreateFireSystem(root.transform, structureBounds, seed);

            // --- Player at the size-up vantage -------------------------------------------
            EditorUtility.DisplayProgressBar("Industrial Layout", "Adding firefighter player...", 0.86f);
            var player = CreatePlayer(root.transform, structureBounds, areaMinZ);

            // --- NPCs on the clearance ring + command controller -------------------------
            EditorUtility.DisplayProgressBar("Industrial Layout", "Placing NPCs...", 0.9f);
            CreateNpcs(root.transform, player, structureBounds);

            // --- NavMesh (fatal if it bakes nothing) -------------------------------------
            EditorUtility.DisplayProgressBar("Industrial Layout", "Baking NavMesh...", 0.94f);
            BakeNavMesh(root);

            // --- Save to a FIXED path, overwriting in place ------------------------------
            EditorUtility.DisplayProgressBar("Industrial Layout", "Saving scene...", 0.98f);
            if (!AssetDatabase.IsValidFolder(SceneFolder))
                Directory.CreateDirectory(SceneFolder);
            string scenePath = $"{SceneFolder}/{SceneName}.unity";
            if (!EditorSceneManager.SaveScene(scene, scenePath))
                throw new Exception(
                    "Could not save IndustrialLayout.unity - is it open/locked by another Unity " +
                    "instance or VCS?");
            AssetDatabase.Refresh();

            Debug.Log($"[FireFight] Industrial layout built and saved to {scenePath}. " +
                      $"Ground: {GroundTilesX}x{GroundTilesZ} tiles ({tileSize.x:F1}x{tileSize.z:F1}m each). " +
                      $"Structure bounds: center={structureBounds.center}, size={structureBounds.size}. " +
                      $"Flammables tagged: {tagged.Count}. Occupied footprints: {occupied.Count}.");
            // NOTE: no success dialog here - Build() is shared by the headless BuildBatch entry
            // (CI / tests) where EditorUtility.DisplayDialog must not run. The interactive
            // BuildMenu shows the completion dialog itself after Build() returns.
        }

        // ================================================================= Pure helpers
        // These two are static and take plain inputs so they can be unit-tested (see
        // IndustrialLayoutBuilderHelpersTests) without a Unity scene.

        /// <summary>
        /// Choose the central structure prefab deterministically: prefer a name containing
        /// "_full" (or exactly "Hangar_v1_full"), else the largest X·Z footprint. Near-zero
        /// footprints are ignored. Returns null for an empty/degenerate candidate set.
        /// </summary>
        internal static GameObject PickCentralPrefab(IReadOnlyList<(GameObject prefab, Vector3 size)> candidates)
        {
            if (candidates == null || candidates.Count == 0) return null;

            // Preferred by name.
            foreach (var (prefab, size) in candidates)
            {
                if (prefab == null) continue;
                string n = prefab.name.ToLowerInvariant();
                if (n == "hangar_v1_full" || n.Contains("_full"))
                    return prefab;
            }

            // Else largest footprint (ignore near-zero).
            GameObject bestPrefab = null;
            float bestArea = 0f;
            foreach (var (prefab, size) in candidates)
            {
                if (prefab == null) continue;
                if (size.x < 0.1f || size.z < 0.1f) continue;
                float area = size.x * size.z;
                if (area > bestArea) { bestArea = area; bestPrefab = prefab; }
            }
            return bestPrefab;
        }

        /// <summary>
        /// Extra grid margin (m per side) BEYOND the clearance ring. Flammables are pushed just
        /// outside the clearance ring by the occupancy test, so a grid sized to exactly
        /// structure+ring would leave the nearest burnables sitting on (or past) the grid edge,
        /// where <see cref="FireGrid.WorldToGrid"/> floor-rounding can drop their cells and the
        /// demo fire never ignites. This margin pulls the first band of burnables comfortably
        /// inside the grid so the seed always has fuelled cells to light.
        /// </summary>
        internal const float FUEL_MARGIN = 8f;

        /// <summary>
        /// Derive a fire grid that provably covers the structure, its full clearance ring, AND a
        /// margin of burnables just outside the ring, under a fixed per-axis cell budget. The
        /// invariant <c>gridSize.x*cellSize >= structSize.x + 2*CLEARANCE</c> (and likewise Z)
        /// always holds because <c>cellSize >= requiredX / MAX_CELLS</c> and the margin only grows
        /// the required extent.
        /// </summary>
        internal static (Vector3Int gridSize, float cellSize) ComputeFireGrid(Vector3 structSize)
        {
            float requiredX = structSize.x + 2f * (CLEARANCE + FUEL_MARGIN);
            float requiredZ = structSize.z + 2f * (CLEARANCE + FUEL_MARGIN);
            float cellSize = Mathf.Max(1f, requiredX / MAX_CELLS, requiredZ / MAX_CELLS);
            int nx = Mathf.Clamp(Mathf.CeilToInt(requiredX / cellSize), 8, MAX_CELLS);
            int nz = Mathf.Clamp(Mathf.CeilToInt(requiredZ / cellSize), 8, MAX_CELLS);
            return (new Vector3Int(nx, 6, nz), cellSize);
        }

        // ================================================================= Central structure
        private static void PlaceCentralStructure(PrefabCatalog cat, Transform parent,
            List<Bounds> occupied, out Bounds structureBounds)
        {
            // Build the candidate list (buildings preferred; oil tanks as fallback).
            var candidates = new List<(GameObject, Vector3)>();
            foreach (var b in cat.Buildings) candidates.Add((b, GetPrefabSize(b)));

            GameObject chosen = PickCentralPrefab(candidates);
            if (chosen == null)
            {
                // Fallback: largest oil tank.
                var tanks = new List<(GameObject, Vector3)>();
                foreach (var t in cat.OilTanks) tanks.Add((t, GetPrefabSize(t)));
                chosen = PickCentralPrefab(tanks);
                if (chosen != null)
                    Debug.LogWarning("[FireFight] No building prefabs; using an oil tank as the central structure.");
            }
            if (chosen == null)
                throw new Exception("No building or oil-tank prefab available for the central structure.");

            var holder = new GameObject("CentralStructure");
            holder.transform.SetParent(parent);

            var go = InstantiatePrefab(chosen, holder.transform, Vector3.zero, Quaternion.identity);

            // Centre the GEOMETRY (not the pivot) on origin X/Z and seat the base at y=0.
            Bounds b0 = GetInstanceBounds(go);
            go.transform.position -= new Vector3(b0.center.x, b0.min.y, b0.center.z);

            // Re-read bounds after the offset: this is the authoritative structureBounds.
            structureBounds = GetInstanceBounds(go);
            MarkStatic(go);

            // Push the padded clearance ring into 'occupied' FIRST so nothing else intrudes.
            occupied.Add(MakeClearanceRing(structureBounds));
        }

        /// <summary>Footprint of the structure padded by CLEARANCE on X/Z (flat, y spans a wall height).</summary>
        private static Bounds MakeClearanceRing(Bounds structureBounds)
        {
            var size = new Vector3(
                structureBounds.size.x + 2f * CLEARANCE,
                4f,
                structureBounds.size.z + 2f * CLEARANCE);
            return new Bounds(new Vector3(structureBounds.center.x, 0f, structureBounds.center.z), size);
        }

        private static bool InsideRing(Vector3 worldXZ, Bounds ring)
        {
            return Mathf.Abs(worldXZ.x - ring.center.x) <= ring.extents.x &&
                   Mathf.Abs(worldXZ.z - ring.center.z) <= ring.extents.z;
        }

        // ================================================================= Roads
        private static void PlaceRoads(PrefabCatalog cat, Transform parent,
            float minX, float maxX, float minZ, float maxZ, List<Bounds> occupied, Bounds ring)
        {
            if (cat.RoadTiles.Count == 0) return;

            var roadRoot = new GameObject("Roads");
            roadRoot.transform.SetParent(parent);

            GameObject roadPrefab = cat.RoadTiles[0];
            Vector3 roadSize = GetPrefabSize(roadPrefab);
            float step = Mathf.Max(roadSize.x, 0.5f);

            // Road along X through centre (z = 0) - skip tiles inside the clearance ring.
            for (float x = minX + step * 0.5f; x < maxX; x += step)
            {
                var p = new Vector3(x, 0.02f, 0f);
                if (InsideRing(p, ring)) continue;
                var r = InstantiatePrefab(roadPrefab, roadRoot.transform, p, Quaternion.identity);
                MarkStatic(r);
                occupied.Add(new Bounds(new Vector3(x, 0, 0), new Vector3(roadSize.x, 1, roadSize.z)));
            }
            // Road along Z through centre (x = 0), rotated 90 - also skip the ring.
            for (float z = minZ + step * 0.5f; z < maxZ; z += step)
            {
                if (Mathf.Abs(z) < roadSize.z) continue; // avoid doubling the intersection
                var p = new Vector3(0f, 0.02f, z);
                if (InsideRing(p, ring)) continue;
                var r = InstantiatePrefab(roadPrefab, roadRoot.transform, p, Quaternion.Euler(0, 90, 0));
                MarkStatic(r);
                occupied.Add(new Bounds(new Vector3(0, 0, z), new Vector3(roadSize.z, 1, roadSize.x)));
            }
        }

        // ================================================================= Fence
        private static void PlacePerimeterFence(PrefabCatalog cat, Transform parent,
            float minX, float maxX, float minZ, float maxZ, List<Bounds> occupied)
        {
            if (cat.FenceStraight == null) return;

            var fenceRoot = new GameObject("PerimeterFence");
            fenceRoot.transform.SetParent(parent);

            Vector3 size = GetPrefabSize(cat.FenceStraight);
            float seg = Mathf.Max(size.x, 0.5f);
            float inset = seg * 0.5f;

            for (float x = minX + inset; x <= maxX - inset; x += seg)
            {
                PlaceFenceSeg(cat, fenceRoot.transform, new Vector3(x, 0, minZ + inset * 0.2f), Quaternion.identity, occupied, size);
                PlaceFenceSeg(cat, fenceRoot.transform, new Vector3(x, 0, maxZ - inset * 0.2f), Quaternion.identity, occupied, size);
            }
            for (float z = minZ + inset; z <= maxZ - inset; z += seg)
            {
                PlaceFenceSeg(cat, fenceRoot.transform, new Vector3(minX + inset * 0.2f, 0, z), Quaternion.Euler(0, 90, 0), occupied, size);
                PlaceFenceSeg(cat, fenceRoot.transform, new Vector3(maxX - inset * 0.2f, 0, z), Quaternion.Euler(0, 90, 0), occupied, size);
            }
        }

        private static void PlaceFenceSeg(PrefabCatalog cat, Transform parent, Vector3 pos,
            Quaternion rot, List<Bounds> occupied, Vector3 size)
        {
            var f = InstantiatePrefab(cat.FenceStraight, parent, pos, rot);
            MarkStatic(f);
            occupied.Add(new Bounds(pos, new Vector3(Mathf.Max(size.x, size.z), 2, Mathf.Max(size.x, size.z))));
        }

        // ================================================================= Secondary buildings
        private static void PlaceBuildings(PrefabCatalog cat, Transform parent, System.Random rng,
            float minX, float maxX, float maxZ, List<Bounds> occupied)
        {
            if (cat.Buildings.Count == 0) return;

            var bRoot = new GameObject("Buildings");
            bRoot.transform.SetParent(parent);

            float cursorX = minX + 6f;
            float rowZ = maxZ - 10f;
            int placed = 0, guard = 0;

            // Only a few secondary hangars along the back edge, honouring 'occupied' (so the
            // clearance ring the central structure pushed in keeps them out of the centre).
            while (cursorX < maxX - 6f && guard++ < 50 && placed < 3)
            {
                GameObject prefab = cat.Buildings[rng.Next(cat.Buildings.Count)];
                Vector3 size = GetPrefabSize(prefab);
                if (size.x < 0.1f) { cursorX += 8f; continue; }

                Vector3 pos = new Vector3(cursorX + size.x * 0.5f, 0f, rowZ - size.z * 0.5f);
                var footprint = new Bounds(pos, new Vector3(size.x, 4, size.z));
                if (!Overlaps(footprint, occupied, 1.5f))
                {
                    var go = InstantiatePrefab(prefab, bRoot.transform, pos, Quaternion.Euler(0, 180, 0));
                    MarkStatic(go);
                    occupied.Add(footprint);
                    placed++;
                }
                cursorX += size.x + 4f;
            }
        }

        // ================================================================= Large structures
        private static void PlaceLargeStructures(PrefabCatalog cat, Transform parent, System.Random rng,
            float minX, float maxX, float minZ, float maxZ, List<Bounds> occupied)
        {
            var sRoot = new GameObject("Structures");
            sRoot.transform.SetParent(parent);

            var big = new List<GameObject>();
            big.AddRange(cat.OilTanks);
            big.AddRange(cat.Containers);
            if (big.Count == 0) return;

            int placed = 0, attempts = 0, target = 14;
            while (placed < target && attempts++ < 400)
            {
                GameObject prefab = big[rng.Next(big.Count)];
                Vector3 size = GetPrefabSize(prefab);
                if (size.x < 0.1f) continue;

                float x = Mathf.Lerp(minX + size.x, maxX - size.x, (float)rng.NextDouble());
                float z = Mathf.Lerp(minZ + size.z, maxZ - 14f, (float)rng.NextDouble());
                Vector3 pos = new Vector3(x, 0f, z);
                var footprint = new Bounds(pos, new Vector3(size.x, 4, size.z));
                if (Overlaps(footprint, occupied, 2.0f)) continue;

                float yaw = 90f * rng.Next(4);
                var go = InstantiatePrefab(prefab, sRoot.transform, pos, Quaternion.Euler(0, yaw, 0));
                MarkStatic(go);
                occupied.Add(footprint);
                placed++;
            }
        }

        // ================================================================= Props
        private static void ScatterProps(PrefabCatalog cat, Transform parent, System.Random rng,
            float minX, float maxX, float minZ, float maxZ, List<Bounds> occupied)
        {
            if (cat.Props.Count == 0 && cat.Particles.Count == 0) return;

            var pRoot = new GameObject("Props");
            pRoot.transform.SetParent(parent);

            int placed = 0, attempts = 0, target = 90;
            while (cat.Props.Count > 0 && placed < target && attempts++ < 2000)
            {
                GameObject prefab = cat.Props[rng.Next(cat.Props.Count)];
                Vector3 size = GetPrefabSize(prefab);
                if (size.x < 0.05f) continue;

                float x = Mathf.Lerp(minX + 2f, maxX - 2f, (float)rng.NextDouble());
                float z = Mathf.Lerp(minZ + 2f, maxZ - 2f, (float)rng.NextDouble());
                Vector3 pos = new Vector3(x, 0f, z);
                var footprint = new Bounds(pos, new Vector3(Mathf.Max(size.x, 0.3f), 2, Mathf.Max(size.z, 0.3f)));
                if (Overlaps(footprint, occupied, 0.3f)) continue;

                float yaw = (float)(rng.NextDouble() * 360.0);
                var go = InstantiatePrefab(prefab, pRoot.transform, pos, Quaternion.Euler(0, yaw, 0));
                MarkStatic(go);
                occupied.Add(footprint);
                placed++;
            }
        }

        // ================================================================= Fire system
        private static void CreateFireSystem(Transform parent, Bounds structureBounds, Flammable seed)
        {
            var fireGo = new GameObject("FireSystem");
            fireGo.transform.SetParent(parent);
            // Grid is anchored at transform.position: centre on the structure, floor at y=0.
            fireGo.transform.position = new Vector3(structureBounds.center.x, 0f, structureBounds.center.z);

            var mgr = fireGo.AddComponent<FireManager>();
            var (gridSize, cellSize) = ComputeFireGrid(structureBounds.size);
            mgr.gridSize = gridSize;
            mgr.cellSize = cellSize;
            mgr.fillWithFuel = false; // air does not burn; Flammables paint their own fuel.

            fireGo.AddComponent<FireGizmoVisualizer>(); // Scene-view debug; harmless at runtime.

            var igniter = fireGo.AddComponent<SeedIgniter>();
            var so = new SerializedObject(igniter);
            so.FindProperty("seed").objectReferenceValue = seed;        // may be null -> runtime fallback
            so.FindProperty("fireManager").objectReferenceValue = mgr;
            so.ApplyModifiedPropertiesWithoutUndo();

            // FireDemo kept for interactive training controls only (Space / LMB); its own
            // start-ignition is OFF so the deterministic SeedIgniter is the single demo fire.
            var demo = fireGo.AddComponent<FireDemo>();
            demo.igniteCentreOnStart = false;
        }

        // ================================================================= Player
        private static GameObject CreatePlayer(Transform parent, Bounds structureBounds, float areaMinZ)
        {
            var player = new GameObject("Player");
            player.transform.SetParent(parent);

            // Size-up vantage: just outside the clearance ring on the -Z (front) side, but
            // clamped a couple of metres inside the perimeter so a big hangar can't push the
            // spawn onto the fence.
            float spawnZ = Mathf.Max(areaMinZ + 2f, -(structureBounds.extents.z + CLEARANCE + 2f));
            player.transform.position = new Vector3(structureBounds.center.x, 1.2f, spawnZ);
            player.transform.forward = Vector3.forward; // face the structure (+Z)
            player.tag = "Player";

            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.35f;
            cc.center = new Vector3(0, 0.9f, 0);

            var camGo = new GameObject("PlayerCamera");
            camGo.transform.SetParent(player.transform);
            camGo.transform.localPosition = new Vector3(0, 1.6f, 0);
            var cam = camGo.AddComponent<Camera>();
            camGo.AddComponent<AudioListener>();
            cam.tag = "MainCamera";
            camGo.AddComponent<UniversalAdditionalCameraData>();

            // Reuse the project's existing FirefighterMovement controller.
            var moveType = FindType("FirefighterMovement");
            if (moveType != null)
            {
                var comp = player.AddComponent(moveType);
                var field = moveType.GetField("playerCamera",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null) field.SetValue(comp, camGo.transform);
                var so = new SerializedObject(comp);
                var prop = so.FindProperty("playerCamera");
                if (prop != null) { prop.objectReferenceValue = camGo.transform; so.ApplyModifiedPropertiesWithoutUndo(); }
            }
            else
            {
                Debug.LogWarning("[FireFight] FirefighterMovement not found; player added without a controller.");
            }

            return player;
        }

        // ================================================================= NPCs
        private static void CreateNpcs(Transform parent, GameObject player, Bounds structureBounds)
        {
            var npcPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BaseManPrefabPath);
            if (npcPrefab == null)
            {
                Debug.LogWarning($"[FireFight] Base_Man prefab not found at {BaseManPrefabPath}; skipping NPCs.");
                return;
            }

            var npcRoot = new GameObject("NPCs");
            npcRoot.transform.SetParent(parent);

            // Two points on the clearance ring (side B and side D of the structure).
            float ringX = structureBounds.extents.x + CLEARANCE * 0.5f;
            float ringZ = structureBounds.extents.z + CLEARANCE * 0.5f;
            var spots = new[]
            {
                new Vector3(structureBounds.center.x + ringX, 0f, structureBounds.center.z),
                new Vector3(structureBounds.center.x - ringX, 0f, structureBounds.center.z + ringZ),
            };

            var movementType = FindType("NPCMovement");
            NPCMovement firstNpc = null;

            foreach (var spot in spots)
            {
                var npc = InstantiatePrefab(npcPrefab, npcRoot.transform, spot, Quaternion.identity);

                // Seat on the ground via the combined bind-pose bounds of all SkinnedMeshRenderers.
                float minY;
                if (TryCombinedRendererMinY(npc, out minY))
                    npc.transform.position = new Vector3(spot.x, -minY, spot.z);
                // else: leave at y=0 (prefab authored at the base).

                // Capsule so selection raycasts / commands can hit the NPC.
                var cap = npc.AddComponent<CapsuleCollider>();
                cap.height = 1.8f;
                cap.radius = 0.3f;
                cap.center = new Vector3(0f, 0.9f, 0f);

                var agent = npc.AddComponent<NavMeshAgent>();
                agent.height = 1.8f;
                agent.radius = 0.3f;

                if (movementType != null)
                {
                    var mv = npc.AddComponent(movementType);
                    if (firstNpc == null) firstNpc = mv as NPCMovement;
                }
            }

            // One command controller on the Player, pre-selecting the first NPC.
            var controller = player.AddComponent<NPCCommandController>();
            var cam = player.GetComponentInChildren<Camera>();
            var so = new SerializedObject(controller);
            var camProp = so.FindProperty("commandCamera");
            if (camProp != null) camProp.objectReferenceValue = cam;
            so.ApplyModifiedPropertiesWithoutUndo();
            if (firstNpc != null) controller.SelectNpc(firstNpc);
        }

        /// <summary>Combined world-space min.y of all SkinnedMeshRenderers (bind pose) on a fresh instance.</summary>
        private static bool TryCombinedRendererMinY(GameObject instance, out float minY)
        {
            var skinned = instance.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (skinned.Length > 0)
            {
                Bounds b = skinned[0].bounds;
                for (int i = 1; i < skinned.Length; i++) b.Encapsulate(skinned[i].bounds);
                if (b.size.y > 0.001f) { minY = b.min.y; return true; }
            }
            minY = 0f;
            return false;
        }

        // ================================================================= Lighting
        private static void CreateLighting(GameObject root)
        {
            var sunGo = new GameObject("Directional Light");
            sunGo.transform.SetParent(root.transform);
            sunGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var light = sunGo.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Soft;
            sunGo.AddComponent<UniversalAdditionalLightData>();
            RenderSettings.sun = light;
            RenderSettings.ambientMode = AmbientMode.Trilight;
        }

        // ================================================================= NavMesh (fatal)
        private static void BakeNavMesh(GameObject root)
        {
            var surface = root.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            try
            {
                surface.BuildNavMesh();
            }
            catch (Exception e)
            {
                throw new Exception(
                    "NavMesh bake threw: " + e.Message +
                    " - check that road-floor tiles have mesh colliders and the ground is flat.");
            }

            if (NavMesh.CalculateTriangulation().vertices.Length == 0)
                throw new Exception(
                    "NavMesh bake produced no walkable surface - check that road-floor tiles have " +
                    "mesh colliders and that the ground is flat.");
        }

        // ================================================================= Material upgrade
        private static void UpgradeImportedMaterialsToUrp()
        {
            var mats = new List<Material>();
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { AssetRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m != null) mats.Add(m);
            }
            MaterialUrpUpgrader.ConvertForBuilder(mats);
        }

        // ================================================================= Helpers
        private static GameObject InstantiatePrefab(GameObject prefab, Transform parent, Vector3 pos, Quaternion rot)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.SetParent(parent);
            go.transform.SetPositionAndRotation(pos, rot);
            return go;
        }

        private static bool Overlaps(Bounds b, List<Bounds> others, float padding)
        {
            var padded = new Bounds(b.center, b.size + new Vector3(padding * 2, 0, padding * 2));
            foreach (var o in others)
            {
                var flatA = new Bounds(new Vector3(padded.center.x, 0, padded.center.z),
                                       new Vector3(padded.size.x, 1, padded.size.z));
                var flatB = new Bounds(new Vector3(o.center.x, 0, o.center.z),
                                       new Vector3(o.size.x, 1, o.size.z));
                if (flatA.Intersects(flatB)) return true;
            }
            return false;
        }

        private static void MarkStatic(GameObject go)
        {
            GameObjectUtility.SetStaticEditorFlags(go,
                StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic |
                StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic);
        }

        /// <summary>World-space size of a prefab from its combined renderer bounds.</summary>
        private static Vector3 GetPrefabSize(GameObject prefab)
        {
            var temp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            temp.transform.position = Vector3.zero;
            temp.transform.rotation = Quaternion.identity;
            temp.transform.localScale = Vector3.one;

            var renderers = temp.GetComponentsInChildren<Renderer>();
            Vector3 size = Vector3.zero;
            if (renderers.Length > 0)
            {
                Bounds b = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
                size = b.size;
            }
            UnityEngine.Object.DestroyImmediate(temp);
            return size;
        }

        /// <summary>Combined world-space renderer bounds of a LIVE instance (keeps centre, unlike GetPrefabSize).</summary>
        private static Bounds GetInstanceBounds(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return new Bounds(instance.transform.position, Vector3.zero);
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        private static Type FindType(string simpleName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetTypes().FirstOrDefault(x => x.Name == simpleName && typeof(MonoBehaviour).IsAssignableFrom(x));
                if (t != null) return t;
            }
            return null;
        }

        // ================================================================= Catalog
        private class PrefabCatalog
        {
            public readonly List<GameObject> GroundFloors = new();
            public readonly List<GameObject> RoadTiles = new();
            public readonly List<GameObject> Buildings = new();
            public readonly List<GameObject> OilTanks = new();
            public readonly List<GameObject> Containers = new();
            public readonly List<GameObject> Props = new();
            public readonly List<GameObject> Particles = new();
            public GameObject FenceStraight;
            public GameObject SmokeParticle;

            public PrefabCatalog(string root)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { root }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) continue;
                    string lower = path.ToLowerInvariant();
                    string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

                    if (lower.Contains("/roads/") && name.Contains("floor"))
                    {
                        GroundFloors.Add(prefab);
                        continue;
                    }
                    if (lower.Contains("/roads/") && name.Contains("tile"))
                    {
                        RoadTiles.Add(prefab);
                        continue;
                    }
                    if (lower.Contains("/buildings/"))
                    {
                        Buildings.Add(prefab);
                        continue;
                    }
                    if (lower.Contains("/oil_tanks/")) { OilTanks.Add(prefab); continue; }
                    if (lower.Contains("/containers/")) { Containers.Add(prefab); continue; }
                    if (lower.Contains("/particles/"))
                    {
                        Particles.Add(prefab);
                        // Prefer a smoke emitter for the ignite cue (not dust).
                        if (SmokeParticle == null && name.Contains("smoke"))
                            SmokeParticle = prefab;
                        continue;
                    }
                    if (lower.Contains("/fences/"))
                    {
                        if (FenceStraight == null && (name.EndsWith("_s") || name.Contains("_s_") || name.Contains("wall_set")))
                            FenceStraight = prefab;
                        continue;
                    }
                    if (lower.Contains("/barrels/") || lower.Contains("/boxes/") ||
                        lower.Contains("/dumpsters/") || lower.Contains("/other_props/"))
                    {
                        Props.Add(prefab);
                    }
                }

                if (RoadTiles.Count == 0) RoadTiles.AddRange(GroundFloors);
                if (FenceStraight == null)
                {
                    foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { root + "/Fences" }))
                    {
                        FenceStraight = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                        if (FenceStraight != null) break;
                    }
                }
            }
        }
    }
}
