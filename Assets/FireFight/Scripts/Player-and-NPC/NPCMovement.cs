using UnityEngine;
using UnityEngine.AI;
using FireFight.Managers;

/// <summary>Moves one NPC. Input and voice commands belong to the command controller.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class NPCMovement : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float destinationSnapDistance = 0.5f;

    [Header("Firefighting")]
    [Tooltip("The hose this NPC carries. Required for extinguish commands.")]
    [SerializeField] private HoseController hose;

    [Tooltip("How close the NPC gets to the fire before it stops and sprays.")]
    [SerializeField, Min(0.5f)] private float extinguishStandoff = 4f;

    [Tooltip("The fire simulation, used to check whether the target is still burning.")]
    [SerializeField] private FireManager fireManager;

    private NavMeshAgent agent;
    private NavMeshPath commandPath;

    // Current extinguish job (null when idle / only moving).
    private bool extinguishing;
    private Vector3 fireTarget;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        commandPath = new NavMeshPath();
        if (hose == null) hose = GetComponentInChildren<HoseController>();
        if (fireManager == null) fireManager = FindFirstObjectByType<FireManager>(); ;
    }

    /// <summary>
    /// Accepts a reachable world-space destination. An invalid command leaves
    /// the current route unchanged. Keyboard and voice can share this API.
    /// </summary>
    public bool MoveTo(Vector3 targetPosition)
    {
        // A plain move cancels any extinguish job and stops spraying.
        CancelExtinguish();
        return SetDestination(targetPosition);
    }

    /// <summary>
    /// Command the NPC to put out a fire at a world point: walk within hose
    /// range, then spray until the target cell stops burning. Returns false if
    /// the NPC has no hose or cannot path toward the fire.
    /// </summary>
    public bool ExtinguishAt(Vector3 firePosition)
    {
        if (hose == null)
            return false;

        fireTarget = firePosition;

        // Aim for a spot a short standoff away from the fire, on the NPC's side.
        Vector3 fromFire = transform.position - firePosition;
        fromFire.y = 0f;
        if (fromFire.sqrMagnitude < 0.0001f) fromFire = -transform.forward;
        Vector3 approach = firePosition + fromFire.normalized * extinguishStandoff;

        if (!SetDestination(approach))
        {
            // If we can't reach the ideal standoff spot, still try to path
            // directly toward the fire so the NPC makes a best effort.
            if (!SetDestination(firePosition))
                return false;
        }

        extinguishing = true;
        return true;
    }

    private void Update()
    {
        if (!extinguishing) return;

        if (hose == null)
        {
            CancelExtinguish();
            return;
        }

        float distToFire = Vector3.Distance(transform.position, fireTarget);
        bool inRange = distToFire <= extinguishStandoff + destinationSnapDistance + 0.5f;

        if (inRange)
        {
            // Stop and spray at the fire.
            if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
                agent.isStopped = true;

            hose.AimAt(fireTarget);
            hose.StartSpray();

            // Done once the simulation reports the target is no longer burning.
            if (fireManager != null && !fireManager.IsAnyBurningInBounds(TargetBounds()))
                CancelExtinguish();
        }
        else
        {
            // Still travelling: don't waste water yet.
            hose.StopSpray();
        }
    }

    /// <summary>A small box around the fire target used to test "is it still burning?".</summary>
    private Bounds TargetBounds()
    {
        return new Bounds(fireTarget, Vector3.one); // 1m cube around the aim point
    }

    /// <summary>Stop any current extinguish job and shut the hose off.</summary>
    public void CancelExtinguish()
    {
        extinguishing = false;
        if (hose != null) hose.StopSpray();
    }

    /// <summary>Shared pathing: validates and issues a navmesh route.</summary>
    private bool SetDestination(Vector3 targetPosition)
    {
        if (!isActiveAndEnabled || agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
            return false;

        var filter = new NavMeshQueryFilter
        {
            agentTypeID = agent.agentTypeID,
            areaMask = agent.areaMask
        };

        if (!NavMesh.SamplePosition(targetPosition, out NavMeshHit destination,
                destinationSnapDistance, filter))
            return false;

        if (!agent.CalculatePath(destination.position, commandPath) ||
            commandPath.status != NavMeshPathStatus.PathComplete)
            return false;

        agent.isStopped = false;
        return agent.SetPath(commandPath);
    }
}
