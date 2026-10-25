using UnityEngine;
using FireFight.Managers;

/// <summary>Spray patterns the hose can emit.</summary>
public enum SprayPattern { Jet, Cone }

/// <summary>
/// Drives one hose: it both FEEDS WATER into the fire simulation and SHOWS the water.
///
/// Simulation (always): while spraying it cools + wets cells via <see cref="FireManager"/>.
///   - Jet:  a focused stream that splashes the impact point (ExtinguishJet).
///   - Cone: a wide spray over a cone of cells (ExtinguishCone).
///
/// Visuals (optional): if a stream / impact particle prefab is assigned (e.g. NamuFX
/// Water_Beam / Water_Impact), the hose spawns them once, aims the stream down the spray
/// direction, parks the impact burst at the point the stream lands, and toggles them with
/// spraying. The impact point is computed ONCE per frame and shared by the sim and the
/// visuals so what you see is exactly what cools the fire.
///
/// The controller is "dumb": something else (player input or the NPC brain) decides WHERE to
/// aim and WHEN to spray via <see cref="AimAt"/> + <see cref="StartSpray"/>/<see cref="StopSpray"/>.
/// So the player and every NPC share one behaviour and one set of water visuals.
/// </summary>
[DisallowMultipleComponent]
public class HoseController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Where the water comes out. Spray originates here and points along its forward axis.")]
    [SerializeField] private Transform nozzlePoint;

    [Tooltip("The fire simulation this hose pours water into.")]
    [SerializeField] private FireManager fireManager;

    [Header("Spray")]
    [SerializeField] private SprayPattern sprayPattern = SprayPattern.Jet;

    [Tooltip("How far the water reaches, in world units.")]
    [SerializeField, Min(0f)] private float sprayRange = 8f;

    [Tooltip("Half-angle of the cone spray, in degrees (Cone pattern only).")]
    [SerializeField, Min(0f)] private float coneAngle = 15f;

    [Tooltip("Heat removed per second at full strength.")]
    [SerializeField, Min(0f)] private float coolingPerSecond = 3f;

    [Tooltip("Wetness added per second so cells resist re-igniting after the spray.")]
    [SerializeField, Min(0f)] private float wetnessPerSecond = 0.5f;

    [Tooltip("Layers the jet stream can hit (walls, floors, flammables). Water lands at the first hit.")]
    [SerializeField] private LayerMask impactLayers = Physics.DefaultRaycastLayers;

    [Header("Water VFX (optional)")]
    [Tooltip("Particle prefab for the water STREAM (e.g. NamuFX Water_Beam). Spawned once, " +
             "aimed down the spray direction while spraying. Leave empty for no stream visual.")]
    [SerializeField] private GameObject streamVfxPrefab;

    [Tooltip("Particle prefab for the IMPACT burst where the water lands (e.g. NamuFX " +
             "Water_Impact / Water_Splash). Parked at the impact point while spraying.")]
    [SerializeField] private GameObject impactVfxPrefab;

    [Tooltip("Local axis of the stream prefab that points 'forward' along the beam. Most NamuFX " +
             "beams fire along +Z; set to +Y or +X if your prefab emits along a different axis.")]
    [SerializeField] private Vector3 streamForwardAxis = Vector3.forward;

    /// <summary>True while water is actively coming out.</summary>
    public bool IsSpraying { get; private set; }

    /// <summary>Optional override direction. If set, spray aims this way instead of nozzle.forward.</summary>
    private Vector3? aimDirectionOverride;

    // Spawned VFX instances (created lazily on first spray).
    private GameObject streamInstance;
    private GameObject impactInstance;

    private void Reset() => AutoWire();
    private void Awake() => AutoWire();

    private void AutoWire()
    {
        if (nozzlePoint == null) nozzlePoint = transform;
        if (fireManager == null) fireManager = FindFirstObjectByType<FireManager>();
    }

    /// <summary>Begin spraying. Safe to call every frame.</summary>
    public void StartSpray() => IsSpraying = true;

    /// <summary>Stop spraying.</summary>
    public void StopSpray() => IsSpraying = false;

    /// <summary>Convenience toggle for input bindings.</summary>
    public void SetSpraying(bool spraying) => IsSpraying = spraying;

    /// <summary>
    /// Point the spray at a world position (e.g. the NPC aims at a fire).
    /// Pass null to clear and fall back to the nozzle's own forward direction.
    /// </summary>
    public void AimAt(Vector3? worldTarget)
    {
        if (worldTarget.HasValue && nozzlePoint != null)
            aimDirectionOverride = (worldTarget.Value - nozzlePoint.position).normalized;
        else
            aimDirectionOverride = null;
    }

    /// <summary>Origin of the spray this frame.</summary>
    public Vector3 Origin => nozzlePoint != null ? nozzlePoint.position : transform.position;

    /// <summary>Direction the spray travels this frame.</summary>
    public Vector3 Direction =>
        aimDirectionOverride ?? (nozzlePoint != null ? nozzlePoint.forward : transform.forward);

    private void Update()
    {
        if (!IsSpraying)
        {
            SetVfxActive(false);
            return;
        }

        Vector3 origin = Origin;
        Vector3 dir = Direction;

        // Compute where the stream lands ONCE, then use it for both sim and visuals.
        Vector3 impact = ComputeImpactPoint(origin, dir, out float distance);

        ApplyWaterToSim(origin, dir, impact);
        UpdateVfx(origin, dir, impact, distance);
    }

    /// <summary>Raycast the stream; returns the first hit or the max-range point if it hits nothing.</summary>
    private Vector3 ComputeImpactPoint(Vector3 origin, Vector3 dir, out float distance)
    {
        if (Physics.Raycast(origin, dir, out RaycastHit hit, sprayRange,
                impactLayers, QueryTriggerInteraction.Ignore))
        {
            distance = hit.distance;
            return hit.point;
        }
        distance = sprayRange;
        return origin + dir * sprayRange;
    }

    /// <summary>Pour this frame's water into the fire simulation.</summary>
    private void ApplyWaterToSim(Vector3 origin, Vector3 dir, Vector3 impact)
    {
        if (fireManager == null) return;

        float cooling = coolingPerSecond * Time.deltaTime;
        float wetness = wetnessPerSecond * Time.deltaTime;
        if (cooling <= 0f && wetness <= 0f) return;

        if (sprayPattern == SprayPattern.Cone)
            fireManager.ExtinguishCone(origin, dir, sprayRange, coneAngle, cooling, wetness);
        else
            fireManager.ExtinguishJet(impact, cooling, wetness);
    }

    // ---- Water visuals ----

    private void UpdateVfx(Vector3 origin, Vector3 dir, Vector3 impact, float distance)
    {
        EnsureVfxSpawned();
        SetVfxActive(true);

        if (streamInstance != null)
        {
            streamInstance.transform.position = origin;
            // Rotate so the prefab's configured forward axis points down the spray.
            streamInstance.transform.rotation = Quaternion.FromToRotation(streamForwardAxis, dir)
                                                * Quaternion.identity;
        }

        if (impactInstance != null)
            impactInstance.transform.position = impact;
    }

    private void EnsureVfxSpawned()
    {
        if (streamInstance == null && streamVfxPrefab != null)
        {
            streamInstance = Instantiate(streamVfxPrefab, Origin, Quaternion.identity, transform);
            streamInstance.SetActive(false);
        }
        if (impactInstance == null && impactVfxPrefab != null)
        {
            impactInstance = Instantiate(impactVfxPrefab, Origin, Quaternion.identity);
            impactInstance.SetActive(false);
        }
    }

    private void SetVfxActive(bool active)
    {
        if (streamInstance != null && streamInstance.activeSelf != active)
            streamInstance.SetActive(active);
        if (impactInstance != null && impactInstance.activeSelf != active)
            impactInstance.SetActive(active);
    }

    private void OnDestroy()
    {
        if (streamInstance != null) Destroy(streamInstance);
        if (impactInstance != null) Destroy(impactInstance);
    }

    private void OnDrawGizmosSelected()
    {
        Transform t = nozzlePoint != null ? nozzlePoint : transform;
        Gizmos.color = IsSpraying ? Color.cyan : new Color(0f, 1f, 1f, 0.3f);
        Vector3 dir = aimDirectionOverride ?? t.forward;
        Gizmos.DrawRay(t.position, dir * sprayRange);
    }
}
