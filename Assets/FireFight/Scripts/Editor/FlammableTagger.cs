using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using FireFight.Managers;

namespace FireFight.EditorTools
{
    /// <summary>
    /// Editor utility that adds the <see cref="Flammable"/> component to burnable
    /// objects in the scene, with per-category fuel/flammability settings.
    ///
    /// Objects are classified by their SOURCE PREFAB path, using the same folder
    /// scheme the IndustrialLayoutBuilder uses (oil_tanks, containers, barrels,
    /// boxes, dumpsters, other_props...). Non-burnable categories (roads, ground,
    /// fences, buildings shell) are skipped.
    ///
    /// All changes are registered with Undo, so a single Ctrl+Z reverts them.
    ///
    /// Menu:
    ///   Tools > FireFight > Mark Flammables In Scene
    ///   Tools > FireFight > Mark Flammables On Selection
    ///   Tools > FireFight > Remove All Flammables In Scene
    /// </summary>
    public static class FlammableTagger
    {
        private const string MenuRoot = "Tools/FireFight/";

        /// <summary>Per-category fuel + flammability presets. Tweak to taste.</summary>
        private struct FuelPreset
        {
            public float fuel;
            public float flammability;
            public FuelPreset(float fuel, float flammability)
            {
                this.fuel = fuel;
                this.flammability = flammability;
            }
        }

        // Classified by substring found in the source prefab's asset path.
        // Order matters: first match wins.
        private static readonly (string pathContains, FuelPreset preset)[] Categories =
        {
            ("/oil_tanks/",   new FuelPreset(1.0f, 0.95f)), // volatile: lots of fuel, ignites fast
            ("/barrels/",     new FuelPreset(0.9f, 0.85f)), // fuel drums
            ("/boxes/",       new FuelPreset(0.7f, 0.7f)),  // wood/cardboard crates
            ("/other_props/", new FuelPreset(0.6f, 0.6f)),  // misc props
            ("/dumpsters/",   new FuelPreset(0.6f, 0.5f)),  // rubbish burns, metal shell slows it
            ("/containers/",  new FuelPreset(0.4f, 0.2f)),  // metal: low flammability
        };

        // Categories that should NEVER burn (skip even if matched elsewhere).
        private static readonly string[] NeverBurn =
        {
            "/roads/", "/fences/", "/buildings/",
        };

        /// <summary>Name prefix of the runtime highlight overlay child; never tagged.</summary>
        private const string HighlightOverlayPrefix = "_FlammableHighlight";

        [MenuItem(MenuRoot + "Mark Flammables In Scene")]
        public static void MarkInScene()
        {
            var all = Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            var roots = new List<GameObject>(all.Length);
            foreach (var t in all) roots.Add(t.gameObject);

            Undo.SetCurrentGroupName("Mark Flammables In Scene");
            int group = Undo.GetCurrentGroup();
            var list = TagFlammables(roots, useUndo: true);
            Undo.CollapseUndoOperations(group);

            if (list.Count > 0)
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            ReportTagging(list.Count);
        }

        [MenuItem(MenuRoot + "Mark Flammables On Selection")]
        public static void MarkOnSelection()
        {
            var sel = Selection.gameObjects;
            if (sel == null || sel.Length == 0)
            {
                EditorUtility.DisplayDialog("Mark Flammables", "Nothing selected.", "OK");
                return;
            }

            // Include children of the selection.
            var list = new List<GameObject>();
            foreach (var go in sel)
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                    list.Add(t.gameObject);

            Undo.SetCurrentGroupName("Mark Flammables On Selection");
            int group = Undo.GetCurrentGroup();
            var tagged = TagFlammables(list, useUndo: true);
            Undo.CollapseUndoOperations(group);

            if (tagged.Count > 0)
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            ReportTagging(tagged.Count);
        }

        private static void ReportTagging(int added)
        {
            string summary = $"Flammable tagging done.\n\nAdded: {added}";
            Debug.Log("[FireFight] " + summary.Replace("\n\n", " ").Replace("\n", " | "));
            EditorUtility.DisplayDialog("Mark Flammables", summary, "OK");
        }

        /// <summary>
        /// Classify the given candidate GameObjects by their source-prefab folder and add a
        /// <see cref="Flammable"/> (with the category's fuel/flammability preset) to each burnable
        /// prefab-instance root that doesn't already have one. Dialog-free and reusable: the menu
        /// items call this with <paramref name="useUndo"/> true; the procedural builder calls it with
        /// false (no undo stack needed mid-build). Returns every object that received a Flammable.
        /// </summary>
        public static List<Flammable> TagFlammables(IEnumerable<GameObject> candidates, bool useUndo)
        {
            var result = new List<Flammable>();
            var seen = new HashSet<GameObject>();

            foreach (var go in candidates)
            {
                if (go == null || !seen.Add(go)) continue;

                // Never tag the runtime highlight overlay child (created at Play time).
                if (go.name.StartsWith(HighlightOverlayPrefix)) continue;

                // One Flammable per logical object: only tag the prefab-instance root
                // so a barrel made of several child meshes gets a single component.
                if (!IsPrefabInstanceRoot(go)) continue;

                if (go.GetComponent<Flammable>() != null) continue;

                string path = GetSourcePrefabPath(go);
                if (string.IsNullOrEmpty(path)) continue;
                string lower = path.ToLowerInvariant();

                if (MatchesAny(lower, NeverBurn)) continue;

                if (!TryClassify(lower, out FuelPreset preset)) continue;

                // Must have something to size the fuel volume from.
                if (go.GetComponentInChildren<Collider>() == null &&
                    go.GetComponentInChildren<Renderer>() == null)
                    continue;

                Flammable flammable = useUndo
                    ? Undo.AddComponent<Flammable>(go)
                    : go.AddComponent<Flammable>();
                flammable.fuel = preset.fuel;
                flammable.flammability = preset.flammability;
                if (!useUndo) EditorUtility.SetDirty(go);
                result.Add(flammable);
            }

            return result;
        }

        [MenuItem(MenuRoot + "Remove All Flammables In Scene")]
        public static void RemoveAllInScene()
        {
            var flammables = Object.FindObjectsByType<Flammable>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (flammables.Length == 0)
            {
                EditorUtility.DisplayDialog("Remove Flammables", "No Flammable components found.", "OK");
                return;
            }

            Undo.SetCurrentGroupName("Remove All Flammables");
            int group = Undo.GetCurrentGroup();
            int removed = 0;
            foreach (var f in flammables)
            {
                Undo.DestroyObjectImmediate(f);
                removed++;
            }
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

            Debug.Log($"[FireFight] Removed {removed} Flammable components.");
            EditorUtility.DisplayDialog("Remove Flammables", $"Removed {removed} Flammable components.", "OK");
        }

        // ---- Helpers ----

        private static bool TryClassify(string lowerPath, out FuelPreset preset)
        {
            foreach (var (pathContains, p) in Categories)
            {
                if (lowerPath.Contains(pathContains))
                {
                    preset = p;
                    return true;
                }
            }
            preset = default;
            return false;
        }

        private static bool MatchesAny(string lowerPath, string[] needles)
        {
            foreach (var n in needles)
                if (lowerPath.Contains(n)) return true;
            return false;
        }

        /// <summary>True if this GameObject is the root of a prefab instance (not a child of one).</summary>
        private static bool IsPrefabInstanceRoot(GameObject go)
        {
            if (!PrefabUtility.IsPartOfPrefabInstance(go)) return false;
            return PrefabUtility.GetOutermostPrefabInstanceRoot(go) == go;
        }

        /// <summary>Asset path of the source prefab backing this instance, or null.</summary>
        private static string GetSourcePrefabPath(GameObject instance)
        {
            var source = PrefabUtility.GetCorrespondingObjectFromSource(instance);
            if (source == null) return null;
            return AssetDatabase.GetAssetPath(source);
        }
    }
}
