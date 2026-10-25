using UnityEngine;
using FireFight.Managers;
using FireFight.Environment;
using System.Collections.Generic;

/// <summary>Jet and cone hoses share the fire manager's existing cooling/wetness API.</summary>
public enum SprayPattern { Jet, Cone }

[DisallowMultipleComponent]
public class HoseController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform nozzlePoint;
    [SerializeField] private FireManager fireManager;
    [Header("Spray")]
    [SerializeField] private SprayPattern sprayPattern = SprayPattern.Jet;
    [SerializeField, Min(0f)] private float sprayRange = 8f;
    [SerializeField, Range(0f, 80f)] private float coneAngle = 25f;
    [SerializeField, Min(0f)] private float coolingPerSecond = 3f;
    [SerializeField, Min(0f)] private float wetnessPerSecond = 0.5f;
    [Tooltip("Overall water delivery. Raise this to cool and soak fires faster without changing fire physics.")]
    [SerializeField, Min(0.1f)] private float waterOutputMultiplier = 3f;
    [SerializeField] private LayerMask impactLayers = Physics.DefaultRaycastLayers;
    [SerializeField, Min(0.05f)] private float jetSplashRadius = 0.65f;
    // Retain the former prefab slots so existing assets do not lose serialized data.
    [SerializeField, HideInInspector] private GameObject streamVfxPrefab;
    [SerializeField, HideInInspector] private GameObject impactVfxPrefab;
    [SerializeField, HideInInspector] private Vector3 streamForwardAxis = Vector3.forward;

    private Vector3? aimDirectionOverride;
    private readonly RaycastHit[] rayHits = new RaycastHit[64];
    private Flammable[] receivers;
    private float refreshReceiversAt;
    private readonly HashSet<int> wettedCells = new HashSet<int>();
    public bool IsSpraying { get; private set; }
    public float SprayRange => sprayRange;
    public SprayPattern Pattern => sprayPattern;
    public float ConeHalfAngle => coneAngle;
    public Vector3 Origin => nozzlePoint != null ? nozzlePoint.position : transform.position;
    public Vector3 Direction => aimDirectionOverride ?? (nozzlePoint != null ? nozzlePoint.forward : transform.forward);
    public Vector3 ImpactPoint { get; private set; }
    public Vector3 ImpactNormal { get; private set; } = Vector3.up;
    public bool HasImpact { get; private set; }
    public float VisibleLength { get; private set; }

    private void Reset() => AutoWire();
    private void Awake()
    {
        AutoWire();
        if (GetComponent<HoseWaterVisuals>() == null) gameObject.AddComponent<HoseWaterVisuals>();
    }

    private void AutoWire()
    {
        if (nozzlePoint == null) nozzlePoint = transform;
        if (fireManager == null) fireManager = FindFirstObjectByType<FireManager>();
    }

    public void StartSpray() => IsSpraying = true;
    public void StopSpray() => IsSpraying = false;
    public void SetSpraying(bool spraying) => IsSpraying = spraying;

    public void AimAt(Vector3? worldTarget)
    {
        aimDirectionOverride = worldTarget.HasValue
            ? (worldTarget.Value - Origin).normalized : (Vector3?)null;
    }

    private void Update()
    {
        if (IsSpraying) TickSpray(Time.deltaTime);
    }

    /// <summary>Applies one spray step. Appearance and water delivery use the same range/angle.</summary>
    public void TickSpray(float deltaTime)
    {
        Vector3 origin = Origin;
        Vector3 direction = Direction;
        HasImpact = RaycastEnvironment(origin, direction, sprayRange, out RaycastHit hit);
        ImpactPoint = HasImpact ? hit.point : origin + direction * sprayRange;
        ImpactNormal = HasImpact ? hit.normal : -direction;
        VisibleLength = HasImpact ? hit.distance : sprayRange;
        if (!IsSpraying || deltaTime <= 0f || fireManager == null || fireManager.Grid == null) return;

        float cooling = coolingPerSecond * waterOutputMultiplier * deltaTime;
        float wetness = wetnessPerSecond * waterOutputMultiplier * deltaTime;
        if (cooling <= 0f && wetness <= 0f) return;
        if (sprayPattern == SprayPattern.Jet)
        {
            if (!HasImpact) return;
            // Deliver full jet strength to the struck cell; splash has a smaller footprint.
            Vector3Int struck = fireManager.Grid.WorldToGrid(hit.point + direction * 0.03f);
            fireManager.WetCell(struck, cooling, wetness);
            ApplyVolume(origin, direction, cooling, wetness, struck);
        }
        else ApplyVolume(origin, direction, cooling, wetness, null);
    }

    private void ApplyVolume(Vector3 origin, Vector3 direction, float cooling, float wetness, Vector3Int? struck)
    {
        FireGrid grid = fireManager.Grid;
        float radius = sprayPattern == SprayPattern.Cone ? sprayRange : jetSplashRadius;
        Vector3 center = sprayPattern == SprayPattern.Cone ? origin : ImpactPoint;
        if (receivers == null || Time.time >= refreshReceiversAt)
        {
            receivers = FindObjectsByType<Flammable>(FindObjectsSortMode.None);
            refreshReceiversAt = Time.time + 0.5f;
        }
        wettedCells.Clear();
        foreach (var receiver in receivers)
        {
            if (receiver == null || receiver.fireManager != fireManager ||
                !FireFight.NPCFireTarget.TryGetBounds(receiver, out Bounds bounds) ||
                bounds.SqrDistance(center) > radius * radius) continue;
            Collider body = receiver.GetComponentInChildren<Collider>();
            Vector3Int min = grid.WorldToGrid(bounds.min);
            Vector3Int max = grid.WorldToGrid(bounds.max);
            for (int x = Mathf.Max(0, min.x); x <= Mathf.Min(grid.Size.x - 1, max.x); x++)
            for (int y = Mathf.Max(0, min.y); y <= Mathf.Min(grid.Size.y - 1, max.y); y++)
            for (int z = Mathf.Max(0, min.z); z <= Mathf.Min(grid.Size.z - 1, max.z); z++)
            {
                var gp = new Vector3Int(x, y, z);
                if ((struck.HasValue && gp == struck.Value) || wettedCells.Contains(grid.Index(gp))) continue;
                FireCell cell = grid.GetCell(gp);
                if (cell.fuel <= 0f && cell.state != FireState.Burning) continue;
                // Boundary voxels can have their centres outside the mesh. Test the actual object surface.
                Vector3 point = body.ClosestPoint(grid.GridToWorld(gp));
                float distance = (point - center).magnitude;
                if (distance > radius || !InsideCone(origin, direction, point)) continue;
                Vector3 offset = point - origin;
                if (!RaycastEnvironment(origin, offset.normalized, offset.magnitude + 0.05f, out RaycastHit contact) ||
                    contact.collider.GetComponentInParent<Flammable>() != receiver) continue;
                float strength = 1f - distance / Mathf.Max(radius, 0.001f);
                // A pressure-fed cone should still deliver substantial water at working range.
                if (sprayPattern == SprayPattern.Cone) strength = Mathf.Lerp(0.65f, 1f, strength);
                fireManager.WetCell(gp, cooling * strength, wetness * strength);
                wettedCells.Add(grid.Index(gp));
            }
        }
    }

    private bool InsideCone(Vector3 origin, Vector3 direction, Vector3 point)
    {
        if (sprayPattern != SprayPattern.Cone) return true;
        Vector3 offset = point - origin;
        return offset.sqrMagnitude > 0.0001f &&
            Vector3.Dot(offset.normalized, direction) >= Mathf.Cos(coneAngle * Mathf.Deg2Rad);
    }

    private bool RaycastEnvironment(Vector3 origin, Vector3 direction, float distance, out RaycastHit closest)
    {
        closest = default;
        int count = Physics.RaycastNonAlloc(origin, direction, rayHits, distance, impactLayers,
            QueryTriggerInteraction.Ignore);
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            if (rayHits[i].collider.transform.IsChildOf(transform) || rayHits[i].distance >= nearest) continue;
            closest = rayHits[i];
            nearest = closest.distance;
        }
        return !float.IsPositiveInfinity(nearest);
    }

    /// <summary>Lets the water cone end at the same solid surfaces that block water delivery.</summary>
    public float GetVisualReach(Vector3 direction)
    {
        return RaycastEnvironment(Origin, direction.normalized, sprayRange, out RaycastHit hit)
            ? hit.distance : sprayRange;
    }

    private void OnDisable() => StopSpray();

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = IsSpraying ? Color.cyan : new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawRay(Origin, Direction * sprayRange);
    }
}
