/*********************************************************************************************
 * COMPONENT OF: front doors of the Starter Apartment and the houses for sale
 * REQUIRED DEPENDENCIES: PlayerInteractor on the Player, MoneyManager, GameHUD,
 *                        DayNightCycle (for sleeping)
 * DESCRIPTION: A home the player can own. If it is for sale, press E to buy it. If the
 *              player owns it, press E to sleep until morning. Buying the dream home wins.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.0
 *********************************************************************************************/
using UnityEngine;

public class HomeProperty : Interactable
{
    [Header("Property")]
    public string propertyName = "Starter Apartment";
    public int price = 0;
    // The starter apartment is owned from the beginning
    public bool ownedAtStart = false;
    // Buying this home finishes the game's main goal
    public bool isDreamHome = false;

    [Header("Signs")]
    // Shown while the home is for sale, and after it is bought
    public GameObject forSaleSign;
    public GameObject ownedSign;

    [Header("Sleeping")]
    // The clock jumps to this hour after sleeping (8 = 8 AM)
    public float wakeUpHour = 8f;

    [Header("Sound")]
    public AudioClip buySound;

    // The home the player owns right now
    public static HomeProperty CurrentHome { get; private set; }

    public bool Owned { get; private set; }

    void Start()
    {
        Owned = ownedAtStart;

        if (Owned)
            CurrentHome = this;

        UpdateSigns();
    }

    public override string GetPrompt(PlayerInteractor player)
    {
        if (Owned)
            return "Press E to sleep at your " + propertyName;

        return "Press E to buy " + propertyName + " - $" + price;
    }

    public override void Interact(PlayerInteractor player)
    {
        if (Owned)
            Sleep();
        else
            TryToBuy(player);
    }

    void TryToBuy(PlayerInteractor player)
    {
        if (player.Money == null || player.Money.currentMoney < price)
        {
            GameHUD.Say("You need $" + price + " to buy the " + propertyName + ".");
            return;
        }

        player.Money.RemoveMoney(price);
        Owned = true;
        CurrentHome = this;
        UpdateSigns();
        player.PlaySound(buySound);

        if (isDreamHome)
        {
            GameHUD.Say("YOU DID IT! You bought the " + propertyName + "!", 10f);
            GameHUD.Objective("You won! Keep exploring the city.");
        }
        else
        {
            GameHUD.Say("You bought the " + propertyName + "! It is your new home.", 5f);
        }
    }

    // Skips the clock to the morning
    void Sleep()
    {
        DayNightCycle clock = FindAnyObjectByType<DayNightCycle>();

        if (clock != null)
            clock.timeOfDay = wakeUpHour;

        GameHUD.Say("You slept until morning.");
    }

    void UpdateSigns()
    {
        if (forSaleSign != null)
            forSaleSign.SetActive(!Owned);

        if (ownedSign != null)
            ownedSign.SetActive(Owned);
    }
}
