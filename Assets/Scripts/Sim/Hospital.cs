/*********************************************************************************************
 * COMPONENT OF: Hospital (made by the setup tool, in the Downtown block)
 * REQUIRED DEPENDENCIES: a release point outside the hospital, MoneyManager, PlayerNeeds
 * DESCRIPTION: When the player gets hit hard by a car they wake up here, pay a hospital
 *              bill, and get some food and water so they can keep going.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.0
 *********************************************************************************************/
using UnityEngine;

public class Hospital : MonoBehaviour
{
    // Lets any script reach this with Hospital.Instance
    public static Hospital Instance { get; private set; }

    // Where the player wakes up
    public Transform releasePoint;

    [Header("Bill")]
    // Part of the player's money the bill costs (0.15 = 15%), but at least minimumBill
    public float billPercent = 0.15f;
    public int minimumBill = 20;

    [Header("Care")]
    // The hospital fills hunger and thirst up to at least this much (out of 100)
    public float refillTo = 50f;
    public AudioClip sound;

    void Awake()
    {
        Instance = this;
    }

    public void Admit(PlayerInteractor player, string reason)
    {
        int bill = ChargeBill(player);
        Refill(player);

        if (releasePoint != null)
            SimUtil.Teleport(player.transform, releasePoint.position);

        if (sound != null)
            AudioSource.PlayClipAtPoint(sound, player.transform.position);

        GameHUD.Say(reason + "! You woke up in the hospital. Bill: $" + bill + ".", 5f);
    }

    int ChargeBill(PlayerInteractor player)
    {
        if (player.Money == null)
            return 0;

        int bill = Mathf.Max(minimumBill, Mathf.RoundToInt(player.Money.currentMoney * billPercent));
        int paid = Mathf.Min(bill, player.Money.currentMoney);
        player.Money.RemoveMoney(paid);
        return paid;
    }

    void Refill(PlayerInteractor player)
    {
        if (player.Needs == null)
            return;

        player.Needs.Eat(Mathf.Max(0f, refillTo - player.Needs.Hunger));
        player.Needs.Drink(Mathf.Max(0f, refillTo - player.Needs.Thirst));
    }
}
