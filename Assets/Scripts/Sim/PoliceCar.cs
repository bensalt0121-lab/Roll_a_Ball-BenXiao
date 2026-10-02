/*********************************************************************************************
 * COMPONENT OF: police cars parked at the Police Station (made by the setup tool)
 * REQUIRED DEPENDENCIES: NavMeshAgent on the same object, a NavMesh (RuntimeNavMesh),
 *                        WantedLevel, siren lights, siren sound in
 *                        Assets/Resources/Audio/Cars/Siren
 * DESCRIPTION: Stays parked until the player has 4 or more wanted stars, then turns on its
 *              siren and chases the player. Gets close enough and the player is busted.
 *              Drives back to its parking spot when the stars go down.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.1
 * VERSION 1.1: Easier: only chases at 4 or more stars, a little slower.
 *********************************************************************************************/
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class PoliceCar : MonoBehaviour
{
    [Header("Chasing")]
    public int starsToChase = 4;
    public float chaseSpeed = 5f;
    public float returnSpeed = 4f;
    // How close the car must get to bust the player
    public float catchDistance = 2.4f;
    // How often (in seconds) the car checks where the player went
    public float repathTime = 0.5f;

    [Header("Siren")]
    // Two lights that flash one after the other (red and blue)
    public Light[] sirenLights;
    public float flashesPerSecond = 3f;
    public AudioSource sirenSound;

    private NavMeshAgent agent;
    private PlayerInteractor player;
    private Vector3 parkSpot;
    private Quaternion parkRotation;
    private float nextRepath;

    bool Chasing => WantedLevel.Instance != null && WantedLevel.Instance.Stars >= starsToChase;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        agent.enabled = false;
    }

    void Start()
    {
        parkSpot = transform.position;
        parkRotation = transform.rotation;
        player = FindAnyObjectByType<PlayerInteractor>();
        JoinNavMesh();
    }

    void Update()
    {
        Drive();
        TryToCatchPlayer();
        UpdateSiren();
    }

    void JoinNavMesh()
    {
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 6f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            agent.enabled = true;
        }
    }

    // Chases the player, or drives back and parks
    void Drive()
    {
        if (!agent.enabled || !agent.isOnNavMesh || Time.time < nextRepath)
            return;

        nextRepath = Time.time + repathTime;

        if (Chasing && player != null)
        {
            agent.speed = chaseSpeed;
            agent.SetDestination(player.transform.position);
            return;
        }

        agent.speed = returnSpeed;
        if (SimUtil.FlatDistance(transform.position, parkSpot) > 1f)
            agent.SetDestination(parkSpot);
        else
            transform.rotation = parkRotation;
    }

    void TryToCatchPlayer()
    {
        if (!Chasing || player == null)
            return;

        if (SimUtil.FlatDistance(transform.position, player.transform.position) <= catchDistance)
            WantedLevel.Instance.Bust(player);
    }

    // Flashes the lights and plays the siren only while chasing
    void UpdateSiren()
    {
        bool on = Chasing;
        bool firstLight = Mathf.Repeat(Time.time * flashesPerSecond, 1f) < 0.5f;

        if (sirenLights != null)
        {
            for (int i = 0; i < sirenLights.Length; i++)
            {
                if (sirenLights[i] != null)
                    sirenLights[i].enabled = on && (i % 2 == 0) == firstLight;
            }
        }

        if (sirenSound == null)
            return;

        if (on && !sirenSound.isPlaying)
        {
            if (sirenSound.clip == null)
                sirenSound.clip = SoundLibrary.Random("Cars/Siren");
            if (sirenSound.clip != null)
                sirenSound.Play();
        }
        else if (!on && sirenSound.isPlaying)
        {
            sirenSound.Stop();
        }
    }
}
