/*********************************************************************************************
 * COMPONENT OF: counters in the hidden black market under the sewer (made by the setup tool)
 * REQUIRED DEPENDENCIES: PlayerInteractor, MoneyManager, PlayerWeapon on the Player,
 *                        WantedLevel
 * DESCRIPTION: Illegal shop items. The pistol lets the player shoot (left click). The
 *              disguise makes the police forget you (clears all wanted stars).
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.0
 *********************************************************************************************/
using UnityEngine;

public class BlackMarketCounter : Interactable
{
    public enum Item { Pistol, Disguise }

    [Header("Item For Sale")]
    public Item item = Item.Pistol;
    public int price = 150;
    public AudioClip buySound;

    public override string GetPrompt(PlayerInteractor player)
    {
        if (item == Item.Pistol)
        {
            PlayerWeapon weapon = player.GetComponent<PlayerWeapon>();
            if (weapon != null && weapon.hasPistol)
                return "You already have a pistol (left click to shoot)";

            return "Press E to buy a Pistol - $" + price;
        }

        return "Press E to buy a Disguise - $" + price + " (police forget you)";
    }

    public override void Interact(PlayerInteractor player)
    {
        PlayerWeapon weapon = player.GetComponent<PlayerWeapon>();
        if (item == Item.Pistol && weapon != null && weapon.hasPistol)
            return;

        if (player.Money == null || player.Money.currentMoney < price)
        {
            GameHUD.Say("Come back when you have $" + price + ".");
            return;
        }

        player.Money.RemoveMoney(price);
        player.PlaySound(buySound);
        GiveItem(player, weapon);
    }

    void GiveItem(PlayerInteractor player, PlayerWeapon weapon)
    {
        if (item == Item.Pistol)
        {
            if (weapon != null)
                weapon.GivePistol();

            GameHUD.Say("You bought a pistol. Left click to shoot. The police will not like it.", 5f);
            return;
        }

        if (WantedLevel.Instance != null)
            WantedLevel.Instance.ClearStars();

        GameHUD.Say("New disguise! The police don't recognize you anymore.", 4f);
    }
}
