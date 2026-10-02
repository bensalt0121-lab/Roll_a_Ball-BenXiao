/*********************************************************************************************
 * COMPONENT OF: Sanitation board next to the garbage truck (made by the setup tool)
 * REQUIRED DEPENDENCIES: Job (base class), a trash bag prefab, spots where bags can appear
 * DESCRIPTION: Trash pickup job. Trash bags appear around the city; roll into each one to
 *              pick it up, then come back to the board to get paid for every bag.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.0
 *********************************************************************************************/
using System.Collections.Generic;
using UnityEngine;

public class TrashJob : Job
{
    [Header("Trash")]
    // The bag that appears around the city
    public GameObject bagPrefab;
    // Places bags can appear. The setup tool fills this list in.
    public Transform[] bagSpots;
    public int bagsPerShift = 6;
    // How close the player must roll to a bag to pick it up
    public float pickUpDistance = 1.3f;
    public AudioClip pickUpSound;

    [Header("Pay")]
    public int payPerBag = 4;
    public int finishBonus = 6;

    private readonly List<GameObject> bags = new List<GameObject>();
    private int collected;

    public int BagsLeft => bags.Count;

    void Update()
    {
        PickUpNearbyBags();
    }

    protected override string ActivePrompt()
    {
        return bags.Count > 0 ? "Collect the trash bags (" + collected + " picked up)" : "Press E to hand in " + collected + " bags";
    }

    // Puts bags on random spots around the city
    protected override void StartWork()
    {
        collected = 0;
        List<Transform> spots = new List<Transform>(bagSpots);

        for (int i = 0; i < bagsPerShift && spots.Count > 0; i++)
        {
            int index = Random.Range(0, spots.Count);
            Vector3 spot = spots[index].position + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f));
            spots.RemoveAt(index);
            bags.Add(Instantiate(bagPrefab, spot, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f)));
        }

        GameHUD.Say("Trash pickup started! Find " + bags.Count + " trash bags (they show on the map).");
        UpdateObjective();
    }

    // Coming back to the board with all bags finishes the shift
    protected override void InteractWhileWorking(PlayerInteractor player)
    {
        if (bags.Count > 0)
        {
            GameHUD.Say(bags.Count + " bags are still out there!");
            return;
        }

        FinishJob(collected * payPerBag + finishBonus, "The city is clean!");
    }

    void PickUpNearbyBags()
    {
        if (!IsActive || Worker == null)
            return;

        for (int i = bags.Count - 1; i >= 0; i--)
        {
            if (bags[i] == null || Vector3.Distance(bags[i].transform.position, Worker.transform.position) > pickUpDistance)
                continue;

            Destroy(bags[i]);
            bags.RemoveAt(i);
            collected++;
            Worker.PlaySound(pickUpSound);
            UpdateObjective();
        }
    }

    void UpdateObjective()
    {
        if (bags.Count > 0)
            GameHUD.Objective("Trash pickup: " + bags.Count + " bags left");
        else
            GameHUD.Objective("Bring the " + collected + " bags back to the garbage truck");
    }
}
