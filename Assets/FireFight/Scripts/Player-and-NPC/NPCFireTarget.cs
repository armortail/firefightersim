using UnityEngine;
using FireFight.Managers;
using FireFight.Environment;

namespace FireFight
{
    /// <summary>Read-only burning-object queries, plus compatibility for older target references.</summary>
    public class NPCFireTarget : MonoBehaviour
    {
        [SerializeField] private Camera commandCamera;
        [SerializeField] private Transform targetMarker;
        [SerializeField] private float targetDistance = 100f;
        [SerializeField] private LayerMask targetLayers = Physics.DefaultRaycastLayers;

        private Flammable target;

        private void Awake()
        {
            if (commandCamera == null) commandCamera = GetComponentInChildren<Camera>();
            if (targetMarker != null) targetMarker.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (target != null && !IsBurning(target)) ClearTarget();
        }

        public bool SelectAtAim()
        {
            if (commandCamera == null) return false;
            Ray ray = commandCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            if (Physics.Raycast(ray, out RaycastHit hit, targetDistance, targetLayers,
                    QueryTriggerInteraction.Ignore))
            {
                var candidate = hit.collider.GetComponentInParent<Flammable>();
                if (SetTarget(candidate))
                {
                    if (targetMarker != null)
                        targetMarker.position = hit.point + hit.normal * 0.15f;
                    return true;
                }
            }
            ClearTarget();
            Debug.Log("Choose an object that is actually on fire.", this);
            return false;
        }

        public bool SetTarget(Flammable candidate)
        {
            // Query the real fire grid; a cached flag or playing particles are not enough.
            if (!IsBurning(candidate)) return false;
            target = candidate;
            if (targetMarker != null) targetMarker.gameObject.SetActive(true);
            Debug.Log("Fire target: " + target.name, this);
            return true;
        }

        public bool TryGetTarget(out Flammable burningObject)
        {
            if (!IsBurning(target)) ClearTarget();
            burningObject = target;
            return burningObject != null;
        }

        public void ClearTarget()
        {
            target = null;
            if (targetMarker != null) targetMarker.gameObject.SetActive(false);
        }

        public static bool TryGetBounds(Flammable flammable, out Bounds bounds)
        {
            bounds = default;
            if (flammable == null || !flammable.isActiveAndEnabled) return false;
            var collider = flammable.GetComponentInChildren<Collider>();
            if (collider == null || !collider.enabled || collider.isTrigger) return false;
            bounds = collider.bounds;
            return true;
        }

        public static bool IsBurning(Flammable flammable)
        {
            return TryGetBounds(flammable, out Bounds bounds) &&
                flammable.fireManager != null && flammable.fireManager.Grid != null &&
                flammable.fireManager.IsAnyBurningInBounds(bounds);
        }

        public static bool TryGetBurningPoint(Flammable flammable, Vector3 from, out Vector3 point)
        {
            point = default;
            if (!TryGetBounds(flammable, out Bounds bounds) ||
                flammable.fireManager == null || flammable.fireManager.Grid == null)
                return false;

            FireGrid grid = flammable.fireManager.Grid;
            Vector3Int min = grid.WorldToGrid(bounds.min);
            Vector3Int max = grid.WorldToGrid(bounds.max);
            float nearest = float.PositiveInfinity;
            for (int x = Mathf.Max(0, min.x); x <= Mathf.Min(grid.Size.x - 1, max.x); x++)
            for (int y = Mathf.Max(0, min.y); y <= Mathf.Min(grid.Size.y - 1, max.y); y++)
            for (int z = Mathf.Max(0, min.z); z <= Mathf.Min(grid.Size.z - 1, max.z); z++)
            {
                var position = new Vector3Int(x, y, z);
                if (grid.GetCell(position).state != FireState.Burning) continue;
                Vector3 world = grid.GridToWorld(position);
                float distance = (world - from).sqrMagnitude;
                if (distance >= nearest) continue;
                nearest = distance;
                point = world;
            }
            return !float.IsPositiveInfinity(nearest);
        }
    }
}
