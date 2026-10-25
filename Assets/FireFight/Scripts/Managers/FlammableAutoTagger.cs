using System;
using System.Collections.Generic;
using UnityEngine;

namespace FireFight.Managers
{
    /// <summary>
    /// Drop this on a parent (e.g. the scene root or a "Props" container) and it adds a
    /// <see cref="Flammable"/> component to every burnable child at startup, so fire can
    /// spread from object to object without hand-tagging each one in the Inspector.
    ///
    /// How it decides what burns:
    ///   1. The child must have a Renderer (visible art) and a Collider (so its fuel volume
    ///      has a real size - Flammable sizes its fuel from collider/renderer bounds).
    ///   2. Its name must NOT contain any <see cref="excludeNameContains"/> term (ground,
    ///      roads, the player, NPCs, etc. are skipped).
    ///   3. If <see cref="onlyNameContains"/> is non-empty, the name MUST contain one of those
    ///      terms (use this to burn ONLY a specific set).
    ///
    /// Per-object flammability can be nudged by name via <see cref="highFlammabilityContains"/>
    /// (oil, fuel, wood...) so an oil tank lights far more readily than a metal generator.
    ///
    /// This is intentionally small and runtime-only: it is NOT the old procedural scene builder.
    /// It only attaches the Flammable bridge; it never moves, creates, or deletes objects.
    /// </summary>
    [DisallowMultipleComponent]
    public class FlammableAutoTagger : MonoBehaviour
    {
        [Header("Fire Manager")]
        [Tooltip("The fire system tagged objects register with. If empty, each Flammable finds one in the scene.")]
        public FireManager fireManager;

        [Header("What counts as burnable")]
        [Tooltip("Require a Collider as well as a Renderer. Strongly recommended: Flammable needs a " +
                 "real volume, and fire only bridges object-to-object when fuel volumes are solid.")]
        public bool requireCollider = true;

        [Tooltip("If an object has a Renderer but no Collider, add a BoxCollider sized to its " +
                 "renderer bounds so it can be tagged and gain a fuel volume. Needed for plain " +
                 "mesh props (keyboards, books, toys) that were imported without colliders.")]
        public bool addColliderIfMissing = true;

        [Tooltip("Skip any object whose name contains one of these (case-insensitive). " +
                 "Ground, roads, fences, the player and NPCs should never burn.")]
        public string[] excludeNameContains =
        {
            "road", "floor", "ground", "fence", "wall", "player", "npc",
            "camera", "light", "hangar", "navmesh", "firesystem",
        };

        [Tooltip("If non-empty, ONLY objects whose name contains one of these are tagged. " +
                 "Leave empty to tag everything that isn't excluded.")]
        public string[] onlyNameContains = Array.Empty<string>();

        [Header("Fuel defaults")]
        [Tooltip("Starting fuel for tagged objects (0..1). Higher = burns longer.")]
        [Range(0f, 1f)] public float defaultFuel = 1f;

        [Tooltip("Base flammability for tagged objects (0..1).")]
        [Range(0f, 1f)] public float defaultFlammability = 0.5f;

        [Tooltip("Objects whose name contains one of these get boosted flammability (catch fast).")]
        public string[] highFlammabilityContains = { "oil", "fuel", "gas", "paper", "wood", "palet", "bag", "plant" };

        [Tooltip("Flammability used for the 'high' set above (0..1).")]
        [Range(0f, 1f)] public float highFlammability = 0.9f;

        [Header("Debug")]
        [Tooltip("Log how many objects were tagged.")]
        public bool logResult = true;

        [Header("Auto-ignite (demo)")]
        [Tooltip("After tagging, automatically set fire to ONE tagged object so you can watch " +
                 "it spread without pressing a key. Ignition is deferred one frame so every " +
                 "Flammable has painted its fuel first (otherwise the cell has no fuel to light).")]
        public bool autoIgniteFirst = true;

        // Remembered to ignite on the first Update (after all Flammable.Start paints fuel).
        private Flammable ignitionSeed;
        private bool ignited;

        private void Awake()
        {
            int tagged = TagChildren();
            if (logResult)
                Debug.Log($"[FireFight] FlammableAutoTagger on '{name}' tagged {tagged} burnable object(s).", this);
        }

        private void Update()
        {
            if (ignited || !autoIgniteFirst) return;
            ignited = true; // only attempt once

            FireManager mgr = fireManager != null ? fireManager : FindFirstObjectByType<FireManager>();
            if (mgr == null || ignitionSeed == null)
            {
                if (ignitionSeed == null && autoIgniteFirst)
                    Debug.LogWarning($"[FireFight] FlammableAutoTagger on '{name}': nothing was tagged, " +
                                     "so there is nothing to auto-ignite.", this);
                return;
            }

            // Ignite the seed's volume directly (same approach SeedIgniter uses). By the first
            // Update every Flammable.Start has painted fuel, so these cells are now ignitable.
            if (TryComputeBounds(ignitionSeed.gameObject, out Bounds b))
            {
                mgr.IgniteBounds(b);
                if (logResult)
                    Debug.Log($"[FireFight] Auto-ignited '{ignitionSeed.name}' as the demo fire source.", this);
            }
        }

        /// <summary>
        /// Walk every descendant and attach a Flammable to each one that qualifies. Returns the
        /// number tagged. Safe to call once (Awake); re-running is a no-op for already-tagged
        /// objects because Flammable is DisallowMultipleComponent-free but we check first.
        /// </summary>
        public int TagChildren()
        {
            int count = 0;
            var renderers = GetComponentsInChildren<Renderer>(includeInactive: true);

            // Track which GameObjects we've already handled so multi-renderer objects
            // (a prefab with several mesh parts) only get ONE Flammable, on the prefab root.
            var handled = new HashSet<GameObject>();

            foreach (var rend in renderers)
            {
                GameObject target = ResolveTagTarget(rend.gameObject);
                if (target == null || handled.Contains(target)) continue;
                handled.Add(target);

                // Name/skip filtering first (cheap, and avoids adding colliders to excluded art).
                if (!PassesNameFilter(target)) continue;

                // Ensure the object has a Collider so (a) it qualifies and (b) Flammable can
                // size a solid fuel volume that fire can bridge between.
                Collider col = target.GetComponentInChildren<Collider>();
                if (col == null && addColliderIfMissing)
                    col = AddBoxColliderFromRenderers(target);

                if (requireCollider && col == null) continue;

                if (target.GetComponent<Flammable>() != null) continue; // already tagged

                var flammable = target.AddComponent<Flammable>();
                flammable.fireManager = fireManager;
                flammable.fuel = defaultFuel;
                flammable.flammability = IsHighFlammability(target.name) ? highFlammability : defaultFlammability;
                count++;

                // Remember the first high-flammability object (else the first tagged) as the
                // demo ignition source, so auto-ignite lights something that catches readily.
                if (ignitionSeed == null || IsHighFlammability(target.name))
                    ignitionSeed = flammable;
            }
            return count;
        }

        /// <summary>Combined world-space renderer bounds of a GameObject (for ignition volume).</summary>
        private static bool TryComputeBounds(GameObject go, out Bounds bounds)
        {
            var col = go.GetComponentInChildren<Collider>();
            if (col != null) { bounds = col.bounds; return true; }

            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
                return true;
            }
            bounds = default;
            return false;
        }

        /// <summary>Add a trigger-free BoxCollider sized to the object's combined renderer bounds.</summary>
        private static Collider AddBoxColliderFromRenderers(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return null;

            Bounds worldBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) worldBounds.Encapsulate(renderers[i].bounds);

            var box = go.AddComponent<BoxCollider>();
            // BoxCollider center/size are in the object's LOCAL space; convert world bounds.
            box.center = go.transform.InverseTransformPoint(worldBounds.center);
            Vector3 lossy = go.transform.lossyScale;
            box.size = new Vector3(
                lossy.x != 0f ? worldBounds.size.x / Mathf.Abs(lossy.x) : worldBounds.size.x,
                lossy.y != 0f ? worldBounds.size.y / Mathf.Abs(lossy.y) : worldBounds.size.y,
                lossy.z != 0f ? worldBounds.size.z / Mathf.Abs(lossy.z) : worldBounds.size.z);
            return box;
        }

        /// <summary>
        /// Prefer to tag the nearest ancestor that looks like a prefab root (the object that
        /// carries the collider), so a multi-part model gets a single Flammable covering it all.
        /// Falls back to the renderer's own GameObject.
        /// </summary>
        private GameObject ResolveTagTarget(GameObject rendererGo)
        {
            // If a collider exists up the chain, tag that object (its bounds cover the whole model).
            var col = rendererGo.GetComponentInParent<Collider>();
            if (col != null) return col.gameObject;
            return rendererGo;
        }

        private bool PassesNameFilter(GameObject go)
        {
            if (go == gameObject) return false; // never tag the tagger's own object

            string n = go.name.ToLowerInvariant();

            foreach (var ex in excludeNameContains)
                if (!string.IsNullOrEmpty(ex) && n.Contains(ex.ToLowerInvariant()))
                    return false;

            if (onlyNameContains != null && onlyNameContains.Length > 0)
            {
                bool matched = false;
                foreach (var inc in onlyNameContains)
                    if (!string.IsNullOrEmpty(inc) && n.Contains(inc.ToLowerInvariant())) { matched = true; break; }
                if (!matched) return false;
            }

            return true;
        }

        private bool IsHighFlammability(string objectName)
        {
            string n = objectName.ToLowerInvariant();
            foreach (var term in highFlammabilityContains)
                if (!string.IsNullOrEmpty(term) && n.Contains(term.ToLowerInvariant()))
                    return true;
            return false;
        }
    }
}
