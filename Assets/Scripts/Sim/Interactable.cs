/*********************************************************************************************
 * COMPONENT OF: (base class) Shop, DeliveryJob and HomeProperty build on top of this
 * REQUIRED DEPENDENCIES: PlayerInteractor on the Player
 * DESCRIPTION: Anything the player can use with the E key. It keeps a list of every
 *              interactable in the scene so the player can find the closest one.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.0
 *********************************************************************************************/
using System.Collections.Generic;
using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    // Every interactable that is turned on right now
    public static readonly List<Interactable> All = new List<Interactable>();

    [Header("Interaction")]
    // How close (in meters) the player must be to use this
    public float interactRange = 2.5f;

    protected virtual void OnEnable()
    {
        All.Add(this);
    }

    protected virtual void OnDisable()
    {
        All.Remove(this);
    }

    // The text shown on screen, for example "Press E to buy Burger - $8"
    public abstract string GetPrompt(PlayerInteractor player);

    // What happens when the player presses E
    public abstract void Interact(PlayerInteractor player);
}
