/*********************************************************************************************
 * COMPONENT OF: Player
 * REQUIRED DEPENDENCIES: PlayerMovement on the Player (to slow down when starving)
 * DESCRIPTION: Hunger and thirst bars that slowly go down over time. Food and drinks fill
 *              them back up. When either bar is empty, the player rolls slower.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.1
 * VERSION 1.1: Softer needs: bars last about 7-8 minutes and the slow-down is smaller.
 *********************************************************************************************/
using UnityEngine;

public class PlayerNeeds : MonoBehaviour
{
    [Header("Hunger")]
    // A full hunger bar. 100 = not hungry at all
    public float maxHunger = 100f;
    // Points lost every second (0.2 = empty after about 8 minutes)
    public float hungerLossPerSecond = 0.2f;

    [Header("Thirst")]
    public float maxThirst = 100f;
    // Points lost every second (0.25 = empty after about 7 minutes)
    public float thirstLossPerSecond = 0.25f;

    [Header("Empty Bar Penalty")]
    // When a bar is empty the player rolls at this part of normal speed (0.7 = 70% speed)
    public float emptySpeedMultiplier = 0.7f;

    public float Hunger { get; private set; }
    public float Thirst { get; private set; }

    // How full each bar is from 0 to 1, used by the HUD
    public float HungerPercent => Hunger / maxHunger;
    public float ThirstPercent => Thirst / maxThirst;

    private PlayerMovement movement;
    private float normalSpeed;
    private bool wasEmpty;

    void Start()
    {
        Hunger = maxHunger;
        Thirst = maxThirst;

        movement = GetComponent<PlayerMovement>();
        if (movement != null)
            normalSpeed = movement.moveSpeed;
    }

    void Update()
    {
        DrainNeeds();
        ApplyEmptyPenalty();
    }

    // Food fills the hunger bar back up
    public void Eat(float amount)
    {
        Hunger = Mathf.Clamp(Hunger + amount, 0f, maxHunger);
    }

    // Drinks fill the thirst bar back up
    public void Drink(float amount)
    {
        Thirst = Mathf.Clamp(Thirst + amount, 0f, maxThirst);
    }

    // Both bars slowly go down. Time.deltaTime keeps it the same speed on any computer.
    void DrainNeeds()
    {
        Hunger = Mathf.Max(0f, Hunger - hungerLossPerSecond * Time.deltaTime);
        Thirst = Mathf.Max(0f, Thirst - thirstLossPerSecond * Time.deltaTime);
    }

    // Slows the player down only at the moment a bar becomes empty or gets refilled
    void ApplyEmptyPenalty()
    {
        if (movement == null)
            return;

        bool isEmpty = Hunger <= 0f || Thirst <= 0f;
        if (isEmpty == wasEmpty)
            return;

        wasEmpty = isEmpty;
        movement.moveSpeed = isEmpty ? normalSpeed * emptySpeedMultiplier : normalSpeed;

        if (isEmpty)
            GameHUD.Say("You are hungry or thirsty! Buy food and drinks to roll at full speed.", 4f);
    }
}
