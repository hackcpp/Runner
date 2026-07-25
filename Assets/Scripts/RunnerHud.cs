using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum RunnerHudMode
{
    Start,
    Playing,
    Paused,
    GameOver
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
        float hintAlpha)
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
}

public sealed class RunnerHud : MonoBehaviour
{
    private static readonly Color PrimaryText = new Color(0.96f, 0.98f, 1f);
    private static readonly Color SecondaryText = new Color(0.72f, 0.8f, 0.84f);
    private static readonly Color Accent = new Color(0.05f, 0.72f, 0.8f);
    private static readonly Color Reward = new Color(1f, 0.84f, 0.2f);

    private Action startAction;
    private Action resumeAction;
    private Action restartAction;
    private RunnerHudMode currentMode;
    private Font font;
    private GameObject overlayRoot;
    private Text scoreText;
    private Text distanceText;
    private Text bestText;
    private Text comboText;
    private Text feedbackText;
    private Text hintText;
    private Text panelTitle;
    private Text panelScore;
    private Text panelDetails;
    private Text primaryButtonLabel;
    private Image comboFill;

    public static RunnerHud AttachTo(
        GameObject host,
        Action onStart,
        Action onResume,
        Action onRestart)
    {
        RunnerHud hud = host.GetComponent<RunnerHud>();
        if (hud == null)
        {
            hud = host.AddComponent<RunnerHud>();
        }

        hud.Initialize(onStart, onResume, onRestart);
        return hud;
    }

    public void Render(RunnerHudViewModel model)
    {
        currentMode = model.Mode;
        bool playing = model.Mode == RunnerHudMode.Playing;
        scoreText.gameObject.SetActive(playing);
        distanceText.gameObject.SetActive(playing);
        bestText.gameObject.SetActive(playing);
        if (playing)
        {
            scoreText.text = "SCORE  " + model.Score;
            distanceText.text = "DISTANCE  " + model.Distance + " m";
            bestText.text = "BEST  " + model.BestScore;
        }

        bool showCombo = playing && model.ComboAmount > 0f;
        comboText.gameObject.SetActive(showCombo);
        comboFill.transform.parent.gameObject.SetActive(showCombo);
        if (showCombo)
        {
            comboText.text = "COMBO  x" + model.ComboMultiplier;
            RectTransform fillTransform = comboFill.rectTransform;
            fillTransform.anchorMax = new Vector2(Mathf.Clamp01(model.ComboAmount), 1f);
        }

        bool showFeedback = playing && model.FeedbackAlpha > 0f;
        feedbackText.gameObject.SetActive(showFeedback);
        if (showFeedback)
        {
            feedbackText.text = "+" + model.FeedbackPoints +
                                (model.ComboMultiplier > 1 ? "   x" + model.ComboMultiplier : string.Empty);
            feedbackText.color = WithAlpha(Reward, model.FeedbackAlpha);
        }

        bool showHint = playing && !string.IsNullOrEmpty(model.HintSymbol) && model.HintAlpha > 0f;
        hintText.gameObject.SetActive(showHint);
        if (showHint)
        {
            hintText.text = model.HintSymbol;
            hintText.color = WithAlpha(Reward, model.HintAlpha);
        }

        overlayRoot.SetActive(!playing);
        if (!playing)
        {
            RenderOverlay(model);
        }
    }

    private void Initialize(Action onStart, Action onResume, Action onRestart)
    {
        startAction = onStart;
        resumeAction = onResume;
        restartAction = onRestart;
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
        scoreText = CreateText(
            canvasTransform,
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
            canvasTransform,
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
            canvasTransform,
            "Best",
            new Vector2(0f, 1f),
            new Vector2(0f, 1f),
            new Vector2(26f, -94f),
            new Vector2(420f, 32f),
            19,
            TextAnchor.MiddleLeft,
            FontStyle.Normal,
            SecondaryText);

        comboText = CreateText(
            canvasTransform,
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
            canvasTransform,
            "Combo Track",
            new Vector2(0.5f, 1f),
            new Vector2(0.5f, 1f),
            new Vector2(0f, -58f),
            new Vector2(180f, 6f),
            new Color(0.02f, 0.03f, 0.04f, 0.7f));
        comboFill = CreateStretchImage(comboTrack.rectTransform, "Combo Fill", Reward);

        feedbackText = CreateText(
            canvasTransform,
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
            canvasTransform,
            "Action Hint",
            new Vector2(0.5f, 0.38f),
            new Vector2(0.5f, 0.5f),
            Vector2.zero,
            new Vector2(320f, 72f),
            48,
            TextAnchor.MiddleCenter,
            FontStyle.Bold,
            Reward);

        BuildOverlay(canvasTransform);
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

        panelTitle = CreateText(
            overlayTransform,
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
            overlayTransform,
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
            overlayTransform,
            "Panel Details",
            new Vector2(0.5f, 0.5f),
            new Vector2(0.5f, 0.5f),
            new Vector2(0f, -18f),
            new Vector2(760f, 78f),
            20,
            TextAnchor.MiddleCenter,
            FontStyle.Normal,
            SecondaryText);

        Button primaryButton = CreateButton(
            overlayTransform,
            "Primary Action",
            new Vector2(0f, -116f),
            new Vector2(286f, 54f));
        primaryButton.onClick.AddListener(HandlePrimaryButton);
        primaryButtonLabel = primaryButton.GetComponentInChildren<Text>();
    }

    private void RenderOverlay(RunnerHudViewModel model)
    {
        if (model.Mode == RunnerHudMode.Start)
        {
            panelTitle.text = "ROOFTOP RUNNER";
            panelScore.text = "BEST  " + model.BestScore;
            panelDetails.text = string.Empty;
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

        panelTitle.text = "RUN ENDED";
        panelScore.text = "SCORE  " + model.Score;
        panelDetails.text = "DISTANCE  " + model.Distance + " m    ACTIONS  " + model.ActionCount +
                            "\nBEST COMBO  x" + model.RunBestCombo;
        primaryButtonLabel.text = "RESTART";
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
        else if (currentMode == RunnerHudMode.GameOver)
        {
            restartAction?.Invoke();
        }
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
        Vector2 size)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);
        RectTransform rect = buttonObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;

        Image image = buttonObject.GetComponent<Image>();
        image.color = Accent;
        Button button = buttonObject.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Accent;
        colors.highlightedColor = new Color(0.08f, 0.82f, 0.88f);
        colors.pressedColor = new Color(0.03f, 0.58f, 0.66f);
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
