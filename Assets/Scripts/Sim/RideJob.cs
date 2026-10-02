/*********************************************************************************************
 * COMPONENT OF: Taxi Stand (made by the setup tool)
 * REQUIRED DEPENDENCIES: Job (base class), a ScriptedWalker passenger, destination points,
 *                        a yellow marker
 * DESCRIPTION: Taxi / ride job. A passenger waits at the taxi stand. Start the job and the
 *              passenger follows you; lead them to the yellow marker to get paid. Roll too
 *              far ahead and you lose them.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.0
 *********************************************************************************************/
using UnityEngine;

public class RideJob : Job
{
    [Header("Ride")]
    public ScriptedWalker passenger;
    // Places passengers want to go. The setup tool fills this list in.
    public Transform[] destinations;
    public GameObject marker;
    // How close the passenger must get to the marker
    public float arriveDistance = 3f;
    // If the passenger is farther than this from the player, the ride is lost
    public float loseDistance = 25f;
    // Destinations closer than this to the stand are skipped
    public float minimumTripLength = 20f;

    [Header("Pay")]
    public int basePay = 15;
    public int payPer10Meters = 4;

    private Transform target;
    private int pay;
    private Vector3 standSpot;

    void Start()
    {
        HideMarker();
        if (passenger != null)
            standSpot = passenger.transform.position;
    }

    void Update()
    {
        CheckRide();
    }

    protected override string ActivePrompt()
    {
        return "Lead your passenger to the yellow marker";
    }

    protected override void StartWork()
    {
        if (passenger == null || destinations == null || destinations.Length == 0)
        {
            FinishJob(0, "No passengers right now.");
            return;
        }

        target = PickDestination();
        pay = basePay + Mathf.RoundToInt(Vector3.Distance(transform.position, target.position) / 10f) * payPer10Meters;

        passenger.FollowTarget(Worker.transform, 1.8f);
        ShowMarker(target.position);
        GameHUD.Say("Your passenger is following you! Don't roll too far ahead.");
        GameHUD.Objective("Taxi: lead your passenger to the yellow marker ($" + pay + ")");
    }

    // A random destination that is not too close to the stand
    Transform PickDestination()
    {
        Transform best = destinations[0];
        for (int i = 0; i < 10; i++)
        {
            best = destinations[Random.Range(0, destinations.Length)];
            if (Vector3.Distance(best.position, transform.position) >= minimumTripLength)
                break;
        }
        return best;
    }

    void CheckRide()
    {
        if (!IsActive || target == null || passenger == null || Worker == null)
            return;

        if (Vector3.Distance(passenger.transform.position, Worker.transform.position) > loseDistance)
        {
            EndRide();
            FinishJob(0, "You lost your passenger!");
            return;
        }

        Vector3 offset = passenger.transform.position - target.position;
        offset.y = 0f;
        if (offset.magnitude <= arriveDistance)
        {
            EndRide();
            FinishJob(pay, "Your passenger arrived safely!");
        }
    }

    // The passenger goes back to the stand so the next ride can start there
    void EndRide()
    {
        target = null;
        HideMarker();
        passenger.ResetTo(standSpot);
    }

    void ShowMarker(Vector3 position)
    {
        if (marker == null)
            return;

        marker.transform.position = position;
        marker.SetActive(true);
    }

    void HideMarker()
    {
        if (marker != null)
            marker.SetActive(false);
    }
}
