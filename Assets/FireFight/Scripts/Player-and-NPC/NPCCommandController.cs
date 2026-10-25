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
        if (keyboard == null)
            return;

        // N = move here. F = go fight the fire at the aim point.
        if (keyboard.nKey.wasPressedThisFrame)
            CommandSelectedNpcAtAim();
        else if (keyboard.fKey.wasPressedThisFrame)
            CommandSelectedNpcExtinguishAtAim();
        if(Mouse.current!= null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            TrySelectNpcAtAim();
        }
    }

    public void TrySelectNpcAtAim()
    {
        Ray aimRay = commandCamera.ViewportPointToRay(new Vector3(0.5f,0.5f,0f));

        if (Physics.Raycast(aimRay, out RaycastHit hit, commandDistance,
    commandLayers, QueryTriggerInteraction.Ignore))
        {
            NPCMovement clickedNpc = hit.collider.GetComponentInParent<NPCMovement>();
            SelectNpc(clickedNpc);
        }
        else
        {
            SelectNpc(null);
        }


    }
    /// <summary>Selects the sole recipient; other NPCs do not listen for input.</summary>
    public void SelectNpc(NPCMovement npc)
    {
        
        selectedNpc = npc;
    }

    /// <summary>Can also be called by a future voice-command interpreter.</summary>
    public bool CommandSelectedNpcAtAim()
    {
        if (!TryGetAimHit(out RaycastHit hit))
            return false;

        // Move commands require roughly flat ground to stand on.
        if (Vector3.Dot(hit.normal, Vector3.up) < 0.5f)
            return false;

        return selectedNpc.MoveTo(hit.point);
    }

    /// <summary>
    /// Tell the selected NPC to extinguish the fire the player is aiming at.
    /// The aim point can be any surface (a burning wall, floor, object), so this
    /// does not require flat ground like a move command does.
    /// </summary>
    public bool CommandSelectedNpcExtinguishAtAim()
    {
        if (!TryGetAimHit(out RaycastHit hit))
            return false;

        return selectedNpc.ExtinguishAt(hit.point);
    }

    /// <summary>Raycast from screen centre. False if no NPC/camera or nothing hit.</summary>
    private bool TryGetAimHit(out RaycastHit hit)
    {
        hit = default;
        if (commandCamera == null || selectedNpc == null)
            return false;

        Ray aimRay = commandCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        return Physics.Raycast(aimRay, out hit, commandDistance,
            commandLayers, QueryTriggerInteraction.Ignore);
    }
}
