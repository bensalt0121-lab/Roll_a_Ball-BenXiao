/*********************************************************************************************
 * COMPONENT OF: Game Systems
 * REQUIRED DEPENDENCIES: GameHUD (shows the stars), PoliceOfficer NPCs (chase the player),
 *                        a release point outside the police station
 * DESCRIPTION: The police wanted level (0 to 5 stars). Knocking people over adds stars.
 *              Stars go away slowly if the player stays out of trouble. If the police catch
 *              the player, they pay a fine and are sent to the police station.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.0
 *********************************************************************************************/
using UnityEngine;

public class WantedLevel : MonoBehaviour
{
    // Lets any script reach this with WantedLevel.Instance
    public static WantedLevel Instance { get; private set; }

    [Header("Stars")]
    public int maxStars = 5;
    // Seconds without a new crime before one star goes away
    public float secondsToLoseStar = 20f;

    [Header("Getting Busted")]
    // Part of the player's money taken as a fine (0.25 = 25%)
    public float finePercent = 0.25f;
    public int minimumFine = 10;
    // Where the player is sent after being busted
    public Transform releasePoint;

    [Header("Sound")]
    public AudioClip crimeSound;
    public AudioClip bustedSound;

    public int Stars { get; private set; }

    private float lastCrimeTime;

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        LoseStarsOverTime();
    }

    // Called when the player does something illegal
    public void ReportCrime(int stars, string crime, Vector3 where)
    {
        Stars = Mathf.Min(maxStars, Stars + stars);
        lastCrimeTime = Time.time;

        if (crimeSound != null)
            AudioSource.PlayClipAtPoint(crimeSound, where);

        GameHUD.Say("WANTED: " + crime + "! Get away from the police!", 3f);
    }

    // Called by a police officer who catches the player
    public void Bust(PlayerInteractor player)
    {
        int finePaid = ChargeFine(player);
        Stars = 0;
        SendToReleasePoint(player.transform);

        if (bustedSound != null)
            AudioSource.PlayClipAtPoint(bustedSound, player.transform.position);

        GameHUD.Say("BUSTED! You paid a $" + finePaid + " fine.", 5f);
    }

    // The black market disguise: the police forget about the player
    public void ClearStars()
    {
        Stars = 0;
    }

    // Removes one star every few seconds while the player behaves
    void LoseStarsOverTime()
    {
        if (Stars == 0 || Time.time - lastCrimeTime < secondsToLoseStar)
            return;

        Stars--;
        lastCrimeTime = Time.time;

        if (Stars == 0)
            GameHUD.Say("The police lost you.");
    }

    int ChargeFine(PlayerInteractor player)
    {
        if (player.Money == null)
            return 0;

        int fine = Mathf.Max(minimumFine, Mathf.RoundToInt(player.Money.currentMoney * finePercent));
        int paid = Mathf.Min(fine, player.Money.currentMoney);
        player.Money.RemoveMoney(paid);
        return paid;
    }

    // Moves the player to the release point and stops them
    void SendToReleasePoint(Transform player)
    {
        if (releasePoint != null)
            SimUtil.Teleport(player, releasePoint.position);
    }
}
