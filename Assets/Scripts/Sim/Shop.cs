/*********************************************************************************************
 * COMPONENT OF: shop counters (Burger Shop, Drink Shop, Corner Store) made by the setup tool
 * REQUIRED DEPENDENCIES: PlayerInteractor on the Player, MoneyManager, PlayerNeeds, GameHUD
 * DESCRIPTION: A counter that sells one food or drink. Pressing E pays the price and fills
 *              the hunger and/or thirst bar.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.0
 *********************************************************************************************/
using UnityEngine;

public class Shop : Interactable
{
    [Header("Item For Sale")]
    public string itemName = "Burger";
    public int price = 8;
    // How much of each bar the item fills back up (bars go from 0 to 100)
    public float hungerRestore = 35f;
    public float thirstRestore = 0f;

    [Header("Sound")]
    public AudioClip buySound;

    public override string GetPrompt(PlayerInteractor player)
    {
        return "Press E to buy " + itemName + " - $" + price;
    }

    public override void Interact(PlayerInteractor player)
    {
        if (!CanAfford(player))
        {
            GameHUD.Say("Not enough money! Get a delivery job at the Depot.");
            return;
        }

        Sell(player);
    }

    bool CanAfford(PlayerInteractor player)
    {
        return player.Money != null && player.Money.currentMoney >= price;
    }

    // Takes the money and fills the bars
    void Sell(PlayerInteractor player)
    {
        player.Money.RemoveMoney(price);

        if (player.Needs != null)
        {
            player.Needs.Eat(hungerRestore);
            player.Needs.Drink(thirstRestore);
        }

        player.PlaySound(buySound);
        GameHUD.Say("You bought " + itemName + "!");
    }
}
