using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace FireFight
{
    public class NPCDestinationMarkers : MonoBehaviour
    {
        // Start is called once before the first execution of Update after the MonoBehaviour is created

        [SerializeField] private Camera commandCamera;
        [SerializeField] private NPCMovement danny;
        [SerializeField] private NPCMovement simon;
        [SerializeField] private Transform dannyMarker;
        [SerializeField] private Transform simonMarker;

        [SerializeField] private float placementDistance = 100f;
        [SerializeField] private LayerMask placementLayers = Physics.DefaultRaycastLayers;

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
        void Start()
        {
        
        }

        public bool TryGetDestination(NPCMovement npc, out Vector3 destination)
        {
            destination = default;

            if (npc == danny && hasDannyDestination)
            {
                destination = dannyDestination;
                return true;
            }

            if (npc == simon && hasSimonDestination)
            {
                destination = simonDestination;
                return true;
            }

            return false;
        }

        // Update is called once per frame
        private void Update()
        {
            if (Cursor.lockState != CursorLockMode.Locked)
                return;

            Mouse mouse = Mouse.current;

            if (mouse == null)
                return;

            if (mouse.leftButton.wasPressedThisFrame)
                PlaceMarker(danny, dannyMarker);

            if (mouse.rightButton.wasPressedThisFrame)
                PlaceMarker(simon, simonMarker);
        }

        private void PlaceMarker(NPCMovement npc, Transform marker)
        {
            Ray aimRay = commandCamera.ViewportPointToRay(
                new Vector3(0.5f, 0.5f, 0f));

            if (!Physics.Raycast(aimRay, out RaycastHit hit,
                    placementDistance, placementLayers,
                    QueryTriggerInteraction.Ignore))
            {
                return;
            }

            if (Vector3.Dot(hit.normal, Vector3.up) < 0.5f)
                return;

            NavMeshAgent agent = npc.GetComponent<NavMeshAgent>();

            if (agent == null || !agent.isActiveAndEnabled || !agent.isOnNavMesh)
                return;

            var filter = new NavMeshQueryFilter
            {
                agentTypeID = agent.agentTypeID,
                areaMask = agent.areaMask
            };

            if (!NavMesh.SamplePosition(hit.point, out NavMeshHit navHit,
                    0.5f, filter))
            {
                return;
            }

            var path = new NavMeshPath();

            if (!agent.CalculatePath(navHit.position, path) ||
                path.status != NavMeshPathStatus.PathComplete)
            {
                Debug.Log("That marker location is not reachable.");
                return;
            }

            if (npc == danny)
            {
                dannyDestination = navHit.position;
                hasDannyDestination = true;
            }
            else
            {
                simonDestination = navHit.position;
                hasSimonDestination = true;
            }

            marker.position = new Vector3(
                navHit.position.x,
                hit.point.y + 0.03f,
                navHit.position.z);

            marker.gameObject.SetActive(true);
        }
    }
}
