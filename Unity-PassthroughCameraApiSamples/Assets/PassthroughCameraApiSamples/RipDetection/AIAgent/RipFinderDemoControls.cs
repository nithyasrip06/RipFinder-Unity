using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Surfaces;
using PassthroughCameraSamples.MultiObjectDetection;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Builds a world-space demo button panel at runtime. The panel is
// clickable with ISDK controller rays / hand pinch on Quest, and every
// button also has an Editor hotkey (1-7) and responds to mouse clicks in the Editor.
public sealed class RipFinderDemoControls : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private RipFinderAgentClient agentClient;

    [SerializeField]
    private RipFinderAssessmentCoordinator assessmentCoordinator;

    [SerializeField]
    private RipFinderDebugControls debugControls;

    [SerializeField]
    private RipFinderMockDetectionSource mockDetectionSource;

    [SerializeField]
    private SentisInferenceRunManager inferenceManager;

    [SerializeField]
    private RipFinderDetectionManager detectionManager;

    [SerializeField]
    private SentisInferenceUiManager inferenceUi;

    [SerializeField]
    private DetectionUiMenuManager detectionMenu;

    [Header("Test Images")]
    [SerializeField]
    private Texture2D ripTestImage;

    [SerializeField]
    private Texture2D noRipTestImage;

    [Header("Style")]
    [Tooltip("Rounded sprite for the card and buttons (Unity's built-in UISprite).")]
    [SerializeField]
    private Sprite roundedSprite;

    [Header("Placement")]
    [Tooltip("Parent for the panel. Defaults to the main camera (head-locked).")]
    [SerializeField]
    private Transform anchor;

    [SerializeField]
    private Vector3 localPosition = new(-0.42f, -0.12f, 1.2f);

    [SerializeField]
    private Vector3 localEulerAngles = new(0f, -19f, 0f);

    [SerializeField]
    private float scale = 0.0015f;

    [Header("Behaviour")]
    [Tooltip("How long a button-triggered assessment stays before live detections may replace it.")]
    [SerializeField]
    private float holdSeconds = 20f;

    private const float PanelWidth = 300f;
    private const float ButtonHeight = 40f;

    private static readonly Color CardColor = new(0.07f, 0.08f, 0.10f, 0.88f);
    private static readonly Color MutedColor = new(0.56f, 0.59f, 0.64f, 1f);
    private static readonly Color LabelColor = new(0.88f, 0.90f, 0.93f, 1f);
    private static readonly Color LowAccent = new(0.45f, 0.75f, 0.58f, 1f);
    private static readonly Color ModerateAccent = new(0.88f, 0.70f, 0.42f, 1f);
    private static readonly Color HighAccent = new(0.90f, 0.47f, 0.45f, 1f);

    private TMP_Text statusText;
    private TMP_Text forecastText;
    private RawImage previewImage;
    private readonly List<GameObject> previewBoxes = new();
    private RectTransform panelRect;
    private BoundsClipper rayClipper;
    private bool pausedForTestImage;
    private readonly List<Button> buttons = new();

    private void Awake()
    {
        if (agentClient == null) agentClient = FindFirstObjectByType<RipFinderAgentClient>();
        if (assessmentCoordinator == null) assessmentCoordinator = FindFirstObjectByType<RipFinderAssessmentCoordinator>();
        if (debugControls == null) debugControls = FindFirstObjectByType<RipFinderDebugControls>();
        if (mockDetectionSource == null) mockDetectionSource = FindFirstObjectByType<RipFinderMockDetectionSource>();
        if (inferenceManager == null) inferenceManager = FindFirstObjectByType<SentisInferenceRunManager>();
        if (detectionManager == null) detectionManager = FindFirstObjectByType<RipFinderDetectionManager>();
        if (inferenceUi == null) inferenceUi = FindFirstObjectByType<SentisInferenceUiManager>();
        if (detectionMenu == null) detectionMenu = FindFirstObjectByType<DetectionUiMenuManager>();
        if (anchor == null && Camera.main != null) anchor = Camera.main.transform;
        if (roundedSprite == null)
        {
            // Reuse the assessment panel's rounded sprite so both cards match.
            var panel = FindFirstObjectByType<RipFinderAssessmentPanel>();
            if (panel != null && panel.TryGetComponent(out Image panelImage)) roundedSprite = panelImage.sprite;
        }

        BuildPanel();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1)) TestLowRisk();
        if (Input.GetKeyDown(KeyCode.Alpha2)) TestModerateRisk();
        if (Input.GetKeyDown(KeyCode.Alpha3)) TestHighRisk();
        if (Input.GetKeyDown(KeyCode.Alpha4)) SendMockDetection();
        if (Input.GetKeyDown(KeyCode.Alpha5)) RunRipTestImage();
        if (Input.GetKeyDown(KeyCode.Alpha6)) RunNoRipTestImage();
        if (Input.GetKeyDown(KeyCode.Alpha7)) ResumeLive();

        // The forecast loads asynchronously and refreshes every 30 minutes.
        if (forecastText.text != assessmentCoordinator.ForecastStatus)
        {
            forecastText.text = assessmentCoordinator.ForecastStatus;
        }

#if UNITY_EDITOR
        // The ISDK input module only handles rays/pinch, so route mouse clicks here.
        if (Input.GetMouseButtonDown(0)) ClickButtonUnderMouse();
#endif
    }

    private void ClickButtonUnderMouse()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        foreach (var button in buttons)
        {
            if (RectTransformUtility.RectangleContainsScreenPoint(
                    (RectTransform)button.transform, Input.mousePosition, cam))
            {
                button.onClick.Invoke();
                return;
            }
        }
    }

    #region Button actions
    public void TestLowRisk() => SendScenario("low risk", debugControls.TestLowRisk);

    public void TestModerateRisk() => SendScenario("moderate risk", debugControls.TestModerateRisk);

    public void TestHighRisk() => SendScenario("high risk", debugControls.TestHighRisk);

    public void SendMockDetection()
    {
        if (!CanSend()) return;

        assessmentCoordinator.SendNextSampleImmediately();
        mockDetectionSource.SendMockDetection();
        assessmentCoordinator.HoldLiveUpdates(holdSeconds);
        SetStatus("Sent mock detection (87%, 2 rips)");
    }

    public void RunRipTestImage() => StartCoroutine(RunTestImage(ripTestImage, "rip"));

    public void RunNoRipTestImage() => StartCoroutine(RunTestImage(noRipTestImage, "no-rip"));

    public void ResumeLive()
    {
        if (pausedForTestImage && detectionManager != null)
        {
            detectionManager.OnPause(false);
        }
        else if (detectionMenu != null)
        {
            // Same as pressing A on the start menu; lets hand-tracking users start.
            detectionMenu.StartDetection();
        }

        pausedForTestImage = false;
        if (inferenceUi != null) inferenceUi.SetOverlayVisible(true);
        ClearPreviewBoxes();
        previewImage.gameObject.SetActive(false);
        UpdateRayBounds();
        assessmentCoordinator.ResetThrottle();
        SetStatus("Live detection");
    }
    #endregion

    private void SendScenario(string name, UnityAction send)
    {
        if (!CanSend()) return;

        send();
        assessmentCoordinator.HoldLiveUpdates(holdSeconds);
        SetStatus($"Sent {name} scenario");
    }

    private bool CanSend()
    {
        if (agentClient.IsRequestRunning)
        {
            SetStatus("Waiting for previous assessment...");
            return false;
        }

        return true;
    }

    private IEnumerator RunTestImage(Texture2D image, string label)
    {
        if (image == null)
        {
            SetStatus($"No {label} test image assigned");
            yield break;
        }

        if (!inferenceManager.IsModelLoaded)
        {
            SetStatus("Model still loading...");
            yield break;
        }

        // Stop live camera inference so it doesn't overwrite the test result.
        if (!pausedForTestImage && detectionManager != null && !detectionManager.IsPaused)
        {
            detectionManager.OnPause(true);
            pausedForTestImage = true;
        }

        SetStatus($"Running {label} test image...");

        while (inferenceManager.IsRunning())
        {
            yield return null;
        }

        previewImage.texture = image;
        previewImage.gameObject.SetActive(true);
        UpdateRayBounds();
        ClearPreviewBoxes();

        // Boxes go on the thumbnail instead of the floating camera overlay.
        if (inferenceUi != null) inferenceUi.SetOverlayVisible(false);

        // A still image is a single frame, so skip persistence and send it immediately.
        assessmentCoordinator.SendNextSampleImmediately();
        inferenceManager.RunInference(image);

        while (inferenceManager.IsRunning())
        {
            yield return null;
        }

        if (inferenceUi != null) DrawPreviewBoxes(inferenceUi.BoxDrawn);

        int count = inferenceUi != null ? inferenceUi.BoxDrawn.Count : 0;
        SetStatus($"{label} test image · {count} detection{(count == 1 ? "" : "s")}");
    }

    private void DrawPreviewBoxes(List<SentisInferenceUiManager.BoundingBox> boxes)
    {
        ClearPreviewBoxes();

        foreach (var box in boxes)
        {
            var go = new GameObject("PreviewBox", typeof(RectTransform));
            go.transform.SetParent(previewImage.transform, false);
            var rect = (RectTransform)go.transform;
            float left = box.NormCenterX - box.NormWidth * 0.5f;
            float right = box.NormCenterX + box.NormWidth * 0.5f;
            float top = 1f - (box.NormCenterY - box.NormHeight * 0.5f);
            float bottom = 1f - (box.NormCenterY + box.NormHeight * 0.5f);
            rect.anchorMin = new Vector2(Mathf.Clamp01(left), Mathf.Clamp01(bottom));
            rect.anchorMax = new Vector2(Mathf.Clamp01(right), Mathf.Clamp01(top));
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            CreateEdge(rect, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 2f));
            CreateEdge(rect, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 2f));
            CreateEdge(rect, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(2f, 0f));
            CreateEdge(rect, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(2f, 0f));

            var label = CreateText(rect, $"{box.ClassName} {box.Confidence * 100f:0}%", 11,
                FontStyles.Bold, 0f, Color.white, TextAlignmentOptions.BottomLeft);
            label.textWrappingMode = TextWrappingModes.NoWrap;
            var labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = new Vector2(0f, 1f);
            labelRect.anchorMax = new Vector2(1f, 1f);
            labelRect.pivot = new Vector2(0f, 0f);
            labelRect.sizeDelta = new Vector2(0f, 14f);
            labelRect.anchoredPosition = new Vector2(2f, 2f);

            previewBoxes.Add(go);
        }
    }

    private static void CreateEdge(RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 size)
    {
        var edge = new GameObject("Edge", typeof(RectTransform));
        edge.transform.SetParent(parent, false);
        var rect = (RectTransform)edge.transform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.sizeDelta = size;
        rect.anchoredPosition = Vector2.zero;
        var image = edge.AddComponent<Image>();
        image.color = HighAccent;
        image.raycastTarget = false;
    }

    private void ClearPreviewBoxes()
    {
        foreach (var box in previewBoxes) Destroy(box);
        previewBoxes.Clear();
    }

    private void SetStatus(string message)
    {
        statusText.text = message;
        Debug.Log($"[RipFinder Demo] {message}");
    }

    #region Panel construction
    private void BuildPanel()
    {
        // Built inactive so ISDK components are injected before their Awake/Start run.
        var root = new GameObject("RipFinderDemoControlsCanvas", typeof(RectTransform));
        root.SetActive(false);
        root.layer = LayerMask.NameToLayer("UI");

        var rootRect = (RectTransform)root.transform;
        rootRect.SetParent(anchor, false);
        rootRect.localPosition = localPosition;
        rootRect.localEulerAngles = localEulerAngles;
        rootRect.localScale = Vector3.one * scale;

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        root.AddComponent<GraphicRaycaster>();

        var background = root.AddComponent<Image>();
        background.sprite = roundedSprite;
        background.type = Image.Type.Sliced;
        background.color = CardColor;

        var layout = root.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 16, 16);
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        var fitter = root.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        rootRect.sizeDelta = new Vector2(PanelWidth, 0f);

        var title = CreateText(rootRect, "RIPFINDER DEMO", 14, FontStyles.Bold, 24f, MutedColor, TextAlignmentOptions.Left);
        title.characterSpacing = 8f;

        CreateSectionLabel(rootRect, "SCENARIOS");
        buttons.Add(CreateButton(rootRect, "1", "Low risk", LowAccent, TestLowRisk));
        buttons.Add(CreateButton(rootRect, "2", "Moderate risk", ModerateAccent, TestModerateRisk));
        buttons.Add(CreateButton(rootRect, "3", "High risk", HighAccent, TestHighRisk));

        CreateSectionLabel(rootRect, "DETECTION");
        buttons.Add(CreateButton(rootRect, "4", "Mock detection", null, SendMockDetection));
        buttons.Add(CreateButton(rootRect, "5", "Rip test image", null, RunRipTestImage));
        buttons.Add(CreateButton(rootRect, "6", "No-rip test image", null, RunNoRipTestImage));

        CreateSectionLabel(rootRect, "CAMERA");
        buttons.Add(CreateButton(rootRect, "7", "Start / resume live detection", null, ResumeLive));

        var previewGo = new GameObject("TestImagePreview", typeof(RectTransform));
        previewGo.transform.SetParent(rootRect, false);
        previewImage = previewGo.AddComponent<RawImage>();
        var previewLayout = previewGo.AddComponent<LayoutElement>();
        previewLayout.preferredHeight = 150f;
        previewGo.SetActive(false);

        CreateSectionLabel(rootRect, "STATUS");
        statusText = CreateText(rootRect, "Ready", 14, FontStyles.Normal, 20f, LabelColor, TextAlignmentOptions.Left);
        forecastText = CreateText(rootRect, assessmentCoordinator.ForecastStatus, 13, FontStyles.Normal, 20f, MutedColor, TextAlignmentOptions.Left);

        panelRect = rootRect;
        AddRayInteraction(root, canvas);

        root.SetActive(true);
        UpdateRayBounds();
    }

    // Keeps the ray hit area matched to the panel, which resizes when
    // the test image preview is shown or hidden.
    private void UpdateRayBounds()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        Vector2 size = panelRect.rect.size;
        rayClipper.Size = new Vector3(size.x, size.y, 1f);
    }

    private void AddRayInteraction(GameObject root, Canvas canvas)
    {
        var surfaceGo = new GameObject("RaySurface");
        surfaceGo.transform.SetParent(root.transform, false);

        var plane = surfaceGo.AddComponent<PlaneSurface>();
        plane.InjectDoubleSided(true);

        rayClipper = surfaceGo.AddComponent<BoundsClipper>();

        var clippedSurface = surfaceGo.AddComponent<ClippedPlaneSurface>();
        clippedSurface.InjectAllClippedPlaneSurface(plane, new IBoundsClipper[] { rayClipper });

        var pointableCanvas = root.AddComponent<PointableCanvas>();
        pointableCanvas.InjectAllPointableCanvas(canvas);

        var rayInteractable = root.AddComponent<RayInteractable>();
        rayInteractable.InjectAllRayInteractable(clippedSurface);
        rayInteractable.InjectOptionalPointableElement(pointableCanvas);
    }

    private static TMP_Text CreateText(RectTransform parent, string text, float fontSize, FontStyles style, float minHeight,
        Color color, TextAlignmentOptions alignment)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.textWrappingMode = TextWrappingModes.Normal;
        tmp.raycastTarget = false;

        go.AddComponent<LayoutElement>().minHeight = minHeight;
        return tmp;
    }

    private static void CreateSectionLabel(RectTransform parent, string label)
    {
        var spacer = new GameObject("Spacer", typeof(RectTransform));
        spacer.transform.SetParent(parent, false);
        spacer.AddComponent<LayoutElement>().preferredHeight = 4f;

        var text = CreateText(parent, label, 11, FontStyles.Bold, 16f, MutedColor, TextAlignmentOptions.Left);
        text.characterSpacing = 6f;
    }

    // Quiet, neutral button: translucent fill that brightens on hover, a muted
    // hotkey number, and an optional thin colour accent on the left edge.
    private Button CreateButton(RectTransform parent, string key, string label, Color? accent, UnityAction onClick)
    {
        var go = new GameObject(label + " Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var image = go.AddComponent<Image>();
        image.sprite = roundedSprite;
        image.type = Image.Type.Sliced;
        image.color = Color.white;

        var button = go.AddComponent<Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.normalColor = new Color(1f, 1f, 1f, 0.06f);
        colors.highlightedColor = new Color(1f, 1f, 1f, 0.14f);
        colors.selectedColor = colors.normalColor;
        colors.pressedColor = new Color(1f, 1f, 1f, 0.22f);
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        button.onClick.AddListener(onClick);

        go.AddComponent<LayoutElement>().preferredHeight = ButtonHeight;

        if (accent.HasValue)
        {
            var stripe = new GameObject("Accent", typeof(RectTransform));
            stripe.transform.SetParent(go.transform, false);
            var stripeRect = (RectTransform)stripe.transform;
            stripeRect.anchorMin = new Vector2(0f, 0.2f);
            stripeRect.anchorMax = new Vector2(0f, 0.8f);
            stripeRect.sizeDelta = new Vector2(3f, 0f);
            stripeRect.anchoredPosition = new Vector2(8f, 0f);
            var stripeImage = stripe.AddComponent<Image>();
            stripeImage.color = accent.Value;
            stripeImage.raycastTarget = false;
        }

        var text = CreateText((RectTransform)go.transform,
            $"<color=#8A929C>{key}</color>    {label}",
            17, FontStyles.Normal, 0f, LabelColor, TextAlignmentOptions.Left);
        var textRect = (RectTransform)text.transform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(20f, 0f);
        textRect.offsetMax = new Vector2(-12f, 0f);

        return button;
    }
    #endregion
}
