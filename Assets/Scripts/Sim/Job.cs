/*********************************************************************************************
 * COMPONENT OF: (base class) DeliveryJob, TrashJob, RideJob, ClerkJob and PerformerJob
 * REQUIRED DEPENDENCIES: PlayerInteractor on the Player, MoneyManager, GameHUD
 * DESCRIPTION: Everything every job shares: only one job can run at a time, the prompt
 *              shown at the job spot, starting the job with E, and getting paid at the end.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.0
 *********************************************************************************************/
using UnityEngine;

public abstract class Job : Interactable
{
    // The job the player is doing right now (null = no job)
    public static Job Active { get; private set; }

    [Header("Job")]
    public string jobName = "Job";
    // Shown in the prompt before the job starts, for example "deliver packages"
    public string jobDescription = "earn money";
    public AudioClip completeSound;

    public bool IsActive => Active == this;

    // The player doing this job
    protected PlayerInteractor Worker { get; private set; }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (Active == this)
            Active = null;
    }

    public override string GetPrompt(PlayerInteractor player)
    {
        if (IsActive)
            return ActivePrompt();

        if (Active != null)
            return "Finish your " + Active.jobName + " job first";

        return "Press E to start the " + jobName + " job (" + jobDescription + ")";
    }

    public override void Interact(PlayerInteractor player)
    {
        if (IsActive)
        {
            InteractWhileWorking(player);
            return;
        }

        if (Active != null)
        {
            GameHUD.Say("Finish your " + Active.jobName + " job first!");
            return;
        }

        Worker = player;
        Active = this;
        StartWork();
    }

    // What the job does when it starts
    protected abstract void StartWork();

    // The prompt while the job is running
    protected virtual string ActivePrompt()
    {
        return jobName + " job in progress";
    }

    // Pressing E at the job spot again while working (some jobs use this)
    protected virtual void InteractWhileWorking(PlayerInteractor player)
    {
        GameHUD.Say("You are already doing this job.");
    }

    // Pays the player and frees them for another job
    protected void FinishJob(int pay, string message)
    {
        if (Worker != null && Worker.Money != null && pay > 0)
            Worker.Money.AddMoney(pay);

        if (Worker != null)
            Worker.PlaySound(completeSound);

        GameHUD.Say(message + (pay > 0 ? " You earned $" + pay + "." : ""), 4f);
        GameHUD.ClearObjective();
        Active = null;
        Worker = null;
    }
}
