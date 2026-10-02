/*********************************************************************************************
 * COMPONENT OF: the sewer entrance and the ladder in the black market (made by the setup tool)
 * REQUIRED DEPENDENCIES: PlayerInteractor on the Player, a destination point
 * DESCRIPTION: Press E to go somewhere else instantly, like climbing down the sewer to the
 *              hidden black market and back up again.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.0
 *********************************************************************************************/
using UnityEngine;

public class Teleporter : Interactable
{
    public string prompt = "Press E to climb down into the sewer";
    public Transform destination;
    // Shown on screen after arriving (leave empty for none)
    public string arriveMessage = "";
    public AudioClip sound;

    public override string GetPrompt(PlayerInteractor player)
    {
        return prompt;
    }

    public override void Interact(PlayerInteractor player)
    {
        if (destination == null)
            return;

        SimUtil.Teleport(player.transform, destination.position);
        player.PlaySound(sound);

        if (arriveMessage != "")
            GameHUD.Say(arriveMessage, 4f);
    }
}
