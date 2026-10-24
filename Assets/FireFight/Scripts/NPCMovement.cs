using UnityEngine;
using UnityEngine.AI;

/// <summary>Moves one NPC. Input and voice commands belong to the command controller.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshAgent))]
public class NPCMovement : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float destinationSnapDistance = 0.5f;

    private NavMeshAgent agent;
    private NavMeshPath commandPath;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        commandPath = new NavMeshPath();
    }

    /// <summary>
    /// Accepts a reachable world-space destination. An invalid command leaves
    /// the current route unchanged. Keyboard and voice can share this API.
    /// </summary>
    public bool MoveTo(Vector3 targetPosition)
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
