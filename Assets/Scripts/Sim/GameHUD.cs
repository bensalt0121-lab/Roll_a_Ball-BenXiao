/*********************************************************************************************
 * COMPONENT OF: Sim HUD (the on-screen canvas made by Tools > I Am A Ball > Build Everything)
 * REQUIRED DEPENDENCIES: PlayerNeeds on the Player, WantedLevel in the scene, TextMeshPro
 * DESCRIPTION: Shows the hunger and thirst bars, the "Press E" prompt, pop-up messages, the
 *              current objective and the wanted stars. Other scripts talk to it with
 *              GameHUD.Say(...) and GameHUD.Objective(...).
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.0
 *********************************************************************************************/
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameHUD : MonoBehaviour
{
    // Lets any script reach the HUD with GameHUD.Instance
    public static GameHUD Instance { get; private set; }

    [Header("Needs Bars")]
    // The colored part of each bar. It gets shorter as the need goes down.
    public RectTransform hungerFill;
    public RectTransform thirstFill;
    public Image hungerFillImage;
    public Image thirstFillImage;
    // Bar color when the need is fine, and when it is almost empty
    public Color okColor = new Color(0.30f, 0.80f, 0.35f);
    public Color lowColor = new Color(0.90f, 0.25f, 0.20f);
    // Below this amount (0 to 1) the bar turns red
    public float lowThreshold = 0.25f;

    [Header("Text")]
    public TMP_Text promptText;
    // The dark box behind the prompt (shown and hidden with it)
    public GameObject promptPanel;
    public TMP_Text messageText;
    public TMP_Text objectiveText;
    // Shown as the objective whenever no job is running
    public string defaultObjective = "Goal: earn money and buy your dream home";

    [Header("Crosshair")]
    // Shown in the middle of the screen while the player has a gun
    public GameObject crosshair;

    [Header("Wanted Level")]
    // One image per star, from left to right
    public Image[] wantedStars;
    public Color starOnColor = new Color(1f, 0.85f, 0.2f);
    public Color starOffColor = new Color(1f, 1f, 1f, 0.15f);

    private PlayerNeeds needs;
    private float messageHideTime;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        needs = FindAnyObjectByType<PlayerNeeds>();
        HidePrompt();
        ShowMessage("Welcome to the city! Find a job, stay fed, and buy a better home.", 6f);
        SetObjective(defaultObjective);
    }

    void Update()
    {
        UpdateNeedsBars();
        UpdateMessageTimer();
        UpdateWantedStars();
    }

    // ---------- Static helpers so other scripts do not need to check for null ----------

    public static void Say(string text, float seconds = 3f)
    {
        if (Instance != null)
            Instance.ShowMessage(text, seconds);
    }

    public static void Objective(string text)
    {
        if (Instance != null)
            Instance.SetObjective(text);
    }

    public static void ShowCrosshair(bool show)
    {
        if (Instance != null && Instance.crosshair != null)
            Instance.crosshair.SetActive(show);
    }

    public static void ClearObjective()
    {
        if (Instance != null)
            Instance.SetObjective(Instance.defaultObjective);
    }

    // ---------- Things other scripts can call ----------

    public void ShowPrompt(string text)
    {
        if (promptText == null)
            return;

        promptText.text = text;
        PromptBox().SetActive(true);
    }

    public void HidePrompt()
    {
        if (promptText != null)
            PromptBox().SetActive(false);
    }

    // The object to show/hide for the prompt: the box if there is one, otherwise the text
    GameObject PromptBox()
    {
        return promptPanel != null ? promptPanel : promptText.gameObject;
    }

    public void ShowMessage(string text, float seconds)
    {
        if (messageText == null)
            return;

        messageText.text = text;
        messageHideTime = Time.time + seconds;
    }

    public void SetObjective(string text)
    {
        if (objectiveText != null)
            objectiveText.text = text;
    }

    // ---------- Every-frame updates ----------

    // Makes each bar as long as the need is full, and red when it is low
    void UpdateNeedsBars()
    {
        if (needs == null)
            return;

        SetBar(hungerFill, hungerFillImage, needs.HungerPercent);
        SetBar(thirstFill, thirstFillImage, needs.ThirstPercent);
    }

    void SetBar(RectTransform fill, Image image, float percent)
    {
        if (fill == null)
            return;

        // anchorMax.x is how far across the bar the fill reaches (0 = empty, 1 = full)
        fill.anchorMax = new Vector2(percent, fill.anchorMax.y);

        if (image != null)
            image.color = percent < lowThreshold ? lowColor : okColor;
    }

    // Clears the pop-up message after its time runs out
    void UpdateMessageTimer()
    {
        if (messageText != null && messageText.text != "" && Time.time > messageHideTime)
            messageText.text = "";
    }

    // Lights up one star for each wanted level
    void UpdateWantedStars()
    {
        if (wantedStars == null)
            return;

        int stars = WantedLevel.Instance != null ? WantedLevel.Instance.Stars : 0;

        for (int i = 0; i < wantedStars.Length; i++)
        {
            if (wantedStars[i] != null)
                wantedStars[i].color = i < stars ? starOnColor : starOffColor;
        }
    }
}
