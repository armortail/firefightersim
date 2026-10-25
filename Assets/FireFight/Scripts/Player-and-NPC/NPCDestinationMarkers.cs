using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using FireFight.Managers;

namespace FireFight
{
    public class NPCDestinationMarkers : MonoBehaviour
    {
        [SerializeField] private Camera commandCamera;
        [SerializeField] private NPCMovement danny;
        [SerializeField] private NPCMovement simon;
        [SerializeField] private Transform dannyMarker;
        [SerializeField] private Transform simonMarker;
        [SerializeField] private float placementDistance = 100f;
        [SerializeField] private LayerMask placementLayers = Physics.DefaultRaycastLayers;
        [SerializeField, Min(0.5f)] private float fireSearchRadius = 3f;

        private Vector3 dannyDestination;
        private Vector3 simonDestination;
        private bool hasDannyDestination;
        private bool hasSimonDestination;

        private void Awake()
        {
            if (commandCamera == null || danny == null || simon == null ||
                dannyMarker == null || simonMarker == null)
            {
                Debug.LogError("Assign all NPCDestinationMarkers references.", this);
                enabled = false;
                return;
            }
            dannyMarker.gameObject.SetActive(false);
            simonMarker.gameObject.SetActive(false);
        }

        public bool TryGetDestination(NPCMovement npc, out Vector3 destination)
        {
            destination = default;
            if (npc == danny && hasDannyDestination) { destination = dannyDestination; return true; }
            if (npc == simon && hasSimonDestination) { destination = simonDestination; return true; }
            return false;
        }

        public bool TryGetFireTarget(NPCMovement npc, out Flammable target)
        {
            target = null;
            if (!TryGetDestination(npc, out Vector3 point)) return false;
            float nearest = fireSearchRadius * fireSearchRadius;
            foreach (var candidate in FindObjectsByType<Flammable>(FindObjectsSortMode.None))
            {
                if (!NPCFireTarget.IsBurning(candidate) || !NPCFireTarget.TryGetBounds(candidate, out Bounds bounds))
                    continue;
                float distance = bounds.SqrDistance(point);
                if (distance > nearest) continue;
                nearest = distance;
                target = candidate;
            }
            return target != null;
        }

        private void Update()
        {
            if (Cursor.lockState != CursorLockMode.Locked || Mouse.current == null) return;
            if (Mouse.current.leftButton.wasPressedThisFrame) PlaceMarker(danny);
            if (Mouse.current.rightButton.wasPressedThisFrame) PlaceMarker(simon);
        }

        private void PlaceMarker(NPCMovement npc)
        {
            Ray ray = commandCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (!Physics.Raycast(ray, out RaycastHit hit, placementDistance, placementLayers,
                    QueryTriggerInteraction.Ignore)) return;
            var objectHit = hit.collider.GetComponentInParent<Flammable>();
            if (Vector3.Dot(hit.normal, Vector3.up) < 0.5f && objectHit == null) return;
            TryPlaceDestination(npc, hit.point, objectHit);
        }

        /// <summary>Object pings resolve to reachable ground beside the object, never inside it.</summary>
        public bool TryPlaceDestination(NPCMovement npc, Vector3 point, Flammable objectHit = null)
        {
            if (npc == null || (npc != danny && npc != simon)) return false;
            var agent = npc.GetComponent<NavMeshAgent>();
            if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh) return false;
            var filter = new NavMeshQueryFilter { agentTypeID = agent.agentTypeID, areaMask = agent.areaMask };
            Vector3 destination = default;
            bool found = false;
            float nearest = float.PositiveInfinity;
            var path = new NavMeshPath();

            for (int i = 0; i < (objectHit == null ? 1 : 9); i++)
            {
                Vector3 candidate = point;
                if (objectHit != null && NPCFireTarget.TryGetBounds(objectHit, out Bounds bounds))
                {
                    candidate.y = npc.transform.position.y;
                    if (i > 0)
                    {
                        Vector3 direction = Quaternion.Euler(0, (i - 1) * 45f, 0) * Vector3.forward;
                        float padding = Mathf.Max(bounds.extents.x, bounds.extents.z) + agent.radius + 0.25f;
                        candidate = new Vector3(bounds.center.x, npc.transform.position.y, bounds.center.z) + direction * padding;
                    }
                }
                if (!NavMesh.SamplePosition(candidate, out NavMeshHit navHit, 0.5f, filter) ||
                    !agent.CalculatePath(navHit.position, path) || path.status != NavMeshPathStatus.PathComplete)
                    continue;
                float score = (navHit.position - point).sqrMagnitude;
                if (score >= nearest) continue;
                destination = navHit.position;
                nearest = score;
                found = true;
            }
            if (!found) { Debug.Log("That marker location is not reachable.", this); return false; }
            Transform marker;
            if (npc == danny)
            {
                dannyDestination = destination;
                hasDannyDestination = true;
                marker = dannyMarker;
            }
            else
            {
                simonDestination = destination;
                hasSimonDestination = true;
                marker = simonMarker;
            }
            if (marker != null)
            {
                marker.position = destination + Vector3.up * 0.03f;
                marker.gameObject.SetActive(true);
            }
            return true;
        }
    }
}
