using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Player")]
    [SerializeField] private Transform player;

    [Header("Camera")]
    [SerializeField] private float distance = 5f;
    [SerializeField] private float height = 2f;

    [Header("Mouse")]
    [SerializeField] private float sensitivity = 0.1f;

    [Header("Vertical Rotation")]
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 70f;

    private float yaw;
    private float pitch = 15f;

    private void Start()
    {
        LockMouse();
    }

    private void Update()
    {
        // ESC unlocks mouse
        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            UnlockMouse();
        }

        // Left click locks it again
        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame)
        {
            LockMouse();
        }
    }

    private void LateUpdate()
    {
        if (player == null || Mouse.current == null)
            return;

        // Mouse movement
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        yaw += mouseDelta.x * sensitivity;
        pitch -= mouseDelta.y * sensitivity;

        pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

        Quaternion rotation =
            Quaternion.Euler(pitch, yaw, 0f);

        Vector3 offset =
            rotation * new Vector3(0f, 0f, -distance);

        transform.position =
            player.position +
            Vector3.up * height +
            offset;

        transform.LookAt(
            player.position + Vector3.up * height
        );
    }

    private void LockMouse()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void UnlockMouse()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}