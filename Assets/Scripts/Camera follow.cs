using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform player;

    [Header("Camera Settings")]
    public float distance = 5f;
    public float height = 2f;

    [Header("Mouse Settings")]
    public float sensitivity = 0.15f;
    public float minVerticalAngle = -30f;
    public float maxVerticalAngle = 70f;

    private float yaw = 0f;
    private float pitch = 15f;

    void LateUpdate()
    {
        if (player == null)
            return;

        // Get mouse movement
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        yaw += mouseDelta.x * sensitivity;
        pitch -= mouseDelta.y * sensitivity;

        // Prevent camera from flipping upside down
        pitch = Mathf.Clamp(pitch, minVerticalAngle, maxVerticalAngle);

        // Camera rotation
        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);

        // Position camera behind player
        Vector3 offset = rotation * new Vector3(0f, 0f, -distance);

        transform.position = player.position + Vector3.up * height + offset;

        // Look toward player
        transform.LookAt(player.position + Vector3.up * height);
    }
}