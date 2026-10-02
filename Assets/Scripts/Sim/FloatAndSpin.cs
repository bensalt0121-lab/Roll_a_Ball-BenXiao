/*********************************************************************************************
 * COMPONENT OF: job markers and map icons (made by the setup tool)
 * REQUIRED DEPENDENCIES: none
 * DESCRIPTION: Makes an object bob up and down and turn slowly so the player notices it.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.0
 *********************************************************************************************/
using UnityEngine;

public class FloatAndSpin : MonoBehaviour
{
    // Degrees per second around the y-axis
    public float spinSpeed = 60f;
    // How far up and down it moves, and how fast
    public float floatHeight = 0.25f;
    public float floatSpeed = 2f;

    private Vector3 startLocalPosition;

    void OnEnable()
    {
        startLocalPosition = transform.localPosition;
    }

    void OnDisable()
    {
        // Put it back so moving the object while hidden works correctly
        transform.localPosition = startLocalPosition;
    }

    void Update()
    {
        Spin();
        Float();
    }

    void Spin()
    {
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);
    }

    void Float()
    {
        float offset = Mathf.Sin(Time.time * floatSpeed) * floatHeight;
        transform.localPosition = startLocalPosition + Vector3.up * offset;
    }
}
