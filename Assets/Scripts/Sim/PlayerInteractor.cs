/*********************************************************************************************
 * COMPONENT OF: Player
 * REQUIRED DEPENDENCIES: MoneyManager in the scene, PlayerNeeds on the Player, GameHUD,
 *                        Input System package
 * DESCRIPTION: Finds the closest thing the player can use, shows "Press E ..." on screen,
 *              and uses it when the player presses E.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.1
 * VERSION 1.1: People walk around the player (NavMeshObstacle) instead of into them.
 *********************************************************************************************/
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

public class PlayerInteractor : MonoBehaviour
{
    [Header("Sound")]
    // Short click played every time the player presses E on something
    public AudioClip interactSound;

    // Other scripts use these to charge money or feed the player
    public PlayerNeeds Needs { get; private set; }
    public MoneyManager Money { get; private set; }

    private Interactable nearest;

    void Start()
    {
        Needs = GetComponent<PlayerNeeds>();
        Money = FindAnyObjectByType<MoneyManager>();
        LetPeopleWalkAround();
    }

    // Tells the NPCs to step around the player like they step around each other
    void LetPeopleWalkAround()
    {
        if (GetComponent<NavMeshObstacle>() != null)
            return;

        SphereCollider ball = GetComponent<SphereCollider>();
        float radius = ball != null ? ball.radius : 0.5f;

        NavMeshObstacle obstacle = gameObject.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Capsule;
        obstacle.radius = radius;
        obstacle.height = radius * 2f;
        obstacle.carving = false;
    }

    void Update()
    {
        FindNearestInteractable();
        UpdatePrompt();
        CheckInteractKey();
    }

    // Plays a sound where the player is (used by shops, jobs and homes)
    public void PlaySound(AudioClip clip)
    {
        if (clip != null)
            AudioSource.PlayClipAtPoint(clip, transform.position);
    }

    // Looks through every interactable and keeps the closest one that is in range
    void FindNearestInteractable()
    {
        nearest = null;
        float bestDistance = float.MaxValue;

        foreach (Interactable item in Interactable.All)
        {
            float distance = Vector3.Distance(transform.position, item.transform.position);

            if (distance <= item.interactRange && distance < bestDistance)
            {
                bestDistance = distance;
                nearest = item;
            }
        }
    }

    // Shows the prompt for the closest interactable, or hides it if nothing is close
    void UpdatePrompt()
    {
        if (GameHUD.Instance == null)
            return;

        if (nearest != null)
            GameHUD.Instance.ShowPrompt(nearest.GetPrompt(this));
        else
            GameHUD.Instance.HidePrompt();
    }

    // Uses the closest interactable when E is pressed
    void CheckInteractKey()
    {
        if (nearest == null || Keyboard.current == null)
            return;

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            PlaySound(interactSound);
            nearest.Interact(this);
        }
    }
}
