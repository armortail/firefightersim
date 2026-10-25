using UnityEngine;
using UnityEngine.AI;
using FireFight;
using FireFight.Managers;

/// <summary>Moves one NPC and runs an explicitly commanded firefighting job.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class NPCMovement : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float destinationSnapDistance = 0.5f;
    [Header("Firefighting")]
    [SerializeField] private HoseController hose;
    [SerializeField, Min(0.5f)] private float extinguishStandoff = 4f;
    [SerializeField] private FireManager fireManager;
    [SerializeField, Min(0.5f)] private float fireWorkRadius = 2.5f;
    [SerializeField, Min(0f)] private float postExtinguishSoakSeconds = 1.5f;

    private NavMeshAgent agent;
    private NavMeshPath commandPath;
    private bool extinguishing;
    private Flammable burningObject;
    private bool tracksObject;
    private Vector3 fireTarget;
    private float nextRepathTime;
    private Vector3 fireZoneCenter;
    private float soakUntil;
    private readonly RaycastHit[] sightHits = new RaycastHit[32];

    public bool IsExtinguishing => extinguishing;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        commandPath = new NavMeshPath();
        if (hose == null) hose = GetComponentInChildren<HoseController>();
        if (fireManager == null) fireManager = FindFirstObjectByType<FireManager>();
    }

    public bool MoveTo(Vector3 targetPosition)
    {
        // Reject an invalid marker without cancelling a valid current job.
        if (!SetDestination(targetPosition)) return false;
        EndFireJob(false);
        return true;
    }

    public bool ExtinguishAt(Flammable target)
    {
        if (hose == null || !NPCFireTarget.TryGetBurningPoint(target, hose.Origin, out Vector3 point))
            return false;
        if (!BeginApproach(point, target)) return false;
        burningObject = target;
        tracksObject = true;
        fireManager = target.fireManager;
        fireTarget = point;
        fireZoneCenter = point;
        soakUntil = 0f;
        extinguishing = true;
        nextRepathTime = Time.time + 0.5f;
        hose.StopSpray();
        return true;
    }

    // Retain the point API for existing callers, but require real burning cells.
    public bool ExtinguishAt(Vector3 point)
    {
        if (fireManager == null || fireManager.Grid == null || hose == null ||
            !fireManager.IsAnyBurningInBounds(PointBounds(point)) || !BeginApproach(point, null))
            return false;
        burningObject = null;
        tracksObject = false;
        fireTarget = point;
        extinguishing = true;
        nextRepathTime = Time.time + 0.5f;
        hose.StopSpray();
        return true;
    }

    private bool AgentReady => isActiveAndEnabled && agent != null &&
        agent.isActiveAndEnabled && agent.isOnNavMesh;

    private bool BeginApproach(Vector3 point, Flammable target)
    {
        if (!AgentReady || hose == null || !hose.isActiveAndEnabled || hose.SprayRange <= 0.5f)
            return false;
        if (WithinSprayDistance(point) && ClearSprayLine(hose.Origin, point, target))
        {
            agent.ResetPath();
            agent.isStopped = true;
            return true;
        }

        Vector3 outward = transform.position - point;
        outward.y = 0f;
        if (outward.sqrMagnitude < 0.001f) outward = -transform.forward;
        outward.Normalize();
        float distance = Mathf.Min(extinguishStandoff, hose.SprayRange - 0.5f);
        Vector3 nozzleOffset = hose.Origin - transform.position;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        // Try nearby sides of the object instead of falling back to walking into the fire.
        for (int i = 0; i < 8; i++)
        {
            Vector3 direction = Quaternion.Euler(0f, i * 45f, 0f) * outward;
            Vector3 approach = point + direction * distance;
            approach.y = transform.position.y;
            if (!NavMesh.SamplePosition(approach, out NavMeshHit hit, destinationSnapDistance, filter))
                continue;
            if (Vector3.Distance(hit.position + nozzleOffset, point) >= hose.SprayRange)
                continue;
            if (!ClearSprayLine(hit.position + nozzleOffset, point, target))
                continue;
            if (!agent.CalculatePath(hit.position, commandPath) ||
                commandPath.status != NavMeshPathStatus.PathComplete)
                continue;
            agent.isStopped = false;
            return agent.SetPath(commandPath);
        }
        return false;
    }

    private bool WithinSprayDistance(Vector3 point)
    {
        float reach = Mathf.Min(extinguishStandoff + destinationSnapDistance,
            hose.SprayRange - 0.1f);
        Vector3 horizontal = point - transform.position;
        horizontal.y = 0f;
        return horizontal.magnitude <= reach &&
            Vector3.Distance(hose.Origin, point) < hose.SprayRange - 0.1f;
    }

    private bool ClearSprayLine(Vector3 origin, Vector3 point, Flammable target)
    {
        Vector3 offset = point - origin;
        if (offset.sqrMagnitude < 0.001f) return true;
        int count = Physics.RaycastNonAlloc(origin, offset.normalized, sightHits, offset.magnitude,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        RaycastHit hit = default;
        float nearest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            if (sightHits[i].collider.transform.IsChildOf(transform) || sightHits[i].distance >= nearest) continue;
            hit = sightHits[i];
            nearest = hit.distance;
        }
        if (float.IsPositiveInfinity(nearest)) return true;
        if (target != null) return hit.collider.GetComponentInParent<Flammable>() == target;
        return hit.distance >= offset.magnitude - 0.15f;
    }

    private void Update()
    {
        if (!extinguishing) return;
        if (!AgentReady || hose == null || !hose.isActiveAndEnabled)
        {
            CancelExtinguish();
            return;
        }

        if (tracksObject)
        {
            if (NPCFireTarget.TryGetBurningPoint(burningObject, hose.Origin, out Vector3 nextPoint))
            {
                fireTarget = nextPoint;
                soakUntil = 0f;
            }
            else
            {
                // Cool/soak the extinguished surface before changing aim, so an adjacent fire
                // does not instantly relight it while this NPC moves to the next object.
                if (burningObject != null && postExtinguishSoakSeconds > 0f)
                {
                    if (soakUntil <= 0f) soakUntil = Time.time + postExtinguishSoakSeconds;
                    if (Time.time < soakUntil)
                    {
                        hose.AimAt(fireTarget);
                        hose.StartSpray();
                        return;
                    }
                }
                soakUntil = 0f;
                if (!ContinueNearbyFireJob())
                {
                    CancelExtinguish();
                    return;
                }
            }
        }
        else if (fireManager == null || fireManager.Grid == null ||
                 !fireManager.IsAnyBurningInBounds(PointBounds(fireTarget)))
        {
            CancelExtinguish();
            return;
        }

        if (WithinSprayDistance(fireTarget) && ClearSprayLine(hose.Origin, fireTarget, burningObject))
        {
            agent.isStopped = true;
            Vector3 facing = fireTarget - transform.position;
            facing.y = 0f;
            if (facing.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(facing), agent.angularSpeed * Time.deltaTime);
            hose.AimAt(fireTarget);
            hose.StartSpray();
        }
        else
        {
            hose.StopSpray();
            // Keep the accepted route stable while walking; replanning around the current
            // position every half-second can make the NPC orbit the fire indefinitely.
            if (agent.pathPending || (agent.hasPath &&
                agent.remainingDistance > agent.stoppingDistance + 0.15f)) return;
            if (WithinSprayDistance(fireTarget))
            {
                Vector3 facing = fireTarget - transform.position;
                facing.y = 0f;
                if (facing.sqrMagnitude > 0.001f)
                {
                    Quaternion desired = Quaternion.LookRotation(facing);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, desired,
                        agent.angularSpeed * Time.deltaTime);
                    if (Quaternion.Angle(transform.rotation, desired) > 5f) return;
                }
            }
            if (Time.time >= nextRepathTime)
            {
                nextRepathTime = Time.time + 0.5f;
                if (!BeginApproach(fireTarget, burningObject)) CancelExtinguish();
            }
        }
    }

    private Bounds PointBounds(Vector3 point) =>
        new Bounds(point, Vector3.one * Mathf.Max(0.1f, fireManager.cellSize));

    private bool ContinueNearbyFireJob()
    {
        // A cone can cover several props. Do not stop merely because the first prop went out.
        foreach (var candidate in FindObjectsByType<Flammable>(FindObjectsSortMode.None))
        {
            if (candidate.fireManager != fireManager ||
                !NPCFireTarget.TryGetBounds(candidate, out Bounds bounds) ||
                bounds.SqrDistance(fireZoneCenter) > fireWorkRadius * fireWorkRadius ||
                !NPCFireTarget.TryGetBurningPoint(candidate, hose.Origin, out Vector3 point))
                continue;
            if (!BeginApproach(point, candidate)) continue;
            burningObject = candidate;
            fireTarget = point;
            nextRepathTime = Time.time + 0.5f;
            return true;
        }
        return false;
    }

    public void CancelExtinguish() => EndFireJob(true);

    private void EndFireJob(bool stopApproach)
    {
        if (stopApproach && extinguishing && AgentReady)
        {
            agent.ResetPath();
            agent.isStopped = true;
        }
        extinguishing = false;
        tracksObject = false;
        burningObject = null;
        if (hose != null)
        {
            hose.StopSpray();
            hose.AimAt(null);
        }
    }

    private void OnDisable() => CancelExtinguish();

    private bool SetDestination(Vector3 targetPosition)
    {
        if (!AgentReady) return false;
        var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
        if (!NavMesh.SamplePosition(targetPosition, out NavMeshHit destination,
                destinationSnapDistance, filter) ||
            !agent.CalculatePath(destination.position, commandPath) ||
            commandPath.status != NavMeshPathStatus.PathComplete)
            return false;
        agent.isStopped = false;
        return agent.SetPath(commandPath);
    }
}
