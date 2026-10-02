/*********************************************************************************************
 * COMPONENT OF: police officer NPCs at the Police Station (made by the setup tool)
 * REQUIRED DEPENDENCIES: everything CityPerson needs, WantedLevel, Player with
 *                        PlayerInteractor
 * DESCRIPTION: Waits at the station. When the player is wanted, chases them (faster with
 *              more stars) and busts them if close enough. Knocking an officer over is a
 *              bigger crime.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.1
 * VERSION 1.1: Easier: slower, must get closer, and gives up when you are far away.
 *********************************************************************************************/
using UnityEngine;

public class PoliceOfficer : CityPerson
{
    [Header("Police")]
    public float patrolSpeed = 1.2f;
    // Chase speed with 1 star. Each extra star adds chaseSpeedPerStar.
    public float chaseSpeed = 1.7f;
    public float chaseSpeedPerStar = 0.1f;
    // How close (in meters) the officer must get to catch the player
    public float catchDistance = 0.9f;
    // If the player gets this far away, the officer gives up and goes back
    public float giveUpDistance = 35f;
    // How often (in seconds) the officer checks where the player went
    public float repathTime = 0.4f;

    private PlayerInteractor player;
    private Vector3 stationSpot;
    private float nextRepath;

    protected override void Start()
    {
        base.Start();
        stationSpot = transform.position;
        player = FindAnyObjectByType<PlayerInteractor>();
    }

    void Update()
    {
        GetUpWhenReady();
        ChaseOrReturn();
        TryToCatchPlayer();
        UpdateAnimation();
    }

    bool PlayerIsWanted => WantedLevel.Instance != null && WantedLevel.Instance.Stars > 0;

    // Runs at the player while they are wanted, otherwise walks back to the station
    void ChaseOrReturn()
    {
        if (!CanWalk || Time.time < nextRepath)
            return;

        nextRepath = Time.time + repathTime;

        bool tooFar = player != null && Vector3.Distance(transform.position, player.transform.position) > giveUpDistance;
        if (PlayerIsWanted && player != null && !tooFar)
        {
            Agent.speed = chaseSpeed + chaseSpeedPerStar * (WantedLevel.Instance.Stars - 1);
            Agent.SetDestination(player.transform.position);
        }
        else
        {
            Agent.speed = patrolSpeed;
            Agent.SetDestination(stationSpot);
        }
    }

    // Busts the player when the officer reaches them (height is ignored)
    void TryToCatchPlayer()
    {
        if (!CanWalk || !PlayerIsWanted || player == null)
            return;

        Vector3 offset = player.transform.position - transform.position;
        offset.y = 0f;

        if (offset.magnitude <= catchDistance)
            WantedLevel.Instance.Bust(player);
    }
}
