/*********************************************************************************************
 * COMPONENT OF: staff counter inside the Corner Store (made by the setup tool)
 * REQUIRED DEPENDENCIES: Job (base class), a ScriptedWalker customer, a spawn spot and a
 *                        spot in front of the counter
 * DESCRIPTION: Store clerk job. Customers walk up to the counter one at a time; press E to
 *              serve each one. Serve the whole shift for a bonus. Leaving ends the shift.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.0
 *********************************************************************************************/
using UnityEngine;

public class ClerkJob : Job
{
    [Header("Customers")]
    public ScriptedWalker customer;
    // Where customers come from and leave to
    public Transform customerSpawn;
    // Where customers stand to be served
    public Transform customerSpot;
    public int customersPerShift = 5;
    // Seconds before the next customer comes
    public float timeBetweenCustomers = 2f;
    // Walking farther than this from the counter ends the shift
    public float maxAwayDistance = 7f;
    public AudioClip serveSound;

    [Header("Pay")]
    public int payPerCustomer = 3;
    public int shiftBonus = 5;

    private int served;
    private bool customerWaiting;
    private float nextCustomerAt;
    private bool customerOnTheWay;

    void Update()
    {
        SendCustomerWhenReady();
        CheckCustomerArrived();
        CheckWorkerLeft();
    }

    protected override string ActivePrompt()
    {
        return customerWaiting ? "Press E to serve the customer" : "Wait for the next customer...";
    }

    protected override void StartWork()
    {
        served = 0;
        customerWaiting = false;
        nextCustomerAt = Time.time + 1f;
        GameHUD.Say("Your shift started! Serve " + customersPerShift + " customers.");
        UpdateObjective();
    }

    // Pressing E at the counter serves the waiting customer
    protected override void InteractWhileWorking(PlayerInteractor player)
    {
        if (!customerWaiting)
        {
            GameHUD.Say("No customer yet. Wait a moment.");
            return;
        }

        served++;
        customerWaiting = false;
        player.PlaySound(serveSound);
        customer.WalkTo(customerSpawn.position);

        if (served >= customersPerShift)
        {
            FinishJob(served * payPerCustomer + shiftBonus, "Great shift!");
            return;
        }

        nextCustomerAt = Time.time + timeBetweenCustomers;
        UpdateObjective();
    }

    void SendCustomerWhenReady()
    {
        if (!IsActive || customerWaiting || customerOnTheWay || customer == null || Time.time < nextCustomerAt)
            return;

        customer.ResetTo(customerSpawn.position);
        customer.WalkTo(customerSpot.position);
        customerOnTheWay = true;
    }

    void CheckCustomerArrived()
    {
        if (!customerOnTheWay || !customer.Arrived)
            return;

        customerOnTheWay = false;
        customerWaiting = true;
        GameHUD.Say("A customer is waiting! Press E to serve them.", 2f);
    }

    void CheckWorkerLeft()
    {
        if (!IsActive || Worker == null)
            return;

        if (Vector3.Distance(Worker.transform.position, transform.position) > maxAwayDistance)
        {
            customerWaiting = false;
            customerOnTheWay = false;
            customer.ResetTo(customerSpawn.position);
            FinishJob(served * payPerCustomer, "You left your shift early.");
        }
    }

    void UpdateObjective()
    {
        GameHUD.Objective("Store clerk: " + served + "/" + customersPerShift + " customers served");
    }
}
