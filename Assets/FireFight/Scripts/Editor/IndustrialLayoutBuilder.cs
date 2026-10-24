using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// Procedurally assembles a fully walkable industrial layout using ONLY the prefabs
    /// imported under Assets/RPG_FPS_game_assets_industrial (roads, buildings, fences,
    /// oil tanks, containers, props, particles, shaders, textures).
    ///
    /// The builder reads each prefab's real world-space bounds at generation time, so tile
    /// placement, prop spacing and perimeter walls adapt to the actual geometry instead of
    /// hard-coded sizes. A walkable ground is laid from the road floor tiles (which ship with
    /// mesh colliders), props are scattered without overlapping, a first-person player
    /// (reusing the project's FirefighterMovement) is dropped in, and a NavMesh is baked for
    /// the whole walkable area. Everything is saved into a fresh scene.
    ///
    /// Menu: Tools > FireFight > Build Industrial Layout
    /// </summary>
    public static class IndustrialLayoutBuilder
    {
        private const string MenuRoot = "Tools/FireFight/";
        private const string AssetRoot = "Assets/RPG_FPS_game_assets_industrial";
        private const string SceneFolder = "Assets/FireFight/Scenes";
        private const string SceneName = "IndustrialLayout";

        // Layout tuning ---------------------------------------------------------------
        private const int GroundTilesX = 12;   // ground tiles along X
        private const int GroundTilesZ = 12;   // ground tiles along Z
        private const int RandomSeed = 1234;   // deterministic result

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
                    "This will create a NEW scene and assemble a walkable industrial layout " +
                    "from your imported RPG_FPS_game_assets_industrial prefabs only.\n\n" +
                    "Built-in (pink) materials in that pack will be auto-converted to URP first.\n\n" +
                    "Continue?", "Build", "Cancel"))
            {
                return;
            }

            try
            {
                Build();
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
            EditorUtility.DisplayProgressBar("Industrial Layout", "Laying walkable ground...", 0.25f);
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

            // --- Roads: a cross running through the middle -------------------------------
            EditorUtility.DisplayProgressBar("Industrial Layout", "Placing roads...", 0.4f);
            PlaceRoads(catalog, root.transform, tileSize, areaMinX, areaMaxX, areaMinZ, areaMaxZ, occupied);

            // --- Perimeter fence ---------------------------------------------------------
            EditorUtility.DisplayProgressBar("Industrial Layout", "Building perimeter fence...", 0.5f);
            PlacePerimeterFence(catalog, root.transform, areaMinX, areaMaxX, areaMinZ, areaMaxZ, occupied);

            // --- Buildings (hangars) along the back --------------------------------------
            EditorUtility.DisplayProgressBar("Industrial Layout", "Placing buildings...", 0.6f);
            PlaceBuildings(catalog, root.transform, random, areaMinX, areaMaxX, areaMaxZ, occupied);

            // --- Large structures: oil tanks & containers --------------------------------
            EditorUtility.DisplayProgressBar("Industrial Layout", "Placing large structures...", 0.7f);
            PlaceLargeStructures(catalog, root.transform, random, areaMinX, areaMaxX, areaMinZ, areaMaxZ, occupied);

            // --- Scatter props -----------------------------------------------------------
            EditorUtility.DisplayProgressBar("Industrial Layout", "Scattering props...", 0.8f);
            ScatterProps(catalog, root.transform, random, areaMinX, areaMaxX, areaMinZ, areaMaxZ, occupied);

            // --- Player ------------------------------------------------------------------
            EditorUtility.DisplayProgressBar("Industrial Layout", "Adding walkable player...", 0.88f);
            CreatePlayer(root.transform);

            // --- NavMesh -----------------------------------------------------------------
            EditorUtility.DisplayProgressBar("Industrial Layout", "Baking NavMesh...", 0.93f);
            BakeNavMesh(root, groundRoot);

            // --- Save --------------------------------------------------------------------
            EditorUtility.DisplayProgressBar("Industrial Layout", "Saving scene...", 0.98f);
            if (!AssetDatabase.IsValidFolder(SceneFolder))
                Directory.CreateDirectory(SceneFolder);
            string scenePath = AssetDatabase.GenerateUniqueAssetPath($"{SceneFolder}/{SceneName}.unity");
            EditorSceneManager.SaveScene(scene, scenePath);
            AssetDatabase.Refresh();

            Debug.Log($"[FireFight] Industrial layout built and saved to {scenePath}. " +
                      $"Ground: {GroundTilesX}x{GroundTilesZ} tiles ({tileSize.x:F1}x{tileSize.z:F1}m each). " +
                      $"Occupied footprints: {occupied.Count}.");

            EditorUtility.DisplayDialog("Build Industrial Layout",
                "Done! A walkable industrial layout was created and saved to:\n\n" + scenePath +
                "\n\nPress Play to walk around with WASD + mouse (Esc to free the cursor).", "OK");
        }

        // ------------------------------------------------------------------ Roads
        private static void PlaceRoads(PrefabCatalog cat, Transform parent, Vector3 tileSize,
            float minX, float maxX, float minZ, float maxZ, List<Bounds> occupied)
        {
            if (cat.RoadTiles.Count == 0) return;

            var roadRoot = new GameObject("Roads");
            roadRoot.transform.SetParent(parent);

            GameObject roadPrefab = cat.RoadTiles[0];
            Vector3 roadSize = GetPrefabSize(roadPrefab);
            float step = Mathf.Max(roadSize.x, 0.5f);

            // Road along X through centre (z = 0)
            for (float x = minX + step * 0.5f; x < maxX; x += step)
            {
                var r = InstantiatePrefab(roadPrefab, roadRoot.transform, new Vector3(x, 0.02f, 0f), Quaternion.identity);
                MarkStatic(r);
                occupied.Add(new Bounds(new Vector3(x, 0, 0), new Vector3(roadSize.x, 1, roadSize.z)));
            }
            // Road along Z through centre (x = 0), rotated 90
            for (float z = minZ + step * 0.5f; z < maxZ; z += step)
            {
                if (Mathf.Abs(z) < roadSize.z) continue; // avoid doubling the intersection
                var r = InstantiatePrefab(roadPrefab, roadRoot.transform, new Vector3(0f, 0.02f, z), Quaternion.Euler(0, 90, 0));
                MarkStatic(r);
                occupied.Add(new Bounds(new Vector3(0, 0, z), new Vector3(roadSize.z, 1, roadSize.x)));
            }
        }

        // ------------------------------------------------------------------ Fence
        private static void PlacePerimeterFence(PrefabCatalog cat, Transform parent,
            float minX, float maxX, float minZ, float maxZ, List<Bounds> occupied)
        {
            if (cat.FenceStraight == null) return;

            var fenceRoot = new GameObject("PerimeterFence");
            fenceRoot.transform.SetParent(parent);

            Vector3 size = GetPrefabSize(cat.FenceStraight);
            float seg = Mathf.Max(size.x, 0.5f);
            float inset = seg * 0.5f;

            // Two sides running along X (north & south)
            for (float x = minX + inset; x <= maxX - inset; x += seg)
            {
                PlaceFenceSeg(cat, fenceRoot.transform, new Vector3(x, 0, minZ + inset * 0.2f), Quaternion.identity, occupied, size);
                PlaceFenceSeg(cat, fenceRoot.transform, new Vector3(x, 0, maxZ - inset * 0.2f), Quaternion.identity, occupied, size);
            }
            // Two sides running along Z (east & west), rotated 90
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

        // ------------------------------------------------------------------ Buildings
        private static void PlaceBuildings(PrefabCatalog cat, Transform parent, System.Random rng,
            float minX, float maxX, float maxZ, List<Bounds> occupied)
        {
            if (cat.Buildings.Count == 0) return;

            var bRoot = new GameObject("Buildings");
            bRoot.transform.SetParent(parent);

            float cursorX = minX + 6f;
            float rowZ = maxZ - 10f;
            int guard = 0;

            while (cursorX < maxX - 6f && guard++ < 50)
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
                }
                cursorX += size.x + 4f;
            }
        }

        // ------------------------------------------------------------------ Large structures
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

        // ------------------------------------------------------------------ Props
        private static void ScatterProps(PrefabCatalog cat, Transform parent, System.Random rng,
            float minX, float maxX, float minZ, float maxZ, List<Bounds> occupied)
        {
            if (cat.Props.Count == 0 && cat.Particles.Count == 0) return;

            var pRoot = new GameObject("Props");
            pRoot.transform.SetParent(parent);

            int placed = 0, attempts = 0, target = 90;
            while (placed < target && attempts++ < 2000)
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

            // A couple of particle emitters (smoke/dust) for atmosphere - still imported-only.
            if (cat.Particles.Count > 0)
            {
                var fxRoot = new GameObject("Atmosphere_FX");
                fxRoot.transform.SetParent(pRoot.transform);
                for (int i = 0; i < Math.Min(3, cat.Particles.Count); i++)
                {
                    float x = Mathf.Lerp(minX + 4f, maxX - 4f, (float)rng.NextDouble());
                    float z = Mathf.Lerp(minZ + 4f, maxZ - 4f, (float)rng.NextDouble());
                    InstantiatePrefab(cat.Particles[i % cat.Particles.Count], fxRoot.transform,
                        new Vector3(x, 1.0f, z), Quaternion.identity);
                }
            }
        }

        // ------------------------------------------------------------------ Player
        private static void CreatePlayer(Transform parent)
        {
            var player = new GameObject("Player");
            player.transform.SetParent(parent);
            player.transform.position = new Vector3(0f, 1.2f, -4f);
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
        }

        // ------------------------------------------------------------------ Lighting
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

        // ------------------------------------------------------------------ NavMesh
        private static void BakeNavMesh(GameObject root, GameObject groundRoot)
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
                Debug.LogWarning("[FireFight] NavMesh bake skipped: " + e.Message);
            }
        }

        // ------------------------------------------------------------------ Material upgrade
        private static void UpgradeImportedMaterialsToUrp()
        {
            var mats = new List<Material>();
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { AssetRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m != null) mats.Add(m);
            }
            // Reuse the dedicated upgrader so behaviour stays consistent across tools.
            MaterialUrpUpgrader.ConvertForBuilder(mats);
        }

        // ------------------------------------------------------------------ Helpers
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

        private static Type FindType(string simpleName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetTypes().FirstOrDefault(x => x.Name == simpleName && typeof(MonoBehaviour).IsAssignableFrom(x));
                if (t != null) return t;
            }
            return null;
        }

        // ------------------------------------------------------------------ Catalog
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

            public PrefabCatalog(string root)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { root }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (prefab == null) continue;
                    string lower = path.ToLowerInvariant();
                    string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();

                    // Walkable ground: flat road "floor" tiles.
                    if (lower.Contains("/roads/") && name.Contains("floor"))
                    {
                        GroundFloors.Add(prefab);
                        continue;
                    }
                    // Flat road tiles (for the road cross).
                    if (lower.Contains("/roads/") && name.Contains("tile"))
                    {
                        RoadTiles.Add(prefab);
                        continue;
                    }
                    if (lower.Contains("/buildings/"))
                    {
                        // Use the larger assembled hangars, skip tiny outbuilding fragments duplicates.
                        Buildings.Add(prefab);
                        continue;
                    }
                    if (lower.Contains("/oil_tanks/")) { OilTanks.Add(prefab); continue; }
                    if (lower.Contains("/containers/")) { Containers.Add(prefab); continue; }
                    if (lower.Contains("/particles/")) { Particles.Add(prefab); continue; }
                    if (lower.Contains("/fences/"))
                    {
                        // Pick one clean straight segment for the perimeter.
                        if (FenceStraight == null && (name.EndsWith("_s") || name.Contains("_s_") || name.Contains("wall_set")))
                            FenceStraight = prefab;
                        continue;
                    }
                    // Everything else in Barrels/Boxes/Dumpsters/Other_props -> scatter props.
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
