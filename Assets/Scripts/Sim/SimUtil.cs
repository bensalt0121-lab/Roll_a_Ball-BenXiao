/*********************************************************************************************
 * COMPONENT OF: (helper, not a component) used by WantedLevel, Hospital, Teleporter
 * REQUIRED DEPENDENCIES: none
 * DESCRIPTION: Small shared helpers, like moving the player somewhere instantly.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.0
 *********************************************************************************************/
using UnityEngine;

public static class SimUtil
{
    // Moves an object with a Rigidbody (like the player) and stops it from rolling
    public static void Teleport(Transform target, Vector3 position)
    {
        Rigidbody body = target.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = position;
        }

        target.position = position;
    }

    // Distance on the ground, ignoring height
    public static float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0f;
        b.y = 0f;
        return Vector3.Distance(a, b);
    }
}
