/*********************************************************************************************
 * COMPONENT OF: Minimap Camera (made by the setup tool)
 * REQUIRED DEPENDENCIES: Camera on the same object, the Player, a RawImage on the HUD,
 *                        PerformanceManager (how often to redraw)
 * DESCRIPTION: A camera high above the player that looks straight down. Its picture is
 *              shown in the corner of the screen like the map in GTA. It turns with the main
 *              camera so "up" on the map is the way the player is looking. To save speed it
 *              only redraws a few times per second instead of every frame.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.1
 * VERSION 1.1: Redraws a few times per second (set by PerformanceManager) to reduce lag.
 *********************************************************************************************/
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Camera))]
public class MinimapCamera : MonoBehaviour
{
    [Header("Follow")]
    public Transform target;
    // Main camera; the map turns to match the way it faces
    public Transform viewCamera;
    public float height = 60f;
    // Half the width of the area shown on the map, in meters
    public float viewSize = 22f;

    [Header("Display")]
    // The HUD image that shows the map
    public RawImage displayImage;
    public int textureSize = 256;

    private Camera mapCamera;
    private float nextDraw;

    void Start()
    {
        CreateMapTexture();
    }

    // LateUpdate runs after the player has moved this frame
    void LateUpdate()
    {
        FollowTarget();
        DrawWhenItIsTime();
    }

    // The camera draws into a texture, and the HUD image shows that texture
    void CreateMapTexture()
    {
        mapCamera = GetComponent<Camera>();
        mapCamera.orthographic = true;
        mapCamera.orthographicSize = viewSize;
        // We call Render() ourselves, only a few times per second
        mapCamera.enabled = false;

        RenderTexture texture = new RenderTexture(textureSize, textureSize, 16);
        mapCamera.targetTexture = texture;

        if (displayImage != null)
            displayImage.texture = texture;
    }

    void FollowTarget()
    {
        if (target == null)
            return;

        transform.position = target.position + Vector3.up * height;

        float yaw = viewCamera != null ? viewCamera.eulerAngles.y : 0f;
        transform.rotation = Quaternion.Euler(90f, yaw, 0f);
    }

    void DrawWhenItIsTime()
    {
        if (Time.unscaledTime < nextDraw)
            return;

        nextDraw = Time.unscaledTime + 1f / Mathf.Max(1f, PerformanceManager.Current.minimapFps);
        mapCamera.Render();
    }
}
