using UnityEngine;
using FireFight.Managers;

namespace FireFight.Managers
{
    /// <summary>
    /// A window/door/vent that opens and closes to change the confined-space oxygen layout.
    ///
    /// Two modes, matching how the FireManager builds its oxygen boxes:
    ///
    ///   AUTO-DETECT (FireManager.autoDetectOxygenBoxes = true) - RECOMMENDED:
    ///     The door occupies a chunk of grid cells. CLOSED makes those cells SOLID, so the
    ///     flood-fill splits the space into two sealed rooms; OPEN makes them air again, so
    ///     the two rooms merge into one. Each change triggers FireManager.RedetectOxygenBoxes,
    ///     so rooms genuinely grow/shrink - open a door and the fresh air from the next room
    ///     floods in (backdraft); close it and a burning room suffocates.
    ///
    ///   MANUAL BOX (auto-detect off):
    ///     Falls back to simply venting/sealing one pre-authored oxygen box (the original
    ///     behaviour) via FireManager.SetOxygenBoxOpen / SetOxygenBoxOpenAtWorld.
    ///
    /// The oxygen effect applies immediately on Open/Close; any transform animation is cosmetic.
    /// </summary>
    [DisallowMultipleComponent]
    public class OxygenWindow : MonoBehaviour
    {
        public enum TargetMode { ByIndex, ByWorldPoint }

        [Header("Fire Manager")]
        [Tooltip("The fire system that owns the oxygen boxes. Found automatically if left empty.")]
        [SerializeField] private FireManager fireManager;

        [Header("Door opening (cells this window blocks when CLOSED)")]
        [Tooltip("World-space size of the gap this door/window fills. Closed = these cells are " +
                 "solid (wall); open = they are air. Centred on this object (or doorBounds if set).")]
        [SerializeField] private Vector3 openingSize = new Vector3(1.5f, 2f, 0.3f);

        [Tooltip("Optional: a collider/transform defining the opening instead of openingSize. " +
                 "If set, its bounds are used.")]
        [SerializeField] private Collider doorBounds;

        [Header("Manual-box fallback (auto-detect OFF)")]
        [SerializeField] private TargetMode targetMode = TargetMode.ByIndex;
        [SerializeField] private int boxIndex = 0;
        [SerializeField] private Transform boxSamplePoint;

        [Header("State")]
        [Tooltip("Whether the window starts open (air flows / rooms merged) or closed (sealed wall).")]
        [SerializeField] private bool startOpen = false;

        [Header("Optional animation (cosmetic)")]
        [SerializeField] private Transform movingPart;
        [SerializeField] private Vector3 openLocalOffset = new Vector3(0f, 1f, 0f);
        [SerializeField] private Vector3 openLocalEuler = Vector3.zero;
        [SerializeField, Min(0f)] private float animSpeed = 4f;

        /// <summary>True when the window is open (air can pass / box vented).</summary>
        public bool IsOpen { get; private set; }

        private Vector3 closedLocalPos;
        private Quaternion closedLocalRot;

        private void Awake()
        {
            if (fireManager == null) fireManager = FindFirstObjectByType<FireManager>();
            if (movingPart != null)
            {
                closedLocalPos = movingPart.localPosition;
                closedLocalRot = movingPart.localRotation;
            }
        }

        private void Start() => SetOpen(startOpen, snap: true);

        /// <summary>Open: air passes (rooms merge / box vented) - fresh air can flood a sealed fire.</summary>
        public void Open() => SetOpen(true);

        /// <summary>Close: becomes a solid barrier (rooms split / box sealed) - fire inside suffocates.</summary>
        public void Close() => SetOpen(false);

        /// <summary>Flip the window's current state.</summary>
        public void Toggle() => SetOpen(!IsOpen);

        /// <summary>Set the window open/closed, applying the oxygen effect immediately.</summary>
        public void SetOpen(bool open, bool snap = false)
        {
            IsOpen = open;
            ApplyOxygenEffect(open);

            if (snap && movingPart != null)
            {
                movingPart.localPosition = open ? closedLocalPos + openLocalOffset : closedLocalPos;
                movingPart.localRotation = open ? closedLocalRot * Quaternion.Euler(openLocalEuler) : closedLocalRot;
            }
        }

        private void ApplyOxygenEffect(bool open)
        {
            if (fireManager == null) return;

            if (fireManager.autoDetectOxygenBoxes)
            {
                // Closed door = solid barrier; open door = air. Then re-detect so the
                // flood-fill splits or merges the rooms around this opening.
                fireManager.SetSolidInBounds(OpeningBounds(), solid: !open);
                fireManager.RedetectOxygenBoxes();
            }
            else
            {
                // Manual-box fallback: just vent/seal the authored box.
                if (targetMode == TargetMode.ByWorldPoint)
                {
                    Vector3 p = boxSamplePoint != null ? boxSamplePoint.position : transform.position;
                    if (!fireManager.SetOxygenBoxOpenAtWorld(p, open))
                        Debug.LogWarning($"[FireFight] OxygenWindow '{name}': no oxygen box at the " +
                                         "sample point.", this);
                }
                else if (boxIndex >= 0 && boxIndex < fireManager.OxygenBoxCount)
                {
                    fireManager.SetOxygenBoxOpen(boxIndex, open);
                }
            }
        }

        /// <summary>World-space bounds of the door opening (from collider if set, else openingSize).</summary>
        private Bounds OpeningBounds()
        {
            if (doorBounds != null) return doorBounds.bounds;
            return new Bounds(transform.position, openingSize);
        }

        private void Update()
        {
            if (movingPart == null) return;

            Vector3 targetPos = IsOpen ? closedLocalPos + openLocalOffset : closedLocalPos;
            Quaternion targetRot = IsOpen ? closedLocalRot * Quaternion.Euler(openLocalEuler) : closedLocalRot;

            float t = 1f - Mathf.Exp(-animSpeed * Time.deltaTime); // frame-rate independent ease
            movingPart.localPosition = Vector3.Lerp(movingPart.localPosition, targetPos, t);
            movingPart.localRotation = Quaternion.Slerp(movingPart.localRotation, targetRot, t);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = IsOpen ? new Color(0.3f, 0.8f, 1f, 0.4f) : new Color(1f, 0.4f, 0.2f, 0.5f);
            Bounds b = OpeningBounds();
            Gizmos.DrawCube(b.center, b.size);
        }
    }
}
