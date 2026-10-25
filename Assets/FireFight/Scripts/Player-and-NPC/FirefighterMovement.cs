using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirefighterMovement : MonoBehaviour
{
    [SerializeField] private Transform playerCamera;
    [SerializeField] private float walkSpeed = 3f;
    [SerializeField] private float mouseSensitivity = 0.1f;

    private CharacterController controller;
    private float pitch;

    private void Start()
    {
        controller = GetComponent<CharacterController>();
        SetCursorLocked(true);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;

        if (keyboard == null)
            return;

        if (keyboard.escapeKey.wasPressedThisFrame)
            SetCursorLocked(false);
            
            
        else if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            SetCursorLocked(true);

        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        // Turn the player sideways; tilt only the camera up/down.
        if (mouse != null)
        {
            Vector2 look = mouse.delta.ReadValue() * mouseSensitivity;

            transform.Rotate(0f, look.x, 0f);

            pitch = Mathf.Clamp(pitch - look.y, -80f, 80f);
            playerCamera.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        // Read WASD and prevent faster diagonal movement.
        float sideways = (keyboard.dKey.isPressed ? 1f : 0f)
                       - (keyboard.aKey.isPressed ? 1f : 0f);
        float forward = (keyboard.wKey.isPressed ? 1f : 0f)
                      - (keyboard.sKey.isPressed ? 1f : 0f);

        Vector3 direction = transform.right * sideways
                          + transform.forward * forward;

        controller.SimpleMove(direction.normalized * walkSpeed);
    }

    private void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked
            ? CursorLockMode.Locked
            : CursorLockMode.None;
        Cursor.visible = !locked;
    }
}