/*********************************************************************************************
 * COMPONENT OF: "Goal Beacons" (creates itself when the game starts - no setup needed)
 * REQUIRED DEPENDENCIES: HomeProperty doors in the scene, GuideBeam
 * DESCRIPTION: A tall purple beam over the next house you can buy (the cheapest one you do not
 *              own yet), so you always know where your goal is. It moves to the Dream Villa
 *              after you buy the first house and disappears when you own everything.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.0
 *********************************************************************************************/
using UnityEngine;

public class GoalBeacons : MonoBehaviour
{
    public static readonly Color HouseColor = new Color(0.8f, 0.4f, 1f);

    // The house the player should save up for next (null when they own every house)
    public static HomeProperty NextHouse { get; private set; }

    private HomeProperty[] houses;
    private GameObject houseBeam;
    private float nextCheck;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateAutomatically()
    {
        if (FindAnyObjectByType<GoalBeacons>() != null || FindAnyObjectByType<PlayerInteractor>() == null)
            return;

        new GameObject("Goal Beacons").AddComponent<GoalBeacons>();
    }

    void Start()
    {
        houses = FindObjectsByType<HomeProperty>();
        houseBeam = GuideBeam.Create("House Goal Beam", HouseColor, 80f);
        houseBeam.SetActive(false);
    }

    void Update()
    {
        if (Time.unscaledTime < nextCheck)
            return;

        nextCheck = Time.unscaledTime + 0.5f;
        UpdateHouseBeam();
    }

    // Finds the cheapest house the player does not own yet and puts the beam over it
    void UpdateHouseBeam()
    {
        NextHouse = null;
        foreach (HomeProperty house in houses)
        {
            if (house == null || house.Owned || house.price <= 0)
                continue;

            if (NextHouse == null || house.price < NextHouse.price)
                NextHouse = house;
        }

        houseBeam.SetActive(NextHouse != null);
        if (NextHouse != null)
            houseBeam.transform.position = NextHouse.transform.position;
    }
}
