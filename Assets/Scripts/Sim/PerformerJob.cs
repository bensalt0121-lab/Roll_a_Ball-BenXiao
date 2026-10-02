/*********************************************************************************************
 * COMPONENT OF: street performance stage in the Food Court plaza (made by the setup tool)
 * REQUIRED DEPENDENCIES: Job (base class), PlayerMovement on the Player (for IsGrounded),
 *                        NpcWalker people nearby
 * DESCRIPTION: Street performer job. Start a 30 second show, then jump around on the stage:
 *              every landing is a trick. People nearby come to watch. You get paid for
 *              tricks and for every person watching.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.0
 *********************************************************************************************/
using UnityEngine;

public class PerformerJob : Job
{
    [Header("Show")]
    public float showLength = 30f;
    // How close to the middle of the stage tricks count
    public float stageRadius = 3.5f;
    // People within this distance come to watch
    public float watchRadius = 15f;
    public AudioClip trickSound;

    [Header("Pay")]
    public int payPerTrick = 1;
    public int maxTrickPay = 20;
    public int payPerWatcher = 2;

    private float endTime;
    private int tricks;
    private bool wasGrounded = true;
    private PlayerMovement movement;

    void Update()
    {
        CountTricks();
        UpdateShow();
    }

    protected override string ActivePrompt()
    {
        return "Jump on the stage to do tricks! (" + Mathf.CeilToInt(endTime - Time.time) + "s left)";
    }

    protected override void StartWork()
    {
        tricks = 0;
        endTime = Time.time + showLength;
        movement = Worker.GetComponent<PlayerMovement>();
        wasGrounded = true;
        CallWatchers();
        GameHUD.Say("Showtime! Jump around on the stage to do tricks!");
    }

    // A landing on the stage counts as one trick
    void CountTricks()
    {
        if (!IsActive || movement == null)
            return;

        Vector3 offset = Worker.transform.position - transform.position;
        offset.y = 0f;
        bool onStage = offset.magnitude <= stageRadius;
        bool grounded = movement.IsGrounded;

        if (onStage && grounded && !wasGrounded)
        {
            tricks++;
            Worker.PlaySound(trickSound);
        }

        wasGrounded = grounded;
    }

    void UpdateShow()
    {
        if (!IsActive)
            return;

        int secondsLeft = Mathf.CeilToInt(endTime - Time.time);
        GameHUD.Objective("Street show: " + tricks + " tricks - " + secondsLeft + "s left");

        if (Time.time >= endTime)
            EndShow();
    }

    void EndShow()
    {
        int watchers = 0;
        foreach (NpcWalker person in FindObjectsByType<NpcWalker>())
        {
            if (person.IsWatching(transform))
                watchers++;
        }

        int pay = Mathf.Min(tricks * payPerTrick, maxTrickPay) + watchers * payPerWatcher;
        FinishJob(pay, "Show's over! " + tricks + " tricks, " + watchers + " people watched.");
    }

    // Tells nearby people to come and watch
    void CallWatchers()
    {
        foreach (NpcWalker person in FindObjectsByType<NpcWalker>())
        {
            if (Vector3.Distance(person.transform.position, transform.position) <= watchRadius)
                person.WatchShow(transform, showLength);
        }
    }
}
