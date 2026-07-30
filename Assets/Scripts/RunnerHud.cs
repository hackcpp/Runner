using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum RunnerHudMode
{
    Start,
    Playing,
    Celebrating,
    Recovering,
    Paused,
    GameOver,
    LevelComplete,
    CampaignComplete
}

public readonly struct RunnerHudViewModel
{
    public RunnerHudViewModel(
        RunnerHudMode mode,
        int score,
        int distance,
        int bestScore,
        int actionCount,
        int runBestCombo,
        int comboMultiplier,
        float comboAmount,
        int feedbackPoints,
        float feedbackAlpha,
        string hintSymbol,
        float hintAlpha,
        bool touchControls,
        bool allowLevelSelection = false)
        : this(
            mode,
            score,
            distance,
            bestScore,
            actionCount,
            runBestCombo,
            comboMultiplier,
            comboAmount,
            feedbackPoints,
            feedbackAlpha,
            hintSymbol,
            hintAlpha,
            touchControls,
            1,
            3,
            0,
            RunnerLevelCatalog.LivesPerLevel,
            RunnerLevelCatalog.LivesPerLevel,
            0,
            allowLevelSelection)
    {
    }

    public RunnerHudViewModel(
        RunnerHudMode mode,
        int score,
        int distance,
        int bestScore,
        int actionCount,
        int runBestCombo,
        int comboMultiplier,
        float comboAmount,
        int feedbackPoints,
        float feedbackAlpha,
        string hintSymbol,
        float hintAlpha,
        bool touchControls,
        int levelNumber,
        int levelCount,
        int targetDistance,
        int lives,
        int maximumLives,
        int checkpointNumber,
        bool allowLevelSelection = false)
    {
        Mode = mode;
        Score = score;
        Distance = distance;
        BestScore = bestScore;
        ActionCount = actionCount;
        RunBestCombo = runBestCombo;
        ComboMultiplier = comboMultiplier;
        ComboAmount = comboAmount;
        FeedbackPoints = feedbackPoints;
        FeedbackAlpha = feedbackAlpha;
        HintSymbol = hintSymbol;
        HintAlpha = hintAlpha;
        TouchControls = touchControls;
        LevelNumber = levelNumber;
        LevelCount = levelCount;
        TargetDistance = targetDistance;
        Lives = lives;
        MaximumLives = maximumLives;
        CheckpointNumber = checkpointNumber;
        AllowLevelSelection = allowLevelSelection;
    }

    public RunnerHudMode Mode { get; }
    public int Score { get; }
    public int Distance { get; }
    public int BestScore { get; }
    public int ActionCount { get; }
    public int RunBestCombo { get; }
    public int ComboMultiplier { get; }
    public float ComboAmount { get; }
    public int FeedbackPoints { get; }
    public float FeedbackAlpha { get; }
    public string HintSymbol { get; }
    public float HintAlpha { get; }
    public bool TouchControls { get; }
    public int LevelNumber { get; }
    public int LevelCount { get; }
    public int TargetDistance { get; }
    public int Lives { get; }
    public int MaximumLives { get; }
    public int CheckpointNumber { get; }
    public bool AllowLevelSelection { get; }
}

public sealed class RunnerHud : MonoBehaviour
{
    private const float GestureDirectionDuration = 1.1f;

    private static readonly Color PrimaryText = new Color(0.96f, 0.98f, 1f);
    private static readonly Color SecondaryText = new Color(0.72f, 0.8f, 0.84f);
    private static readonly Color Accent = new Color(0.05f, 0.72f, 0.8f);
    private static readonly Color Reward = new Color(1f, 0.84f, 0.2f);
    private static readonly Vector2[] GestureDirections =
    {
        Vector2.left,
        Vector2.right,
        Vector2.up,
        Vector2.down
    };
    private static readonly string[] GestureArrows =
    {
        "\u2190",
        "\u2192",
        "\u2191",
        "\u2193"
    };

    private Action startAction;
    private Action pauseAction;
    private Action resumeAction;
    private Action restartAction;
    private Action exitAction;
    private Action<int> selectLevelAction;
    private RunnerHudMode currentMode;
    private Font font;
    private GameObject overlayRoot;
    private Text scoreText;
    private Text distanceText;
    private Text bestText;
    private Text levelText;
    private Text livesText;
    private Text comboText;
    private Text feedbackText;
    private Text hintText;
    private Text panelTitle;
    private Text panelScore;
    private Text panelDetails;
    private Text primaryButtonLabel;
    private Image comboFill;
    private Button pauseButton;
    private Button primaryButton;
    private Button exitButton;
    private RectTransform levelSelectRoot;
    private readonly Button[] levelSelectButtons = new Button[3];
    private RectTransform gestureGuideRoot;
    private RectTransform gestureFinger;
    private Image gestureTrail;
    private Text gestureArrow;
    private CanvasGroup gestureFingerCanvasGroup;
    private RectTransform gameplaySafeArea;
    private RectTransform overlaySafeArea;
    private Rect appliedSafeArea;
    private Vector2 appliedScreenSize;
    private float gestureGuideTime;
    private bool hasAppliedSafeArea;
    private bool gestureGuideVisible;

    public Button PauseButton => pauseButton;
    public Button PrimaryButton => primaryButton;
    public Button ExitButton => exitButton;
    public Button[] LevelSelectButtons => levelSelectButtons;
    public RectTransform GestureGuideRoot => gestureGuideRoot;
    public RectTransform GestureFinger => gestureFinger;
    public Image GestureTrail => gestureTrail;
    public Text GestureArrow => gestureArrow;
    public int GestureGuideDirectionIndex { get; private set; }
    public RectTransform GameplaySafeArea => gameplaySafeArea;
    public RectTransform OverlaySafeArea => overlaySafeArea;

    public static RunnerHud AttachTo(
        GameObject host,
        Action onStart,
        Action onPause,
        Action onResume,
        Action onRestart,
        Action onExit,
        Action<int> onSelectLevel)
    {
        RunnerHud hud = host.GetComponent<RunnerHud>();
        if (hud == null)
        {
            hud = host.AddComponent<RunnerHud>();
        }

        hud.Initialize(onStart, onPause, onResume, onRestart, onExit, onSelectLevel);
        return hud;
    }

    public void Render(RunnerHudViewModel model)
    {
        currentMode = model.Mode;
        bool playing = model.Mode == RunnerHudMode.Playing;
        bool gameplayVisible = playing || model.Mode == RunnerHudMode.Celebrating;
        scoreText.gameObject.SetActive(gameplayVisible);
        distanceText.gameObject.SetActive(gameplayVisible);
        bestText.gameObject.SetActive(gameplayVisible);
        pauseButton.gameObject.SetActive(playing);
        if (gameplayVisible)
        {
            scoreText.text = "SCORE  " + model.Score;
            distanceText.text = "PROGRESS  " + model.Distance + " / " + model.TargetDistance + " m";
            bestText.text = "BEST  " + model.BestScore;
            levelText.text = "LEVEL  " + model.LevelNumber + " / " + model.LevelCount;
            livesText.text = "LIVES  " + model.Lives + " / " + model.MaximumLives;
        }

        levelText.gameObject.SetActive(gameplayVisible);
        livesText.gameObject.SetActive(gameplayVisible);

        bool showCombo = playing && model.ComboAmount > 0f;
        comboText.gameObject.SetActive(showCombo);
        comboFill.transform.parent.gameObject.SetActive(showCombo);
        if (showCombo)
        {
            comboText.text = "COMBO  x" + model.ComboMultiplier;
            RectTransform fillTransform = comboFill.rectTransform;
            fillTransform.anchorMax = new Vector2(Mathf.Clamp01(model.ComboAmount), 1f);
        }

        bool celebrating = model.Mode == RunnerHudMode.Celebrating;
        bool showFeedback = celebrating || playing && model.FeedbackAlpha > 0f;
        feedbackText.gameObject.SetActive(showFeedback);
        if (showFeedback)
        {
            feedbackText.text = celebrating
                ? "LEVEL CLEAR"
                : "+" + model.FeedbackPoints +
                  (model.ComboMultiplier > 1 ? "   x" + model.ComboMultiplier : string.Empty);
            feedbackText.color = WithAlpha(Reward, celebrating ? 1f : model.FeedbackAlpha);
        }

        bool showHint = playing && !string.IsNullOrEmpty(model.HintSymbol) && model.HintAlpha > 0f;
        hintText.gameObject.SetActive(showHint);
        if (showHint)
        {
            hintText.text = model.HintSymbol;
            hintText.color = WithAlpha(Reward, model.HintAlpha);
        }

        overlayRoot.SetActive(!gameplayVisible);
        if (!gameplayVisible)
        {
            RenderOverlay(model);
        }

        bool showLevelSelection = model.Mode == RunnerHudMode.Start && model.AllowLevelSelection;
        levelSelectRoot.gameObject.SetActive(showLevelSelection);
        exitButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, showLevelSelection ? -276f : -194f);

        SetGestureGuideVisible(model.Mode == RunnerHudMode.Start && model.TouchControls);
    }

    public void ApplySafeAreaForTests(Rect safeArea, Vector2 screenSize)
    {
        ApplySafeArea(safeArea, screenSize);
    }

    public void SetGestureGuideTimeForTests(float elapsed)
    {
        gestureGuideTime = Mathf.Repeat(elapsed, GestureDirectionDuration * GestureDirections.Length);
        RenderGestureGuideAnimation();
    }

    private void Update()
    {
        ApplySafeArea(Screen.safeArea, new Vector2(Screen.width, Screen.height));
        if (gestureGuideRoot != null && gestureGuideRoot.gameObject.activeInHierarchy)
        {
            gestureGuideTime = Mathf.Repeat(
                gestureGuideTime + Time.unscaledDeltaTime,
                GestureDirectionDuration * GestureDirections.Length);
            RenderGestureGuideAnimation();
        }
    }

    private void Initialize(
        Action onStart,
        Action onPause,
        Action onResume,
        Action onRestart,
        Action onExit,
        Action<int> onSelectLevel)
    {
        startAction = onStart;
        pauseAction = onPause;
        resumeAction = onResume;
        restartAction = onRestart;
        exitAction = onExit;
        selectLevelAction = onSelectLevel;
        if (scoreText != null)
        {
            return;
        }

        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildCanvas();
        EnsureEventSystem();
    }

    private void BuildCanvas()
    {
        GameObject canvasObject = new GameObject(
            "Runner HUD Canvas",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1440f, 900f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasTransform = canvasObject.GetComponent<RectTransform>();
        gameplaySafeArea = CreateSafeAreaRoot(canvasTransform, "Runner Gameplay Safe Area");
        scoreText = CreateText(
            gameplaySafeArea,
            "Score",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(26f, -20f),
            new Vector2(420f, 42f),
            28,
            TextAnchor.MiddleLeft,
            FontStyle.Bold,
            PrimaryText);
        distanceText = CreateText(
            gameplaySafeArea,
            "Distance",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(26f, -62f),
            new Vector2(420f, 32f),
            19,
            TextAnchor.MiddleLeft,
            FontStyle.Normal,
            SecondaryText);
        bestText = CreateText(
            gameplaySafeArea,
            "Best",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(26f, -94f),
            new Vector2(420f, 32f),
            19,
            TextAnchor.MiddleLeft,
            FontStyle.Normal,
            SecondaryText);
        levelText = CreateText(
            gameplaySafeArea,
            "Level",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-98f, -22f),
            new Vector2(250f, 32f),
            19,
            TextAnchor.MiddleRight,
            FontStyle.Bold,
            PrimaryText);
        livesText = CreateText(
            gameplaySafeArea,
            "Lives",
            new Vector2(1f, 1f),
            new Vector2(1f, 1f),
            new Vector2(-98f, -54f),
            new Vector2(250f, 32f),
            19,
            TextAnchor.MiddleRight,
            FontStyle.Bold,
            Reward);

        comboText = CreateText(
            gameplaySafeArea,
            "Combo",
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -20f),
            new Vector2(220f, 34f),
            21,
            TextAnchor.MiddleCenter,
            FontStyle.Bold,
            Reward);
        Image comboTrack = CreateImage(
            gameplaySafeArea,
            "Combo Track",
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -58f),
            new Vector2(180f, 6f),
            new Color(0.02f, 0.03f, 0.04f, 0.7f));
        comboFill = CreateStretchImage(comboTrack.rectTransform, "Combo Fill", Reward);

        feedbackText = CreateText(
            gameplaySafeArea,
            "Action Reward",
            new Vector2(0.5f, 0.72f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(300f, 58f),
            34,
            TextAnchor.MiddleCenter,
            FontStyle.Bold,
            Reward);
        hintText = CreateText(
            gameplaySafeArea,
            "Action Hint",
            new Vector2(0.5f, 0.38f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(320f, 72f),
            48,
            TextAnchor.MiddleCenter,
            FontStyle.Bold,
            Reward);

        pauseButton = CreateButton(
            gameplaySafeArea,
            "Pause Run",
            new Vector2(-20f, -18f),
            new Vector2(64f, 64f));
        RectTransform pauseTransform = pauseButton.GetComponent<RectTransform>();
        pauseTransform.anchorMin = Vector2.one;
        pauseTransform.anchorMax = Vector2.one;
        pauseTransform.pivot = Vector2.one;
        pauseButton.GetComponentInChildren<Text>().text = "II";
        pauseButton.onClick.AddListener(HandlePauseButton);

        BuildOverlay(canvasTransform);
        ApplySafeArea(Screen.safeArea, new Vector2(Screen.width, Screen.height));
    }

    private void BuildOverlay(RectTransform canvasTransform)
    {
        Image overlay = CreateStretchImage(
            canvasTransform,
            "Runner HUD Overlay",
            new Color(0.015f, 0.025f, 0.03f, 0.76f));
        overlay.raycastTarget = false;
        overlayRoot = overlay.gameObject;
        RectTransform overlayTransform = overlay.rectTransform;
        overlaySafeArea = CreateSafeAreaRoot(overlayTransform, "Runner Overlay Safe Area");

        panelTitle = CreateText(
            overlaySafeArea,
            "Panel Title",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 112f),
            new Vector2(760f, 78f),
            48,
            TextAnchor.MiddleCenter,
            FontStyle.Bold,
            PrimaryText);
        panelScore = CreateText(
            overlaySafeArea,
            "Panel Score",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, 38f),
            new Vector2(720f, 54f),
            32,
            TextAnchor.MiddleCenter,
            FontStyle.Bold,
            Reward);
        panelDetails = CreateText(
            overlaySafeArea,
            "Panel Details",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -18f),
            new Vector2(760f, 78f),
            20,
            TextAnchor.MiddleCenter,
            FontStyle.Normal,
            SecondaryText);

        BuildGestureGuide();

        primaryButton = CreateButton(
            overlaySafeArea,
            "Primary Action",
            new Vector2(0f, -116f),
            new Vector2(300f, 64f));
        primaryButton.onClick.AddListener(HandlePrimaryButton);
        primaryButtonLabel = primaryButton.GetComponentInChildren<Text>();

        GameObject levelSelectObject = new GameObject("Debug Level Selection", typeof(RectTransform));
        levelSelectObject.transform.SetParent(overlaySafeArea, false);
        levelSelectRoot = levelSelectObject.GetComponent<RectTransform>();
        levelSelectRoot.anchorMin = new Vector2(0.5f, 0.5f);
        levelSelectRoot.anchorMax = new Vector2(0.5f, 0.5f);
        levelSelectRoot.pivot = new Vector2(0.5f, 0.5f);
        levelSelectRoot.anchoredPosition = new Vector2(0f, -194f);
        levelSelectRoot.sizeDelta = new Vector2(540f, 52f);

        for (int index = 0; index < levelSelectButtons.Length; index++)
        {
            int levelNumber = index + 1;
            Button levelButton = CreateButton(
                levelSelectRoot,
                "Start Level " + levelNumber,
                new Vector2((index - 1) * 180f, 0f),
                new Vector2(164f, 52f),
                true);
            levelButton.GetComponentInChildren<Text>().text = "LEVEL " + levelNumber;
            levelButton.onClick.AddListener(() => HandleLevelSelectButton(levelNumber));
            levelSelectButtons[index] = levelButton;
        }

        exitButton = CreateButton(
            overlaySafeArea,
            "Exit Game",
            new Vector2(0f, -194f),
            new Vector2(300f, 56f),
            true);
        exitButton.GetComponentInChildren<Text>().text = "EXIT";
        exitButton.onClick.AddListener(HandleExitButton);
    }

    private void RenderOverlay(RunnerHudViewModel model)
    {
        if (model.Mode == RunnerHudMode.Start)
        {
            panelTitle.text = "ROOFTOP RUNNER";
            panelScore.text = "BEST  " + model.BestScore;
            panelDetails.text = "3 LEVELS  -  3 LIVES EACH";
            primaryButtonLabel.text = "START RUN";
            return;
        }

        if (model.Mode == RunnerHudMode.Paused)
        {
            panelTitle.text = "PAUSED";
            panelScore.text = "SCORE  " + model.Score;
            panelDetails.text = "DISTANCE  " + model.Distance + " m";
            primaryButtonLabel.text = "RESUME";
            return;
        }

        if (model.Mode == RunnerHudMode.Recovering)
        {
            panelTitle.text = "LIFE LOST";
            panelScore.text = "LIVES  " + model.Lives + " / " + model.MaximumLives;
            panelDetails.text = "CHECKPOINT  " + model.CheckpointNumber + " / " + RunnerLevelCatalog.CheckpointCount +
                                "    DISTANCE  " + model.Distance + " m";
            primaryButtonLabel.text = "CONTINUE";
            return;
        }

        if (model.Mode == RunnerHudMode.LevelComplete)
        {
            panelTitle.text = "LEVEL " + model.LevelNumber + " CLEAR";
            panelScore.text = "SCORE  " + model.Score;
            panelDetails.text = "CHECKPOINTS SECURED  " + model.CheckpointNumber + " / " + RunnerLevelCatalog.CheckpointCount;
            primaryButtonLabel.text = "NEXT LEVEL";
            return;
        }

        if (model.Mode == RunnerHudMode.CampaignComplete)
        {
            panelTitle.text = "ROOFTOP COMPLETE";
            panelScore.text = "FINAL SCORE  " + model.Score;
            panelDetails.text = "ALL " + model.LevelCount + " LEVELS CLEARED";
            primaryButtonLabel.text = "PLAY AGAIN";
            return;
        }

        panelTitle.text = "LEVEL " + model.LevelNumber + " FAILED";
        panelScore.text = "SCORE  " + model.Score;
        panelDetails.text = "CHECKPOINT  " + model.CheckpointNumber + " / " + RunnerLevelCatalog.CheckpointCount +
                            "    ACTIONS  " + model.ActionCount +
                            "\nBEST COMBO  x" + model.RunBestCombo;
        primaryButtonLabel.text = "TRY AGAIN";
    }

    private void BuildGestureGuide()
    {
        GameObject guideObject = new GameObject("Swipe Gesture Guide", typeof(RectTransform));
        guideObject.transform.SetParent(overlaySafeArea, false);
        gestureGuideRoot = guideObject.GetComponent<RectTransform>();
        gestureGuideRoot.anchorMin = new Vector2(0.5f, 0.5f);
        gestureGuideRoot.anchorMax = new Vector2(0.5f, 0.5f);
        gestureGuideRoot.pivot = new Vector2(0.5f, 0.5f);
        gestureGuideRoot.anchoredPosition = new Vector2(0f, -36f);
        gestureGuideRoot.sizeDelta = new Vector2(280f, 80f);

        gestureTrail = CreateImage(
            gestureGuideRoot,
            "Swipe Trail",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(0f, 5f),
            Reward);

        gestureArrow = CreateText(
            gestureGuideRoot,
            "Swipe Direction",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(30f, 30f),
            28,
            TextAnchor.MiddleCenter,
            FontStyle.Bold,
            Reward);

        GameObject fingerObject = new GameObject("Swipe Finger", typeof(RectTransform), typeof(CanvasGroup));
        fingerObject.transform.SetParent(gestureGuideRoot, false);
        gestureFinger = fingerObject.GetComponent<RectTransform>();
        gestureFinger.anchorMin = new Vector2(0.5f, 0.5f);
        gestureFinger.anchorMax = new Vector2(0.5f, 0.5f);
        gestureFinger.pivot = new Vector2(0.5f, 0.5f);
        gestureFinger.sizeDelta = new Vector2(36f, 42f);
        gestureFingerCanvasGroup = fingerObject.GetComponent<CanvasGroup>();
        gestureFingerCanvasGroup.interactable = false;
        gestureFingerCanvasGroup.blocksRaycasts = false;

        CreateImage(
            gestureFinger,
            "Finger Palm",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -7f),
            new Vector2(20f, 21f),
            PrimaryText);
        CreateImage(
            gestureFinger,
            "Index Finger",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(-6f, 8f),
            new Vector2(8f, 27f),
            PrimaryText);
        CreateImage(
            gestureFinger,
            "Middle Finger",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(3f, 2f),
            new Vector2(7f, 15f),
            PrimaryText);
        Image thumb = CreateImage(
            gestureFinger,
            "Thumb",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(11f, -4f),
            new Vector2(12f, 7f),
            PrimaryText);
        thumb.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -24f);
        CreateImage(
            gestureFinger,
            "Finger Cuff",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -20f),
            new Vector2(20f, 6f),
            Accent);

        gestureGuideRoot.gameObject.SetActive(false);
        RenderGestureGuideAnimation();
    }

    private void SetGestureGuideVisible(bool visible)
    {
        if (gestureGuideVisible != visible)
        {
            gestureGuideTime = 0f;
            gestureGuideVisible = visible;
            RenderGestureGuideAnimation();
        }

        gestureGuideRoot.gameObject.SetActive(visible);
    }

    private void RenderGestureGuideAnimation()
    {
        if (gestureFinger == null)
        {
            return;
        }

        float totalDuration = GestureDirectionDuration * GestureDirections.Length;
        float wrappedTime = Mathf.Repeat(gestureGuideTime, totalDuration);
        GestureGuideDirectionIndex = Mathf.FloorToInt(wrappedTime / GestureDirectionDuration);
        float phase = Mathf.Repeat(wrappedTime, GestureDirectionDuration) / GestureDirectionDuration;
        Vector2 direction = GestureDirections[GestureGuideDirectionIndex];
        bool horizontal = Mathf.Abs(direction.x) > 0f;
        float travelDistance = horizontal ? 52f : 18f;
        float arrowDistance = horizontal ? 78f : 30f;
        float movement = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.14f, 0.7f, phase));
        float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.78f, 0.98f, phase));
        float trailLength = travelDistance * movement;
        Vector2 fingerPosition = direction * trailLength;

        gestureFinger.anchoredPosition = fingerPosition;
        gestureFinger.localScale = Vector3.one * Mathf.Lerp(0.9f, 1f, movement);
        gestureFingerCanvasGroup.alpha = fade;

        gestureTrail.rectTransform.anchoredPosition = direction * (trailLength * 0.5f);
        gestureTrail.rectTransform.sizeDelta = new Vector2(trailLength, 5f);
        gestureTrail.rectTransform.localRotation = Quaternion.Euler(
            0f,
            0f,
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        gestureTrail.color = WithAlpha(Reward, fade * Mathf.Lerp(0.12f, 0.72f, movement));

        gestureArrow.text = GestureArrows[GestureGuideDirectionIndex];
        gestureArrow.rectTransform.anchoredPosition = direction * arrowDistance;
        gestureArrow.color = WithAlpha(Reward, fade * Mathf.Lerp(0.45f, 1f, movement));
    }

    private void HandlePrimaryButton()
    {
        if (currentMode == RunnerHudMode.Start)
        {
            startAction?.Invoke();
        }
        else if (currentMode == RunnerHudMode.Paused)
        {
            resumeAction?.Invoke();
        }
        else if (currentMode == RunnerHudMode.Recovering ||
                 currentMode == RunnerHudMode.GameOver ||
                 currentMode == RunnerHudMode.LevelComplete ||
                 currentMode == RunnerHudMode.CampaignComplete)
        {
            restartAction?.Invoke();
        }
    }

    private void HandlePauseButton()
    {
        if (currentMode == RunnerHudMode.Playing)
        {
            pauseAction?.Invoke();
        }
    }

    private void HandleExitButton()
    {
        if (currentMode != RunnerHudMode.Playing)
        {
            exitAction?.Invoke();
        }
    }

    private void HandleLevelSelectButton(int levelNumber)
    {
        if (currentMode == RunnerHudMode.Start && levelSelectRoot.gameObject.activeInHierarchy)
        {
            selectLevelAction?.Invoke(levelNumber);
        }
    }

    private void ApplySafeArea(Rect safeArea, Vector2 screenSize)
    {
        if (screenSize.x <= 0f || screenSize.y <= 0f || gameplaySafeArea == null || overlaySafeArea == null)
        {
            return;
        }

        if (hasAppliedSafeArea && appliedSafeArea == safeArea && appliedScreenSize == screenSize)
        {
            return;
        }

        Vector2 anchorMin = new Vector2(safeArea.xMin / screenSize.x, safeArea.yMin / screenSize.y);
        Vector2 anchorMax = new Vector2(safeArea.xMax / screenSize.x, safeArea.yMax / screenSize.y);
        anchorMin = new Vector2(Mathf.Clamp01(anchorMin.x), Mathf.Clamp01(anchorMin.y));
        anchorMax = new Vector2(Mathf.Clamp01(anchorMax.x), Mathf.Clamp01(anchorMax.y));

        ApplySafeAreaAnchors(gameplaySafeArea, anchorMin, anchorMax);
        ApplySafeAreaAnchors(overlaySafeArea, anchorMin, anchorMax);
        appliedSafeArea = safeArea;
        appliedScreenSize = screenSize;
        hasAppliedSafeArea = true;
    }

    private static void ApplySafeAreaAnchors(RectTransform target, Vector2 anchorMin, Vector2 anchorMax)
    {
        target.anchorMin = anchorMin;
        target.anchorMax = anchorMax;
        target.offsetMin = Vector2.zero;
        target.offsetMax = Vector2.zero;
    }

    private static RectTransform CreateSafeAreaRoot(RectTransform parent, string objectName)
    {
        GameObject safeAreaObject = new GameObject(objectName, typeof(RectTransform));
        safeAreaObject.transform.SetParent(parent, false);
        RectTransform rect = safeAreaObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return rect;
    }

    private Text CreateText(
        RectTransform parent,
        string objectName,
        Vector2 anchor,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 size,
        int fontSize,
        TextAnchor alignment,
        FontStyle fontStyle,
        Color color)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(Text), typeof(Outline));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = color;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        text.raycastTarget = false;

        Outline outline = textObject.GetComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.72f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);
        return text;
    }

    private static Image CreateImage(
        RectTransform parent,
        string objectName,
        Vector2 anchor,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 size,
        Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Image CreateStretchImage(RectTransform parent, string objectName, Color color)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private Button CreateButton(
        RectTransform parent,
        string objectName,
        Vector2 anchoredPosition,
        Vector2 size,
        bool secondary = false)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Color normalColor = secondary ? new Color(0.08f, 0.12f, 0.14f) : Accent;
        Color highlightedColor = secondary ? new Color(0.14f, 0.2f, 0.22f) : new Color(0.08f, 0.82f, 0.88f);
        Color pressedColor = secondary ? new Color(0.04f, 0.07f, 0.08f) : new Color(0.03f, 0.58f, 0.66f);

        Image image = buttonObject.GetComponent<Image>();
        image.color = normalColor;
        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = normalColor;
        colors.highlightedColor = highlightedColor;
        colors.pressedColor = pressedColor;
        colors.selectedColor = colors.highlightedColor;
        colors.fadeDuration = 0.08f;
        button.colors = colors;

        Text label = CreateText(
            rect,
            "Label",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            size,
            21,
            TextAnchor.MiddleCenter,
            FontStyle.Bold,
            Color.white);
        label.raycastTarget = false;
        return button;
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystem = new GameObject(
            "Runner UI Event System",
            typeof(EventSystem),
            typeof(StandaloneInputModule));
        eventSystem.transform.SetParent(transform, false);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }
}
