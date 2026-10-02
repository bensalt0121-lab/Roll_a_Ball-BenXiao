/*********************************************************************************************
 * COMPONENT OF: "Tutorial" (creates itself when the game starts - no setup needed)
 * REQUIRED DEPENDENCIES: Player with PlayerInteractor and PlayerMovement, MoneyManager,
 *                        a Burger counter (Shop) and a DeliveryJob in the scene, TextMeshPro,
 *                        Input System package
 * DESCRIPTION: A step-by-step tutorial for new players: move, jump, buy food, start and finish
 *              a job, then the goal and the police. A blue beam shows where to go. Also the
 *              help screen (H) with the controls, the goal and the map colors.
 *              Keys: Enter = next tip, T = skip tutorial, H = help, R (in help) = replay.
 * AUTHOR: Ben Xiao (written with Claude)
 * VERSION: 1.1
 * VERSION 1.1: Teaches the big map (M), brighter guide beam, M in the help screen.
 *********************************************************************************************/
using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class TutorialManager : MonoBehaviour
{
    // One step of the tutorial: what to show, where to point, and when it is done
    class Step
    {
        public string title;
        public string text;
        public Func<Vector3?> target;
        public Func<bool> isDone;
        public bool isTip;
    }

    const string DoneKey = "IAmABall.TutorialDone";
    static readonly Color Blue = new Color(0.35f, 0.75f, 1f);

    private Step[] steps;
    private int stepIndex = -1;
    private float stepStartTime;
    private Vector3 stepStartPosition;

    private PlayerInteractor player;
    private PlayerMovement movement;
    private MoneyManager money;
    private bool wasGrounded = true;
    private bool jumped;
    private bool bought;
    private bool finishedJob;

    private GameObject tutorialPanel;
    private TMP_Text titleText;
    private TMP_Text bodyText;
    private TMP_Text footerText;
    private GameObject helpPanel;
    private GameObject guideBeam;

    // Makes the tutorial by itself when the game scene starts
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateAutomatically()
    {
        if (FindAnyObjectByType<TutorialManager>() != null || FindAnyObjectByType<PlayerInteractor>() == null)
            return;

        new GameObject("Tutorial").AddComponent<TutorialManager>();
    }

    void Start()
    {
        player = FindAnyObjectByType<PlayerInteractor>();
        movement = player.GetComponent<PlayerMovement>();
        money = FindAnyObjectByType<MoneyManager>();

        Shop.Purchased += OnPurchased;
        Job.Finished += OnJobFinished;

        BuildScreen();
        BuildGuideBeam();
        MakeSteps();

        if (PlayerPrefs.GetInt(DoneKey, 0) == 1)
            EndTutorial(false);
        else
            GoToStep(0);
    }

    void OnDestroy()
    {
        Shop.Purchased -= OnPurchased;
        Job.Finished -= OnJobFinished;
    }

    void Update()
    {
        CheckKeys();
        TrackJumps();
        CheckStep();
        MoveGuideBeam();
    }

    void OnPurchased(Shop shop) => bought = true;
    void OnJobFinished(Job job, int pay) => finishedJob = true;

    // ---------- The steps ----------

    void MakeSteps()
    {
        Shop burger = FindObjectsByType<Shop>().FirstOrDefault(s => s.itemName == "Burger");
        DeliveryJob depot = FindAnyObjectByType<DeliveryJob>();
        Func<Vector3?> burgerSpot = () => burger != null ? burger.transform.position : (Vector3?)null;

        steps = new[]
        {
            new Step { title = "Move around", text = "You are a human pretending to be a ball!\nRoll with <b>W A S D</b> and look around with the <b>mouse</b>.",
                       isDone = () => SimUtil.FlatDistance(player.transform.position, stepStartPosition) > 4f },
            new Step { title = "Jump", text = "Press <b>Space</b> to jump.", isDone = () => jumped },
            new Step { title = "Open the map", text = "Press <b>M</b> to open the big map. It shows every place and tells you <b>what to do next</b>.\nPress <b>M</b> again to close it.",
                       isDone = () => MapScreen.TimesOpened > 0 && !MapScreen.IsOpen },
            new Step { title = "Find food", text = "Your <b>HUNGER</b> and <b>THIRST</b> bars (bottom left) slowly go down.\nRoll to the <color=#59BFFF>blue beam</color>: the Burger Shop.",
                       target = burgerSpot, isDone = () => burger == null || SimUtil.FlatDistance(player.transform.position, burger.transform.position) < 3f },
            new Step { title = "Buy something", text = "Stand at a counter and press <b>E</b> to buy.\nFood fills hunger, drinks fill thirst.",
                       target = burgerSpot, isDone = () => bought },
            new Step { title = "Get a job", text = "Now you need money! Roll to the <color=#59BFFF>blue beam</color>: the Delivery Depot.\nPress <b>E</b> at the job board.",
                       target = () => depot != null ? depot.transform.position : (Vector3?)null, isDone = () => Job.Active != null },
            new Step { title = "Do the job", text = "Follow the goal at the top of the screen.\nFor deliveries: roll to the <color=#7CFC7C>green beam</color> to get paid.",
                       isDone = () => finishedJob },
            new Step { title = "Your goal", isTip = true,
                       text = "There are <b>5 jobs</b> in the city (<color=#7CFC7C>green</color> on the map).\nSave <b>$300</b> for the House on Oak Street, then <b>$800</b> for the <b>Dream Villa</b> (<color=#C080FF>purple</color> on the map) to win!\nPress <b>E</b> at your home door to sleep through the night." },
            new Step { title = "Stay out of trouble", isTip = true,
                       text = "Bumping into people is fine, but <b>jumping into people</b> or <b>shooting</b> makes the <b>police</b> chase you (stars, top right).\nGet far away and the stars slowly disappear.\nIf a car hits you, you wake up at the <b>hospital</b> and pay a bill." },
            new Step { title = "You're ready!", isTip = true,
                       text = "Press <b>H</b> any time for help and the map colors.\nPress <b>F1</b> if the game is slow.\nHave fun!" },
        };
    }

    void GoToStep(int index)
    {
        stepIndex = index;
        if (stepIndex >= steps.Length)
        {
            EndTutorial(true);
            return;
        }

        Step step = steps[stepIndex];
        stepStartTime = Time.time;
        stepStartPosition = player.transform.position;
        jumped = false;
        bought = false;
        finishedJob = false;
        GiveStarterMoneyIfNeeded();

        tutorialPanel.SetActive(!helpPanel.activeSelf);
        titleText.text = "TUTORIAL " + (stepIndex + 1) + "/" + steps.Length + ": " + step.title;
        bodyText.text = step.text;
        footerText.text = step.isTip ? "Enter = next    T = skip tutorial    H = help    M = map" : "T = skip tutorial    H = help    M = map";
    }

    // Moves on when the player has done what the step asks (tips wait for Enter or a few seconds)
    void CheckStep()
    {
        if (stepIndex < 0 || stepIndex >= steps.Length)
            return;

        Step step = steps[stepIndex];
        bool done = step.isTip ? Time.time - stepStartTime > 14f : step.isDone();
        if (done)
            GoToStep(stepIndex + 1);
    }

    // Makes sure the player can afford a burger during the tutorial
    void GiveStarterMoneyIfNeeded()
    {
        if (steps[stepIndex].title == "Buy something" && money != null && money.currentMoney < 10)
            money.AddMoney(20 - money.currentMoney);
    }

    void EndTutorial(bool justFinished)
    {
        stepIndex = steps.Length;
        tutorialPanel.SetActive(false);
        guideBeam.SetActive(false);
        PlayerPrefs.SetInt(DoneKey, 1);

        if (justFinished)
            GameHUD.Say("Tutorial done! Press H any time for help.", 4f);
    }

    void TrackJumps()
    {
        if (movement == null)
            return;

        if (!movement.IsGrounded && wasGrounded)
            jumped = true;

        wasGrounded = movement.IsGrounded;
    }

    void CheckKeys()
    {
        Keyboard keys = Keyboard.current;
        if (keys == null)
            return;

        bool inTutorial = stepIndex >= 0 && stepIndex < steps.Length;

        if (keys.hKey.wasPressedThisFrame)
        {
            helpPanel.SetActive(!helpPanel.activeSelf);
            tutorialPanel.SetActive(inTutorial && !helpPanel.activeSelf);
        }

        if (helpPanel.activeSelf && keys.rKey.wasPressedThisFrame)
        {
            helpPanel.SetActive(false);
            GoToStep(0);
        }

        if (!inTutorial)
            return;

        if (keys.tKey.wasPressedThisFrame)
            EndTutorial(false);
        else if (steps[stepIndex].isTip && (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame))
            GoToStep(stepIndex + 1);
    }

    // ---------- The blue beam that shows where to go ----------

    // A very tall, bright blue beam (it also shows on the minimap)
    void BuildGuideBeam()
    {
        guideBeam = GuideBeam.Create("Tutorial Guide Beam", Blue, 80f);
        guideBeam.SetActive(false);
    }

    void MoveGuideBeam()
    {
        Vector3? target = null;
        if (stepIndex >= 0 && stepIndex < steps.Length && steps[stepIndex].target != null)
            target = steps[stepIndex].target();

        guideBeam.SetActive(target.HasValue);
        if (target.HasValue)
            guideBeam.transform.position = target.Value;
    }

    // ---------- Screen panels (made in code so no setup is needed) ----------

    void BuildScreen()
    {
        GameObject canvasObject = new GameObject("Tutorial Screen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        RectTransform screen = canvasObject.GetComponent<RectTransform>();

        // Tutorial box on the left
        RectTransform box = Box("Tutorial Box", screen, new Vector2(0f, 0.5f), new Vector2(30f, 60f), new Vector2(560f, 270f), 0.65f);
        titleText = Label("Title", box, new Vector2(20f, -16f), new Vector2(520f, 40f), 26f, new Color(1f, 0.85f, 0.3f), true);
        bodyText = Label("Text", box, new Vector2(20f, -62f), new Vector2(520f, 160f), 22f, Color.white, false);
        footerText = Label("Keys", box, new Vector2(20f, -228f), new Vector2(520f, 30f), 17f, new Color(0.75f, 0.75f, 0.75f), false);
        tutorialPanel = box.gameObject;

        // Help screen in the middle
        RectTransform help = Box("Help", screen, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1040f, 700f), 0.9f);
        help.pivot = new Vector2(0.5f, 0.5f);
        help.anchoredPosition = Vector2.zero;
        Label("Help Title", help, new Vector2(40f, -24f), new Vector2(960f, 50f), 36f, new Color(1f, 0.85f, 0.3f), true).text = "HOW TO PLAY";
        Label("Help Text", help, new Vector2(40f, -86f), new Vector2(960f, 560f), 21f, Color.white, false).text = HelpText;
        Label("Help Keys", help, new Vector2(40f, -660f), new Vector2(960f, 30f), 18f, new Color(0.75f, 0.75f, 0.75f), false).text = "H = close help    R = replay the tutorial";
        helpPanel = help.gameObject;
        helpPanel.SetActive(false);

        // Small reminder above the hunger/thirst bars
        RectTransform hint = new GameObject("Help Hint", typeof(RectTransform)).GetComponent<RectTransform>();
        hint.SetParent(screen, false);
        hint.anchorMin = hint.anchorMax = hint.pivot = Vector2.zero;
        hint.anchoredPosition = new Vector2(34f, 146f);
        hint.sizeDelta = new Vector2(400f, 30f);
        Text(hint, 18f, new Color(1f, 1f, 1f, 0.8f), true).text = "H = Help    M = Map    F1 = Graphics";
    }

    const string HelpText =
        "<color=#FFD54A><b>CONTROLS</b></color>\n" +
        "W A S D = roll    Mouse = look    Space = jump    E = use / buy / start a job    M = big map\n" +
        "Left click = shoot (after buying a pistol)    Esc = free the mouse    F1 = graphics    F3 = FPS\n\n" +
        "<color=#FFD54A><b>GOAL</b></color>\n" +
        "Earn money with jobs and keep your hunger and thirst up. Save $300 for the House on Oak Street,\n" +
        "then $800 for the Dream Villa to win. Sleep at your home door to skip the night.\n\n" +
        "<color=#FFD54A><b>JOBS</b></color>\n" +
        "Delivery (depot), Trash Pickup (by the garbage truck), Taxi (downtown), Store Clerk (corner store),\n" +
        "Street Performer (food court stage). Only one job at a time.\n\n" +
        "<color=#FFD54A><b>MAP COLORS</b></color> (bottom right)\n" +
        "<color=#FF8C1A>Orange</color> = food and drinks    <color=#33D94D>Green</color> = jobs    <color=#FFE633>Yellow</color> = your home\n" +
        "<color=#C080FF>Purple</color> = home for sale    <color=#3366FF>Blue</color> = police    <color=#FF4040>Red</color> = hospital\n\n" +
        "<color=#FFD54A><b>WATCH OUT</b></color>\n" +
        "Jumping into people or shooting = police chase you (stars, top right). Get away and the stars go away.\n" +
        "A car hit sends you to the hospital (small bill). The secret black market is down the sewer entrance.";

    static RectTransform Box(string name, Transform parent, Vector2 anchor, Vector2 position, Vector2 size, float darkness)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        Image image = rect.GetComponent<Image>();
        image.color = new Color(0f, 0f, 0f, darkness);
        image.raycastTarget = false;
        return rect;
    }

    // A text box placed from the top-left corner of its parent
    static TMP_Text Label(string name, RectTransform parent, Vector2 topLeft, Vector2 size, float fontSize, Color color, bool bold)
    {
        RectTransform rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = topLeft;
        rect.sizeDelta = size;
        return Text(rect, fontSize, color, bold);
    }

    static TMP_Text Text(RectTransform rect, float fontSize, Color color, bool bold)
    {
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.color = color;
        text.richText = true;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.Normal;
        if (bold)
            text.fontStyle = FontStyles.Bold;
        return text;
    }
}
