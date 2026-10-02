/*********************************************************************************************
 * COMPONENT OF: job helpers: the taxi passenger and the store customer (made by the setup tool)
 * REQUIRED DEPENDENCIES: everything CityPerson needs
 * DESCRIPTION: A person that does what a job tells it: follow the player, walk to a spot,
 *              or jump back to a starting spot. Can still be knocked over (see CityPerson).
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.0
 *********************************************************************************************/
using UnityEngine;

public class ScriptedWalker : CityPerson
{
    [Header("Moving")]
    // Fast enough to keep up with a rolling player
    public float followSpeed = 3.4f;
    public float walkSpeed = 1.4f;
    // How often (in seconds) a follower checks where the player went
    public float repathTime = 0.3f;

    private Transform followTarget;
    private float keepDistance;
    private float nextRepath;

    // True when the person has reached the spot they were told to walk to
    public bool Arrived => CanWalk && followTarget == null && !Agent.pathPending && Agent.remainingDistance <= Agent.stoppingDistance + 0.3f;

    void Update()
    {
        GetUpWhenReady();
        Follow();
        UpdateAnimation();
    }

    public void FollowTarget(Transform target, float distance)
    {
        followTarget = target;
        keepDistance = distance;
        Agent.speed = followSpeed;
    }

    public void WalkTo(Vector3 spot)
    {
        followTarget = null;
        if (!CanWalk)
            return;

        Agent.speed = walkSpeed;
        Agent.SetDestination(spot);
    }

    // Instantly puts the person back at a spot and makes them stand still
    public void ResetTo(Vector3 spot)
    {
        followTarget = null;
        if (Agent.enabled)
            Agent.Warp(spot);
        else
            transform.position = spot;

        if (CanWalk)
            Agent.ResetPath();
    }

    // Walks toward the target, stopping a little behind it
    void Follow()
    {
        if (followTarget == null || !CanWalk || Time.time < nextRepath)
            return;

        nextRepath = Time.time + repathTime;
        float distance = Vector3.Distance(transform.position, followTarget.position);

        if (distance > keepDistance)
            Agent.SetDestination(followTarget.position);
        else
            Agent.ResetPath();
    }
}
