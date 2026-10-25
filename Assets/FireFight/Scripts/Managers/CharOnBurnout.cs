using System.Collections.Generic;
using UnityEngine;

namespace FireFight.Managers
{
    /// <summary>
    /// Makes an object look charred once it has fully burned out.
    ///
    /// Subscribes to the sibling <see cref="Flammable.onBurnedOut"/> event and, when it fires,
    /// swaps every renderer's materials for darkened, matte "charred" instances (near-black
    /// albedo, no metallic/smoothness, no emission). The original shared materials are never
    /// modified - we create per-renderer instances and restore them on destroy - so the source
    /// art assets stay clean.
    ///
    /// This is purely cosmetic and reads nothing from the simulation beyond the one event, so
    /// it stays decoupled like the other visual components. Pair it with a <see cref="Flammable"/>
    /// (the auto-tagger adds both).
    /// </summary>
    [RequireComponent(typeof(Flammable))]
    public class CharOnBurnout : MonoBehaviour
    {
        [Tooltip("How dark the surface becomes (0 = untouched, 1 = near black soot).")]
        [Range(0f, 1f)] public float charAmount = 0.85f;

        [Tooltip("Tint multiplied into the darkened albedo (slightly warm soot reads better than pure grey).")]
        public Color charTint = new Color(0.12f, 0.1f, 0.09f);

        [Tooltip("If true, char immediately if the object is already burned out when this starts " +
                 "(e.g. component added after the fact).")]
        public bool charIfAlreadyBurned = true;

        private Flammable flammable;
        private bool charred;

        // Per-renderer swap bookkeeping so we can restore + clean up our owned instances.
        private readonly List<Renderer> affected = new List<Renderer>();
        private readonly List<Material[]> originalShared = new List<Material[]>();
        private readonly List<Material[]> ownedCharred = new List<Material[]>();

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int GlossinessId = Shader.PropertyToID("_Glossiness");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            flammable = GetComponent<Flammable>();
        }

        private void Start()
        {
            if (flammable == null) flammable = GetComponent<Flammable>();
            if (flammable == null)
            {
                // No sibling Flammable: nothing can tell us when to char.
                enabled = false;
                return;
            }

            // onBurnedOut is initialised on Flammable, but guard anyway so a null
            // event (e.g. from an unusual add order) can never throw here.
            flammable.onBurnedOut?.AddListener(ApplyChar);

            if (charIfAlreadyBurned && flammable.burnedOut)
                ApplyChar();
        }

        private void OnDestroy()
        {
            if (flammable != null)
                flammable.onBurnedOut?.RemoveListener(ApplyChar);
            RestoreAndCleanup();
        }

        /// <summary>Swap every renderer on this object to a charred material instance.</summary>
        public void ApplyChar()
        {
            if (charred) return;
            charred = true;

            var renderers = GetComponentsInChildren<Renderer>();
            foreach (var r in renderers)
            {
                if (r == null) continue;
                // Skip overlay renderers created by other visual components (e.g. the highlight cage).
                if (r.gameObject.name.StartsWith("_")) continue;

                Material[] shared = r.sharedMaterials;
                var charredSet = new Material[shared.Length];
                for (int i = 0; i < shared.Length; i++)
                    charredSet[i] = MakeCharred(shared[i]);

                affected.Add(r);
                originalShared.Add(shared);
                ownedCharred.Add(charredSet);

                r.sharedMaterials = charredSet;
            }
        }

        /// <summary>Clone a material and darken it to soot; kills shine and emission.</summary>
        private Material MakeCharred(Material source)
        {
            // Empty material slot: nothing to darken, and fabricating one needs a shader
            // that may not exist in a build. Return null so the slot stays as-is.
            if (source == null) return null;
            if (source.shader == null) return source; // can't safely clone; leave original

            var clone = new Material(source) { name = source.name + "_Charred" };

            // Darken whatever colour property the shader uses.
            DarkenColor(clone, BaseColorId);
            DarkenColor(clone, ColorId);

            // Soot is matte: no metal, no gloss.
            if (clone.HasProperty(MetallicId)) clone.SetFloat(MetallicId, 0f);
            if (clone.HasProperty(SmoothnessId)) clone.SetFloat(SmoothnessId, 0.05f);
            if (clone.HasProperty(GlossinessId)) clone.SetFloat(GlossinessId, 0.05f);

            // Kill any emission so a formerly glowing object goes dead black.
            if (clone.HasProperty(EmissionColorId))
            {
                clone.SetColor(EmissionColorId, Color.black);
                clone.DisableKeyword("_EMISSION");
                clone.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }

            return clone;
        }

        private void DarkenColor(Material mat, int propId)
        {
            if (!mat.HasProperty(propId)) return;
            Color c = mat.GetColor(propId);
            // Lerp the existing colour toward the soot tint by charAmount.
            Color charred = Color.Lerp(c, charTint, charAmount);
            charred.a = c.a; // preserve transparency
            mat.SetColor(propId, charred);
        }

        private void RestoreAndCleanup()
        {
            for (int i = 0; i < affected.Count; i++)
            {
                Material[] original = originalShared[i];
                if (affected[i] != null && original != null)
                    affected[i].sharedMaterials = original;

                // Destroy only the clones WE created - never a null slot or a
                // pass-through of an original shared material (that would delete art).
                Material[] charredSet = ownedCharred[i];
                if (charredSet == null) continue;
                for (int j = 0; j < charredSet.Length; j++)
                {
                    Material m = charredSet[j];
                    if (m == null) continue;
                    bool isOriginal = original != null && j < original.Length && original[j] == m;
                    if (!isOriginal) Destroy(m);
                }
            }
            affected.Clear();
            originalShared.Clear();
            ownedCharred.Clear();
        }
    }
}
