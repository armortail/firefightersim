using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Unity.AI.Navigation;

namespace FireFight
{
    /// <summary>
    /// A hinged house door (or, with isWindow set, a window). E opens the one under the
    /// crosshair. Both swing open/closed and vent their room's oxygen; a window additionally
    /// creates no walkable NavMesh passage and skips the doorway occupant check.
    /// </summary>
    [DisallowMultipleComponent]
    public class HouseDoor : MonoBehaviour
    {
        [SerializeField] private Transform[] hinges;
        [SerializeField] private float[] openAngles;
        [SerializeField] private float turnSpeed = 180f;
        [SerializeField] private float interactionDistance = 5f;
        [SerializeField] private HouseVentilation ventilation;
        [SerializeField] private int roomA;
        [SerializeField] private int roomB = -1;
        [SerializeField] private Vector3 closedCenter;
        [SerializeField] private Vector3 closedSize;

        [Tooltip("Treat this as a WINDOW, not a door: it still swings open/closed and vents its " +
                 "room's oxygen, but does NOT create a walkable NavMesh passage (you can't walk " +
                 "through a window) and doesn't block closing on someone standing in it. Set a " +
                 "smaller closedSize / hinge angle in the Inspector for a window.")]
        [SerializeField] private bool isWindow = false;
        [SerializeField] private bool startOpen = false;

        private Quaternion[] closedRotations;
        private Quaternion[] openRotations;
        private NavMeshObstacle[] obstacles;
        private bool animating;
        private static Camera view;
        private static HouseDoor aimedDoor;
        private static int aimFrame = -1;
        private static readonly Vector3[] routeCorners = new Vector3[128];
        private static readonly List<HouseDoor> activeDoors = new List<HouseDoor>();
        private NavMeshLink passage;
        private float nextLinkAttempt;
        public bool IsOpen { get; private set; }
        public int RoomA => roomA;
        public int RoomB => roomB;

        private void OnEnable()
        {
            activeDoors.Add(this);
            if (passage == null) passage = GetComponent<NavMeshLink>();
            if (passage != null) passage.enabled = true;
        }

        private void OnDisable()
        {
            activeDoors.Remove(this);
            if (aimedDoor == this) aimedDoor = null;
            if (passage != null) passage.enabled = false;
        }

        private void Start() => ConnectPassage();

        private void ConnectPassage()
        {
            // A window is not walkable: never build a NavMesh passage through it.
            if (isWindow) return;

            if (passage != null) { passage.enabled = true; return; }
            nextLinkAttempt = Time.time + 1f;
            // Both demo firefighters use the default humanoid agent type.
            var filter = new NavMeshQueryFilter { agentTypeID = 0, areaMask = NavMesh.AllAreas };
            Vector3 center = new Vector3(closedCenter.x, 0f, 0f);
            if (!NavMesh.SamplePosition(transform.TransformPoint(center + Vector3.forward * 1.4f),
                    out var front, 1f, filter) ||
                !NavMesh.SamplePosition(transform.TransformPoint(center - Vector3.forward * 1.4f),
                    out var back, 1f, filter)) return;
            // Never connect two samples which snapped to the same side of a wall.
            if (transform.InverseTransformPoint(front.position).z <= 0.2f ||
                transform.InverseTransformPoint(back.position).z >= -0.2f) return;
            passage = gameObject.AddComponent<NavMeshLink>();
            passage.enabled = false;
            passage.agentTypeID = 0;
            passage.startPoint = transform.InverseTransformPoint(front.position);
            passage.endPoint = transform.InverseTransformPoint(back.position);
            passage.width = 0f;
            passage.bidirectional = true;
            passage.costModifier = 1f;
            passage.enabled = true;
        }

        /// <summary>Open only a nearby door intersected by the NPC's accepted route.</summary>
        public static HouseDoor OpenForApproachingAgent(NavMeshAgent agent)
        {
            if (agent == null || !agent.isOnNavMesh || agent.pathPending || !agent.hasPath) return null;
            int cornerCount = -1;
            foreach (var door in activeDoors)
            {
                if (door == null || door.isWindow || door.LeavesAreClear()) continue;
                Vector3 position = door.transform.InverseTransformPoint(agent.transform.position);
                if (Mathf.Abs(position.z) > 3f + agent.radius ||
                    Mathf.Abs(position.x - door.closedCenter.x) > door.closedSize.x * 0.5f + agent.radius ||
                    Mathf.Abs(position.y) > 1.5f) continue;
                if (cornerCount < 0) cornerCount = agent.path.GetCornersNonAlloc(routeCorners);
                Vector3 previous = position;
                for (int i = 0; i < cornerCount; i++)
                {
                    Vector3 next = door.transform.InverseTransformPoint(routeCorners[i]);
                    if (previous.z * next.z <= 0f && Mathf.Abs(next.z - previous.z) > 0.01f)
                    {
                        float t = -previous.z / (next.z - previous.z);
                        float x = Mathf.Lerp(previous.x, next.x, t);
                        if (Mathf.Abs(x - door.closedCenter.x) <= door.closedSize.x * 0.5f + agent.radius)
                        {
                            door.SetOpen(true);
                            return door.LeavesAreClear() ? null : door;
                        }
                    }
                    previous = next;
                }
            }
            return null;
        }

        public bool IsPassable => LeavesAreClear();

        private bool LeavesAreClear()
        {
            if (!IsOpen || closedRotations == null) return false;
            for (int i = 0; i < hinges.Length; i++)
                if (Quaternion.Angle(hinges[i].localRotation, closedRotations[i]) <
                    Mathf.Min(80f, Mathf.Abs(openAngles[i]) * 0.85f)) return false;
            return true;
        }

        private void Awake()
        {
            IsOpen = startOpen;
            closedRotations = new Quaternion[hinges.Length];
            openRotations = new Quaternion[hinges.Length];
            for (int i = 0; i < hinges.Length; i++)
            {
                closedRotations[i] = hinges[i].localRotation;
                openRotations[i] = closedRotations[i] * Quaternion.Euler(0f, openAngles[i], 0f);
                if (startOpen) hinges[i].localRotation = openRotations[i];
            }
            obstacles = GetComponentsInChildren<NavMeshObstacle>();
            foreach (var obstacle in obstacles) obstacle.carveOnlyStationary = true;
        }

        private void Update()
        {
            if (!isWindow && passage == null && Time.time >= nextLinkAttempt) ConnectPassage();
            if (animating)
            {
                bool finished = true;
                for (int i = 0; i < hinges.Length; i++)
                {
                    Quaternion target = IsOpen ? openRotations[i] : closedRotations[i];
                    hinges[i].localRotation = Quaternion.RotateTowards(hinges[i].localRotation, target, turnSpeed * Time.deltaTime);
                    if (Quaternion.Angle(hinges[i].localRotation, target) > 0.01f) finished = false;
                }
                if (finished)
                {
                    animating = false;
                    foreach (var obstacle in obstacles) obstacle.enabled = true;
                }
            }
            UpdateAim();
            if (aimedDoor == this && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) Toggle();
        }

        private static void UpdateAim()
        {
            if (aimFrame == Time.frameCount) return;
            aimFrame = Time.frameCount;
            aimedDoor = null;
            if (view == null) view = Camera.main;
            if (view == null || Cursor.lockState != CursorLockMode.Locked) return;
            float distance = 0f;
            foreach (var door in activeDoors) if (door != null) distance = Mathf.Max(distance, door.interactionDistance);
            if (!Physics.Raycast(view.ViewportPointToRay(new Vector3(0.5f, 0.5f)), out var hit,
                    distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) return;
            var selected = hit.collider.GetComponentInParent<HouseDoor>();
            if (selected != null && selected.isActiveAndEnabled && hit.distance <= selected.interactionDistance)
                aimedDoor = selected;
        }

        public void Toggle() => SetOpen(!IsOpen);

        public void SetOpen(bool open)
        {
            if (open == IsOpen) return;
            if (!open && !isWindow)
            {
                // Do not close a DOOR through a player or firefighter standing in the opening.
                // Windows skip this - nobody stands inside a window.
                var occupants = Physics.OverlapBox(transform.TransformPoint(closedCenter), closedSize * 0.5f,
                    transform.rotation, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                foreach (var body in occupants)
                    if (body.GetComponentInParent<CharacterController>() != null ||
                        body.GetComponentInParent<NavMeshAgent>() != null)
                    {
                        Debug.Log("The doorway is occupied. Move clear before closing it.", this);
                        return;
                    }
            }
            IsOpen = open;
            animating = true;
            // The passage link handles traversal while the leaves swing. Re-carve only once settled.
            foreach (var obstacle in obstacles) obstacle.enabled = false;
            if (ventilation != null) ventilation.RefreshVentilation();
        }

        private void OnGUI()
        {
            if (aimedDoor != this) return;
            string noun = isWindow ? "window" : "door";
            string text = IsOpen ? $"E: Close {noun}" : $"E: Open {noun}";
            if (ventilation != null)
            {
                float oxygen = ventilation.GetRoomOxygen(roomA);
                if (oxygen >= 0f) text += "  |  Room oxygen: " + Mathf.RoundToInt(oxygen * 100f) + "%";
            }
            GUI.Box(new Rect(Screen.width * 0.5f - 180f, Screen.height * 0.5f + 35f, 360f, 28f), text);
        }
    }
}
