/*********************************************************************************************
 * COMPONENT OF: traffic cars (made by the setup tool)
 * REQUIRED DEPENDENCIES: Rigidbody and a Collider on the car, a route of points on the roads,
 *                        an AudioSource for the engine, Hospital, sounds in
 *                        Assets/Resources/Audio/Cars/Engine and Cars/Horn
 * DESCRIPTION: A car that drives around a loop of road points forever. It brakes when the
 *              player, a person or another car is in front of it, and honks if it has to wait.
 *              If it hits the player while driving fast, the player goes to the hospital.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.1
 * VERSION 1.1: Engine sound, honking, and sending the player to the hospital when hit.
 *********************************************************************************************/
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class TrafficCar : MonoBehaviour
{
    [Header("Route")]
    // Points the car drives through, in order, then loops. The setup tool fills these in.
    public Vector3[] route;
    // Which point the car drives to first
    public int startIndex = 0;

    [Header("Driving")]
    // Meters per second
    public float speed = 5f;
    // Degrees per second the car body turns to face where it is going
    public float turnSpeed = 180f;

    [Header("Braking")]
    // How far ahead (in meters) the car looks for things to stop for
    public float lookAhead = 4f;
    // Half the width of the area the car checks in front of it
    public float checkRadius = 0.7f;
    // If another car blocks this one for this long, ignore cars for a moment (stops jams)
    public float maxWaitTime = 3f;

    [Header("Sounds")]
    public AudioSource engineSound;
    // Seconds stuck behind something before honking
    public float honkAfter = 1.5f;

    [Header("Hitting The Player")]
    // A hit faster than this (meters per second) sends the player to the hospital
    public float hospitalSpeed = 2.5f;

    // How fast the car is moving right now
    public float CurrentSpeed { get; private set; }

    private Rigidbody body;
    private int nextIndex;
    private float blockedTime;
    private float stoppedTime;
    private bool honked;
    private float ignoreCarsUntil;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        nextIndex = startIndex;
    }

    void Start()
    {
        StartEngineSound();
    }

    // Physics moves happen in FixedUpdate so the car pushes things smoothly
    void FixedUpdate()
    {
        Drive();
        UpdateEngineSound();
    }

    void Drive()
    {
        CurrentSpeed = 0f;
        if (route == null || route.Length < 2)
            return;

        Vector3 target = route[nextIndex];
        target.y = body.position.y;
        Vector3 toTarget = target - body.position;

        // Close enough to this point: aim for the next one
        if (toTarget.magnitude < 0.5f)
        {
            nextIndex = (nextIndex + 1) % route.Length;
            return;
        }

        FaceDirection(toTarget);

        if (PathIsBlocked())
        {
            WaitAndHonk();
            return;
        }

        stoppedTime = 0f;
        honked = false;
        float step = Mathf.Min(speed * Time.fixedDeltaTime, toTarget.magnitude);
        body.MovePosition(body.position + toTarget.normalized * step);
        CurrentSpeed = step / Time.fixedDeltaTime;
    }

    // Smoothly turns the car body toward where it is driving
    void FaceDirection(Vector3 direction)
    {
        Quaternion wanted = Quaternion.LookRotation(direction.normalized, Vector3.up);
        body.MoveRotation(Quaternion.RotateTowards(body.rotation, wanted, turnSpeed * Time.fixedDeltaTime));
    }

    // Looks ahead for the player, people and other cars
    bool PathIsBlocked()
    {
        Vector3 start = body.position + Vector3.up * 0.8f;
        RaycastHit[] hits = Physics.SphereCastAll(start, checkRadius, transform.forward, lookAhead, ~0, QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits)
        {
            if (hit.rigidbody == body)
                continue;

            if (IsSomethingToStopFor(hit.collider))
                return true;
        }

        blockedTime = 0f;
        return false;
    }

    bool IsSomethingToStopFor(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponentInParent<CityPerson>() != null)
            return true;

        if (other.GetComponentInParent<TrafficCar>() == null || Time.time < ignoreCarsUntil)
            return false;

        // Another car is in the way: wait, but not forever
        blockedTime += Time.fixedDeltaTime;
        if (blockedTime > maxWaitTime)
        {
            ignoreCarsUntil = Time.time + 2f;
            blockedTime = 0f;
        }

        return true;
    }

    // Honks once after waiting a little while
    void WaitAndHonk()
    {
        stoppedTime += Time.fixedDeltaTime;
        if (honked || stoppedTime < honkAfter)
            return;

        honked = true;
        AudioClip horn = SoundLibrary.Random("Cars/Horn");
        if (horn != null)
            AudioSource.PlayClipAtPoint(horn, transform.position, 0.6f);
    }

    void StartEngineSound()
    {
        if (engineSound == null)
            return;

        if (engineSound.clip == null)
            engineSound.clip = SoundLibrary.Random("Cars/Engine");

        if (engineSound.clip == null)
            return;

        engineSound.loop = true;
        engineSound.time = Random.Range(0f, engineSound.clip.length);
        engineSound.Play();
    }

    // The engine sounds higher when the car goes faster
    void UpdateEngineSound()
    {
        if (engineSound != null)
            engineSound.pitch = Mathf.Lerp(0.8f, 1.2f, CurrentSpeed / Mathf.Max(0.1f, speed));
    }

    // Unity calls this when something bumps into the car
    void OnCollisionEnter(Collision collision)
    {
        if (!collision.collider.CompareTag("Player") || CurrentSpeed < hospitalSpeed || Hospital.Instance == null)
            return;

        PlayerInteractor player = collision.collider.GetComponent<PlayerInteractor>();
        if (player != null)
            Hospital.Instance.Admit(player, "You got hit by a car");
    }
}
