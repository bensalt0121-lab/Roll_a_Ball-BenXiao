/*********************************************************************************************
 * COMPONENT OF: (base class) NpcWalker and PoliceOfficer build on top of this
 * REQUIRED DEPENDENCIES: NavMeshAgent and Rigidbody on the same object, a baked or runtime
 *                        NavMesh (RuntimeNavMesh), an Animator with "Speed" and "Down"
 *                        parameters on the character model, WantedLevel
 * DESCRIPTION: Everything every person in the city shares: walking on the NavMesh, playing
 *              walk/idle animations, and falling over (with real physics) when the player
 *              rolls into them fast, which also makes the player wanted.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.1
 * VERSION 1.1: KnockOver is public so the pistol can knock people over too.
 *********************************************************************************************/
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Rigidbody))]
public abstract class CityPerson : MonoBehaviour
{
    [Header("Getting Knocked Over")]
    // How fast the player must be rolling to knock this person over
    public float knockOverSpeed = 1.2f;
    // How hard the person is pushed when hit
    public float knockForce = 3f;
    // Seconds before the person gets back up
    public float getUpTime = 4f;
    // Wanted stars the player gets for doing it, and the crime shown on screen
    public int crimeStars = 1;
    public string crimeName = "You knocked someone over";
    public AudioClip hitSound;

    [Header("Animation")]
    // Animator on the character model (needs "Speed" float and "Down" bool parameters)
    public Animator animator;

    public bool IsDown { get; private set; }

    protected NavMeshAgent Agent { get; private set; }
    protected Rigidbody Body { get; private set; }

    // True when the person is standing on the NavMesh and able to walk
    protected bool CanWalk => Agent.enabled && Agent.isOnNavMesh && !IsDown;

    private float getUpAt;

    protected virtual void Awake()
    {
        Agent = GetComponent<NavMeshAgent>();
        Body = GetComponent<Rigidbody>();

        // While walking the NavMeshAgent moves the person, so physics is switched off
        Body.isKinematic = true;
        Agent.enabled = false;
    }

    protected virtual void Start()
    {
        JoinNavMesh();
    }

    // Puts the person on the closest point of the NavMesh and turns on walking
    protected void JoinNavMesh()
    {
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 5f, NavMesh.AllAreas))
        {
            transform.position = hit.position;
            Agent.enabled = true;
        }
    }

    // Tells the Animator how fast the person is moving and if they are lying down
    protected void UpdateAnimation()
    {
        if (animator == null)
            return;

        float speed = Agent.enabled ? Agent.velocity.magnitude : 0f;
        animator.SetFloat("Speed", speed);
        animator.SetBool("Down", IsDown);
    }

    // Stands the person back up after a few seconds on the ground
    protected void GetUpWhenReady()
    {
        if (!IsDown || Time.time < getUpAt)
            return;

        Body.linearVelocity = Vector3.zero;
        Body.angularVelocity = Vector3.zero;
        Body.isKinematic = true;
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);

        IsDown = false;
        JoinNavMesh();
    }

    // Unity calls this when something bumps into this person
    void OnCollisionEnter(Collision collision)
    {
        if (IsDown || !collision.collider.CompareTag("Player"))
            return;

        if (collision.relativeVelocity.magnitude < knockOverSpeed)
            return;

        KnockOver(collision.transform.position, knockForce, crimeStars, crimeName);
    }

    // Turns on physics so the person falls over, and reports the crime (also used by the pistol)
    public void KnockOver(Vector3 hitFrom, float force, int stars, string crime)
    {
        IsDown = true;
        getUpAt = Time.time + getUpTime;

        Agent.enabled = false;
        Body.isKinematic = false;

        Vector3 pushDirection = transform.position - hitFrom;
        pushDirection.y = 0f;
        Body.AddForce((pushDirection.normalized + Vector3.up * 0.5f) * force, ForceMode.VelocityChange);

        if (hitSound != null)
            AudioSource.PlayClipAtPoint(hitSound, transform.position);

        if (WantedLevel.Instance != null)
            WantedLevel.Instance.ReportCrime(stars, crime, transform.position);
    }
}
