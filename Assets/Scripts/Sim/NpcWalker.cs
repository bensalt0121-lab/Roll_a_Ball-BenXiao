/*********************************************************************************************
 * COMPONENT OF: city NPCs (people walking around, made by the setup tool)
 * REQUIRED DEPENDENCIES: everything CityPerson needs, plus a list of walk points
 * DESCRIPTION: A person who walks from spot to spot around the city, waits a little, then
 *              picks a new spot. Stops to watch street shows. Can be knocked over
 *              (see CityPerson).
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.1
 * VERSION 1.1: People can come and watch the street performer job.
 *********************************************************************************************/
using UnityEngine;

public class NpcWalker : CityPerson
{
    [Header("Walking")]
    public float walkSpeed = 1.3f;
    // Seconds the person waits before walking to the next spot
    public float minWaitTime = 1f;
    public float maxWaitTime = 5f;
    // Spots this person can walk to. The setup tool fills this list in.
    public Transform[] walkPoints;

    [Header("Watching Shows")]
    // How far from the show the person stands
    public float watchDistance = 4f;

    private float leaveAt;
    private bool waiting = true;
    private Transform show;
    private float watchUntil;

    protected override void Start()
    {
        base.Start();
        Agent.speed = walkSpeed;
        leaveAt = Time.time + Random.Range(0f, maxWaitTime);
    }

    void Update()
    {
        GetUpWhenReady();
        if (!WatchingShow())
            Wander();
        UpdateAnimation();
    }

    // Walks over to a show and stays there until it ends
    public void WatchShow(Transform showSpot, float seconds)
    {
        show = showSpot;
        watchUntil = Time.time + seconds;

        if (!CanWalk)
            return;

        Vector3 away = transform.position - showSpot.position;
        away.y = 0f;
        Vector3 standSpot = showSpot.position + (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward) * watchDistance;
        Agent.SetDestination(standSpot);
    }

    public bool IsWatching(Transform showSpot)
    {
        return show == showSpot && Time.time < watchUntil && !IsDown;
    }

    // While watching, turn to face the show; returns false when not watching
    bool WatchingShow()
    {
        if (show == null)
            return false;

        if (Time.time >= watchUntil)
        {
            show = null;
            waiting = true;
            leaveAt = Time.time + Random.Range(minWaitTime, maxWaitTime);
            return false;
        }

        bool arrived = CanWalk && !Agent.pathPending && Agent.remainingDistance <= Agent.stoppingDistance + 0.3f;
        if (arrived)
        {
            Vector3 look = show.position - transform.position;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(look), 5f * Time.deltaTime);
        }

        return true;
    }

    // Walks to a spot, waits there, then picks another one
    void Wander()
    {
        if (!CanWalk)
            return;

        if (waiting)
        {
            if (Time.time >= leaveAt)
                WalkToRandomPoint();
            return;
        }

        bool arrived = !Agent.pathPending && Agent.remainingDistance <= Agent.stoppingDistance + 0.2f;
        if (arrived)
        {
            waiting = true;
            leaveAt = Time.time + Random.Range(minWaitTime, maxWaitTime);
        }
    }

    void WalkToRandomPoint()
    {
        waiting = false;

        if (walkPoints != null && walkPoints.Length > 0)
        {
            Transform point = walkPoints[Random.Range(0, walkPoints.Length)];
            Agent.SetDestination(point.position);
        }
        else
        {
            // No walk points: wander somewhere within 15 meters
            Vector3 randomSpot = transform.position + Random.insideUnitSphere * 15f;
            Agent.SetDestination(randomSpot);
        }
    }
}
