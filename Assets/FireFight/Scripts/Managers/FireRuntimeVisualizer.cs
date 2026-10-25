using System.Collections.Generic;
using UnityEngine;
using FireFight.Environment;

namespace FireFight.Managers
{
    /// <summary>
    /// Renders the fire grid in the ACTUAL GAME (Game view and builds), not just the Scene
    /// view. <see cref="FireGizmoVisualizer"/> uses Gizmos, which only draw in the editor
    /// Scene view; this component draws real, camera-visible geometry so a player sees the
    /// fire, heat shimmer, and smoke.
    ///
    /// Implementation: one GPU-instanced billboard quad per visible cell via
    /// <see cref="Graphics.DrawMeshInstanced"/>. No per-cell GameObjects are created (nothing
    /// to leak or GC-thrash), and it is render-pipeline agnostic because it builds its own
    /// transparent, emissive materials in code. It only READS the grid - never mutates it -
    /// so simulation and rendering stay decoupled, exactly like the gizmo version.
    ///
    /// Attach to the same GameObject as <see cref="FireManager"/>.
    /// </summary>
    [RequireComponent(typeof(FireManager))]
    public class FireRuntimeVisualizer : MonoBehaviour
    {
        [Header("What to draw")]
        public bool showFire = true;
        public bool showHeat = true;
        public bool showSmoke = true;

        [Tooltip("Smoke below this density is not drawn (reduces clutter / overdraw).")]
        [Range(0f, 0.5f)] public float smokeDrawThreshold = 0.05f;

        [Header("Look")]
        [Tooltip("Quad size relative to a grid cell (1 = full cell).")]
        [Range(0.2f, 2f)] public float quadScale = 1.1f;

        [Tooltip("Flame flicker speed. 0 = steady.")]
        public float flickerSpeed = 8f;

        [Tooltip("Flame flicker amount (fraction of size). 0 = no flicker.")]
        [Range(0f, 0.6f)] public float flickerAmount = 0.25f;

        [Tooltip("Colour of an actively burning cell.")]
        public Color fireColor = new Color(1f, 0.35f, 0.05f);

        [Tooltip("Colour smoke renders as.")]
        public Color smokeColor = new Color(0.2f, 0.2f, 0.2f);

        private FireManager manager;
        private Camera targetCamera;

        private Mesh quad;
        private Material fireMaterial;   // additive, emissive - for flames + heat
        private Material smokeMaterial;  // alpha-blended grey - for smoke

        // Reused each frame; instancing is capped at 1023 matrices per draw call.
        private const int BatchMax = 1023;
        private readonly List<Matrix4x4> fireBatch = new List<Matrix4x4>(BatchMax);
        private readonly List<Vector4> fireColors = new List<Vector4>(BatchMax);
        private readonly List<Matrix4x4> smokeBatch = new List<Matrix4x4>(BatchMax);
        private readonly List<Vector4> smokeColors = new List<Vector4>(BatchMax);
        private MaterialPropertyBlock mpb;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private void Awake()
        {
            manager = GetComponent<FireManager>();
            quad = BuildQuad();
            fireMaterial = BuildFireMaterial();
            smokeMaterial = BuildSmokeMaterial();
            mpb = new MaterialPropertyBlock();
        }

        private void OnDestroy()
        {
            if (quad != null) Destroy(quad);
            if (fireMaterial != null) Destroy(fireMaterial);
            if (smokeMaterial != null) Destroy(smokeMaterial);
        }

        private void LateUpdate()
        {
            FireGrid grid = manager != null ? manager.Grid : null;
            if (grid == null) return; // grid only exists in Play mode

            targetCamera = Camera.main != null ? Camera.main : Camera.current;
            // Billboard toward the camera if we have one; otherwise face up.
            Quaternion facing = targetCamera != null
                ? Quaternion.LookRotation(-targetCamera.transform.forward, Vector3.up)
                : Quaternion.Euler(90f, 0f, 0f);

            float cell = grid.CellSize;
            float baseSize = cell * quadScale;
            float ignition = Mathf.Max(0.0001f, manager.config.ignitionThreshold);
            float flicker = flickerAmount > 0f
                ? 1f + Mathf.Sin(Time.time * flickerSpeed) * flickerAmount
                : 1f;

            fireBatch.Clear(); fireColors.Clear();
            smokeBatch.Clear(); smokeColors.Clear();

            FireCell[] cells = grid.Cells;
            for (int i = 0; i < cells.Length; i++)
            {
                FireCell c = cells[i];
                Vector3 world = grid.GridToWorld(c.gridPosition);

                // ---- Smoke (drawn by its own material) ----
                if (showSmoke && c.smoke >= smokeDrawThreshold)
                {
                    float a = Mathf.Clamp01(c.smoke) * 0.7f;
                    AddInstance(smokeBatch, smokeColors,
                        world, facing, baseSize,
                        new Vector4(smokeColor.r, smokeColor.g, smokeColor.b, a));
                }

                // ---- Fire / heat (emissive material) ----
                if (c.state == FireState.Burning)
                {
                    if (!showFire) continue;
                    Color col = fireColor;
                    AddInstance(fireBatch, fireColors,
                        world, facing, baseSize * flicker,
                        new Vector4(col.r, col.g, col.b, 1f));
                }
                else if (showHeat && c.state == FireState.Unburned && c.heat > 0f)
                {
                    // Pre-ignition glow: yellow -> fire colour as heat approaches ignition.
                    float t = Mathf.Clamp01(c.heat / ignition);
                    Color col = Color.Lerp(new Color(1f, 0.9f, 0.2f), fireColor, t);
                    AddInstance(fireBatch, fireColors,
                        world, facing, baseSize * (0.5f + 0.5f * t),
                        new Vector4(col.r, col.g, col.b, Mathf.Lerp(0.3f, 1f, t)));
                }
            }

            FlushBatches();
        }

        /// <summary>Queue one billboarded instance, flushing automatically at the 1023 cap.</summary>
        private void AddInstance(List<Matrix4x4> batch, List<Vector4> colors,
            Vector3 pos, Quaternion rot, float size, Vector4 color)
        {
            batch.Add(Matrix4x4.TRS(pos, rot, Vector3.one * size));
            colors.Add(color);
            if (batch.Count >= BatchMax) FlushBatches();
        }

        private void FlushBatches()
        {
            if (fireBatch.Count > 0)
            {
                mpb.Clear();
                mpb.SetVectorArray("_InstanceColors", fireColors);
                // Set a representative emission/base so even non-instanced-color shaders glow.
                mpb.SetColor(EmissionColorId, fireColor);
                mpb.SetColor(BaseColorId, fireColor);
                mpb.SetColor(ColorId, fireColor);
                Graphics.DrawMeshInstanced(quad, 0, fireMaterial, fireBatch, mpb);
                fireBatch.Clear(); fireColors.Clear();
            }

            if (smokeBatch.Count > 0)
            {
                mpb.Clear();
                mpb.SetColor(BaseColorId, smokeColor);
                mpb.SetColor(ColorId, smokeColor);
                Graphics.DrawMeshInstanced(quad, 0, smokeMaterial, smokeBatch, mpb);
                smokeBatch.Clear(); smokeColors.Clear();
            }
        }

        // ---- Resource construction (self-owned, destroyed in OnDestroy) ----

        private static Mesh BuildQuad()
        {
            var m = new Mesh { name = "FireRuntimeQuad" };
            float h = 0.5f;
            m.vertices = new[]
            {
                new Vector3(-h, -h, 0f), new Vector3(h, -h, 0f),
                new Vector3(h,  h, 0f), new Vector3(-h, h, 0f),
            };
            m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
            m.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Additive, emissive, unlit material so flames glow regardless of scene lighting.</summary>
        private Material BuildFireMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                            ?? Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader) { name = "FireRuntimeFireMat", enableInstancing = true };
            ConfigureTransparent(mat, additive: true);
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty(EmissionColorId)) mat.SetColor(EmissionColorId, fireColor);
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, fireColor);
            if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, fireColor);
            return mat;
        }

        /// <summary>Alpha-blended unlit material for smoke.</summary>
        private Material BuildSmokeMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                            ?? Shader.Find("Universal Render Pipeline/Unlit")
                            ?? Shader.Find("Sprites/Default");
            var mat = new Material(shader) { name = "FireRuntimeSmokeMat", enableInstancing = true };
            ConfigureTransparent(mat, additive: false);
            if (mat.HasProperty(BaseColorId)) mat.SetColor(BaseColorId, smokeColor);
            if (mat.HasProperty(ColorId)) mat.SetColor(ColorId, smokeColor);
            return mat;
        }

        /// <summary>Set standard URP/built-in transparent blend + no depth write + no cull.</summary>
        private static void ConfigureTransparent(Material mat, bool additive)
        {
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);     // URP: transparent
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", additive ? 1f : 0f);
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);

            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", additive
                ? (int)UnityEngine.Rendering.BlendMode.One
                : (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWriteInt", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
        }
    }
}
