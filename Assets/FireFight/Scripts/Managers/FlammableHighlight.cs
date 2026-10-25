using UnityEngine;
using UnityEngine.Rendering;

namespace FireFight.Managers
{
    /// <summary>
    /// Play-mode "this can burn" cue for a <see cref="Flammable"/> object.
    ///
    /// <see cref="FireGizmoVisualizer"/> only draws in the Scene view, so it cannot help a
    /// trainee reading the scene in Game view / Play mode. This component draws a wireframe
    /// hazard box around the object using a <see cref="LineRenderer"/> whose material is a
    /// code-created, self-owned instance with emission enabled - which an MPB cannot do, and
    /// which makes the cue visible regardless of the base art's emission state.
    ///
    /// State: warm amber at size-up (resting), hot orange once the sibling ignites,
    /// charred grey (overlay off) once it burns out. Driven by self-subscribing to the
    /// sibling Flammable's events, so no author-time UnityEvent wiring ends up in the YAML.
    /// </summary>
    [RequireComponent(typeof(Flammable))]
    public class FlammableHighlight : MonoBehaviour
    {
        [Tooltip("Resting emissive colour shown at size-up so the object reads as 'burnable'.")]
        public Color sizeUpEmission = new Color(1.0f, 0.55f, 0.1f);

        [Tooltip("Emissive colour once the object is on fire.")]
        public Color burningEmission = new Color(1.0f, 0.25f, 0.0f);

        [Tooltip("Emissive colour once the object has fully burned out (overlay also disables).")]
        public Color burnedEmission = new Color(0.15f, 0.15f, 0.15f);

        [Tooltip("If true and a particle prefab is set, spawn it at the object's centre on ignite.")]
        public bool spawnParticleOnIgnite;

        [Tooltip("Pack smoke/fire particle spawned on ignite (assigned by the builder).")]
        public GameObject igniteParticlePrefab;

        private const string OverlayName = "_FlammableHighlight";

        private LineRenderer line;
        private Material ownedMaterial;
        private Flammable flammable;

        private void Awake()
        {
            flammable = GetComponent<Flammable>();

            if (!TryComputeBounds(out Bounds b))
            {
                Debug.LogWarning($"[FireFight] FlammableHighlight on '{name}' has no renderers; disabling cue.", this);
                enabled = false;
                return;
            }

            ownedMaterial = CreateEmissiveMaterial(sizeUpEmission);

            var overlay = new GameObject(OverlayName);
            overlay.transform.SetParent(transform, worldPositionStays: true);

            line = overlay.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = false;
            line.widthMultiplier = 0.06f;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.material = ownedMaterial;

            SetBoxCorners(b);
        }

        private void Start()
        {
            // Subscribe regardless of whether the sibling disabled itself: a disabled
            // MonoBehaviour's UnityEvent fields are still valid listener targets.
            if (flammable != null)
            {
                flammable.onIgnite?.AddListener(OnIgnite);
                flammable.onBurnedOut?.AddListener(OnBurned);
            }
        }

        private void OnIgnite()
        {
            if (ownedMaterial != null)
                ownedMaterial.SetColor("_EmissionColor", burningEmission);

            if (spawnParticleOnIgnite && igniteParticlePrefab != null)
            {
                if (TryComputeBounds(out Bounds b))
                {
                    var fx = Instantiate(igniteParticlePrefab, b.center, Quaternion.identity, transform);
                    var ps = fx.GetComponentInChildren<ParticleSystem>();
                    if (ps != null) ps.Play();
                }
            }
        }

        private void OnBurned()
        {
            if (ownedMaterial != null)
                ownedMaterial.SetColor("_EmissionColor", burnedEmission);
            if (line != null)
                line.enabled = false;
        }

        private void OnDestroy()
        {
            if (flammable != null)
            {
                flammable.onIgnite?.RemoveListener(OnIgnite);
                flammable.onBurnedOut?.RemoveListener(OnBurned);
            }
            if (ownedMaterial != null)
                Destroy(ownedMaterial);
        }

        /// <summary>Trace the 12 edges of the bounds box as a single continuous LineRenderer path.</summary>
        private void SetBoxCorners(Bounds b)
        {
            Vector3 c = b.center, e = b.extents;
            Vector3 p0 = c + new Vector3(-e.x, -e.y, -e.z);
            Vector3 p1 = c + new Vector3(e.x, -e.y, -e.z);
            Vector3 p2 = c + new Vector3(e.x, -e.y, e.z);
            Vector3 p3 = c + new Vector3(-e.x, -e.y, e.z);
            Vector3 p4 = c + new Vector3(-e.x, e.y, -e.z);
            Vector3 p5 = c + new Vector3(e.x, e.y, -e.z);
            Vector3 p6 = c + new Vector3(e.x, e.y, e.z);
            Vector3 p7 = c + new Vector3(-e.x, e.y, e.z);

            // One stroke that covers all 12 edges (some retraced - acceptable for a wire cage).
            Vector3[] path =
            {
                p0, p1, p2, p3, p0, // bottom loop
                p4, p5, p6, p7, p4, // top loop (via vertical p0->p4)
                p5, p1,             // vertical edge
                p2, p6,             // vertical edge
                p7, p3              // vertical edge
            };

            line.positionCount = path.Length;
            line.SetPositions(path);
        }

        private bool TryComputeBounds(out Bounds bounds)
        {
            var renderers = GetComponentsInChildren<Renderer>();
            // Ignore our own overlay renderer when recomputing.
            Bounds? acc = null;
            foreach (var r in renderers)
            {
                if (r.gameObject.name == OverlayName) continue;
                acc = acc.HasValue ? Encapsulate(acc.Value, r.bounds) : r.bounds;
            }
            if (acc.HasValue) { bounds = acc.Value; return true; }
            bounds = default;
            return false;
        }

        private static Bounds Encapsulate(Bounds a, Bounds b)
        {
            a.Encapsulate(b);
            return a;
        }

        private static Material CreateEmissiveMaterial(Color emission)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit")
                            ?? Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", emission);
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", emission);
            mat.color = emission;
            return mat;
        }
    }
}
