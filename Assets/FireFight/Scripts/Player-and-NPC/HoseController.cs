using UnityEngine;
using FireFight.Managers;

/// <summary>Spray patterns the hose can emit.</summary>
public enum SprayPattern { Jet, Cone }

/// <summary>
/// Drives one hose. When spraying, it feeds water into the fire simulation
/// every frame via <see cref="FireManager"/>:
///   - Jet:  a focused stream. Raycasts from the nozzle and splashes the
///           impact point (ExtinguishJet).
///   - Cone: a wide spray. Cools/wets a cone of cells in front of the nozzle
///           (ExtinguishCone).
///
/// The controller is "dumb": something else (player input or the NPC brain)
/// decides WHERE to aim and WHEN to spray by setting the aim direction and
/// calling StartSpray / StopSpray. This keeps the hose reusable for both the
/// player and NPCs.
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

    /// <summary>True while water is actively coming out.</summary>
    public bool IsSpraying { get; private set; }

    /// <summary>Optional override direction. If set, spray aims this way instead of nozzle.forward.</summary>
    private Vector3? aimDirectionOverride;

    private void Reset()
    {
        if (nozzlePoint == null) nozzlePoint = transform;
        if (fireManager == null) fireManager = FindFirstObjectByType<FireManager>();
    }

    private void Awake()
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
        if (!IsSpraying || fireManager == null) return;

        float cooling = coolingPerSecond * Time.deltaTime;
        float wetness = wetnessPerSecond * Time.deltaTime;
        if (cooling <= 0f && wetness <= 0f) return;

        Vector3 origin = Origin;
        Vector3 dir = Direction;

        switch (sprayPattern)
        {
            case SprayPattern.Jet:
            {
                // Water lands where the stream first hits something, or at max
                // range if it hits nothing.
                Vector3 impact = origin + dir * sprayRange;
                if (Physics.Raycast(origin, dir, out RaycastHit hit, sprayRange,
                        impactLayers, QueryTriggerInteraction.Ignore))
                    impact = hit.point;

                fireManager.ExtinguishJet(impact, cooling, wetness);
                break;
            }

            case SprayPattern.Cone:
            {
                fireManager.ExtinguishCone(origin, dir, sprayRange, coneAngle, cooling, wetness);
                break;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Transform t = nozzlePoint != null ? nozzlePoint : transform;
        Gizmos.color = IsSpraying ? Color.cyan : new Color(0f, 1f, 1f, 0.3f);
        Vector3 dir = aimDirectionOverride ?? t.forward;
        Gizmos.DrawRay(t.position, dir * sprayRange);
    }
}
