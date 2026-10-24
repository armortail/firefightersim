using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Chooses the recipient and aimed destination for player-issued NPC commands.</summary>
[DisallowMultipleComponent]
public class NPCCommandController : MonoBehaviour
{
    [SerializeField] private Camera commandCamera;
    [SerializeField] private NPCMovement selectedNpc;
    [SerializeField, Min(1f)] private float commandDistance = 100f;
    [SerializeField] private LayerMask commandLayers = Physics.DefaultRaycastLayers;

    public NPCMovement SelectedNpc => selectedNpc;

    private void Awake()
    {
        if (commandCamera == null)
            commandCamera = Camera.main;
    }

    private void Update()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.nKey.wasPressedThisFrame)
            CommandSelectedNpcAtAim();
    }

    /// <summary>Selects the sole recipient; other NPCs do not listen for input.</summary>
    public void SelectNpc(NPCMovement npc)
    {
        selectedNpc = npc;
    }

    /// <summary>Can also be called by a future voice-command interpreter.</summary>
    public bool CommandSelectedNpcAtAim()
    {
        if (commandCamera == null || selectedNpc == null)
            return false;

        Ray aimRay = commandCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(aimRay, out RaycastHit hit, commandDistance,
                commandLayers, QueryTriggerInteraction.Ignore))
            return false;

        if (Vector3.Dot(hit.normal, Vector3.up) < 0.5f)
            return false;

        return selectedNpc.MoveTo(hit.point);
    }
}
