/*********************************************************************************************
 * COMPONENT OF: Job Board at the Delivery Depot (made by the setup tool)
 * REQUIRED DEPENDENCIES: Job (base class), a marker object and a list of drop-off points
 * DESCRIPTION: Press E at the job board to pick up a package, roll it to the green marker
 *              somewhere in the city, and get paid. Farther away = more money.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.1
 * VERSION 1.1: Now uses the shared Job base class so only one job runs at a time.
 *********************************************************************************************/
using UnityEngine;

public class DeliveryJob : Job
{
    [Header("Delivery Spots")]
    // Places a package can be delivered to. The setup tool fills this list in.
    public Transform[] dropOffPoints;
    // Glowing marker that shows where to go
    public GameObject marker;
    // How close (in meters) the player must get to the marker to finish
    public float arriveDistance = 2.5f;

    [Header("Pay")]
    public int basePay = 10;
    // Extra money for every 10 meters between the depot and the drop-off
    public int payPer10Meters = 3;

    private Transform target;
    private int currentPay;
    private int lastIndex = -1;

    void Start()
    {
        HideMarker();
    }

    void Update()
    {
        CheckArrival();
    }

    protected override string ActivePrompt()
    {
        return "Delivery in progress - follow the green marker";
    }

    // Picks a drop-off spot, works out the pay and shows the marker
    protected override void StartWork()
    {
        if (dropOffPoints == null || dropOffPoints.Length == 0)
        {
            FinishJob(0, "No delivery spots are set up yet.");
            return;
        }

        target = PickDropOff();
        float distance = Vector3.Distance(transform.position, target.position);
        currentPay = basePay + Mathf.RoundToInt(distance / 10f) * payPer10Meters;

        ShowMarker(target.position);
        GameHUD.Say("Package picked up! Deliver it for $" + currentPay + ".");
        GameHUD.Objective("Deliver the package to the green marker ($" + currentPay + ")");
    }

    // Chooses a random spot, but never the same one twice in a row
    Transform PickDropOff()
    {
        int index = Random.Range(0, dropOffPoints.Length);

        if (dropOffPoints.Length > 1 && index == lastIndex)
            index = (index + 1) % dropOffPoints.Length;

        lastIndex = index;
        return dropOffPoints[index];
    }

    // Finishes the job when the player is close enough to the marker (height is ignored)
    void CheckArrival()
    {
        if (!IsActive || target == null || Worker == null)
            return;

        Vector3 offset = Worker.transform.position - target.position;
        offset.y = 0f;

        if (offset.magnitude <= arriveDistance)
        {
            target = null;
            HideMarker();
            FinishJob(currentPay, "Delivered!");
        }
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
