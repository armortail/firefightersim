using UnityEngine;
using Unity.Cinemachine;

namespace FireFight.Environment
{
    /// <summary>
    /// Binds a CinemachineCamera's Tracking Target to the player at runtime.
    ///
    /// The StarterAssets player prefab exposes a dedicated camera pivot
    /// ("PlayerCameraRoot", tagged "CinemachineTarget") at head height. Because
    /// that pivot lives inside a prefab instance, wiring it in the scene by hand
    /// is fragile, so instead we locate it on Awake and assign it. Drop this on
    /// the same GameObject as a CinemachineCamera and it just works.
    /// </summary>
    [RequireComponent(typeof(CinemachineCamera))]
    public class CameraTargetBinder : MonoBehaviour
    {
        [Tooltip("Tag of the object the camera should follow. StarterAssets uses 'CinemachineTarget'.")]
        public string targetTag = "CinemachineTarget";

        [Tooltip("Optional explicit target. If set, overrides the tag search.")]
        public Transform explicitTarget;

        private void Awake()
        {
            var vcam = GetComponent<CinemachineCamera>();
            Transform target = explicitTarget;

            if (target == null && !string.IsNullOrEmpty(targetTag))
            {
                var tagged = GameObject.FindWithTag(targetTag);
                if (tagged != null) target = tagged.transform;
            }

            if (target != null)
            {
                vcam.Follow = target;
                vcam.LookAt = target;
            }
            else
            {
                Debug.LogWarning(
                    $"[CameraTargetBinder] No target found (tag '{targetTag}'). " +
                    "Camera will not follow until a target is assigned.", this);
            }
        }
    }
}
