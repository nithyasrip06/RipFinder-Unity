// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using Meta.XR.Samples;
using Unity.Sentis;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Collections;
using System;
using System.Linq;

namespace PassthroughCameraSamples.MultiObjectDetection
{
    [MetaCodeSample("PassthroughCameraApiSamples-MultiObjectDetection")]
    public class SentisInferenceUiManager : MonoBehaviour
    {
        [Header("Placement configuration")]
        [SerializeField] private EnvironmentRayCastSampleManager m_environmentRaycast;
        [SerializeField] private WebCamTextureManager m_webCamTextureManager;
        private PassthroughCameraEye CameraEye => m_webCamTextureManager.Eye;

        [Header("UI display references")]
        [SerializeField] private SentisObjectDetectedUiManager m_detectionCanvas;
        [SerializeField] private RawImage m_displayImage;
        [SerializeField] private Sprite m_boxTexture;
        [SerializeField] private Color m_boxColor;
        [SerializeField] private Font m_font;
        [SerializeField] private Color m_fontColor;
        [SerializeField] private int m_fontSize = 80;
        [Space(10)]
        public UnityEvent<int> OnObjectsDetected;
        [SerializeField] private GameObject ripCurrentPosterObject;
        private bool posterDisplayed = false;
        [SerializeField] private SentisInferenceRunManager m_runManager;
        [SerializeField] private GameObject m_arrowPrefab;
        [SerializeField] private Transform m_arrowCanvasParent;
        private List<GameObject> m_arrowPool = new();
        private static float Sigmoid(float x) => 1f / (1f + Mathf.Exp(-x));
        private int m_activeBoxCount = 0;

        [SerializeField] private Text m_controlsInfoText;
        private Coroutine m_restoreTextCoroutine;
        private string m_baseFooterText = "Pinch to grab poster from a distance\n and move to reposition. Press Meta\n button and open camera to record.";
        private float lastScreenshotTime = 0f;
        private float screenshotCooldown = 3f;
        private Dictionary<int, int> lastClassAssignments = new Dictionary<int, int>();

        private const float SWITCH_THRESHOLD = 0.0005f;

        public List<BoundingBox> BoxDrawn = new();

        private string[] m_labels;
        private List<GameObject> m_boxPool = new();
        private Transform m_displayLocation;

        private const int RIP_CLASS_ID = 0;

        [Header("Coordinate options")]
        [Tooltip("Enable if your passthrough/image feed is horizontally mirrored.")]
        [SerializeField] private bool m_flipX = false;

        public struct BoundingBox
        {
            public float CenterX;
            public float CenterY;
            public float Width;
            public float Height;
            public string Label;
            public Vector3? WorldPos;
            public string ClassName;
            public float Confidence;
        }

        private void Start()
        {
            m_displayLocation = m_displayImage.transform;
        }

        public void OnObjectDetectionError()
        {
            ClearAnnotations();
            OnObjectsDetected?.Invoke(0);
        }

        public void SetLabels(TextAsset labelsAsset)
        {
            m_labels = labelsAsset.text.Split('\n');
        }

        public void ShowScreenshotMessage()
        {
            if (m_restoreTextCoroutine != null)
                StopCoroutine(m_restoreTextCoroutine);

            m_controlsInfoText.text = m_baseFooterText + "\nScreenshot captured!";
            m_restoreTextCoroutine = StartCoroutine(RestoreFooterTextAfterDelay());
        }

        private Color GetColorForClass(string className)
        {
            if (className.Equals("sediment_rip", System.StringComparison.OrdinalIgnoreCase))
                return new Color(0.216f, 0.847f, 0.259f);
            if (className.Equals("rip", System.StringComparison.OrdinalIgnoreCase))
                return new Color(0.847f, 0.212f, 0.447f);
            return m_boxColor;
        }

        private IEnumerator RestoreFooterTextAfterDelay()
        {
            yield return new WaitForSeconds(5f);
            m_controlsInfoText.text = m_baseFooterText;
        }

        public void SetDetectionCapture(Texture image)
        {
            m_displayImage.texture = image;
            m_detectionCanvas.CapturePosition();
        }

        private IEnumerator ShowPosterWithDelay()
        {
            yield return new WaitForSeconds(3f);
            if (ripCurrentPosterObject != null)
                ripCurrentPosterObject.SetActive(true);
        }

        public void DrawUIBoxes(Tensor<float> output, float imageWidth, float imageHeight)
        {
            m_detectionCanvas.UpdatePosition();
            ClearAnnotations();

            // --- Aspect-preserving scale + letterbox for the RawImage target ---
            var rect = m_displayImage.rectTransform.rect;
            float dispW = rect.width, dispH = rect.height;

            float sX = dispW / imageWidth;
            float sY = dispH / imageHeight;
            float s  = Mathf.Min(sX, sY);                   // uniform scale (fit)
            float xPad = (dispW - imageWidth * s) * 0.5f;   // horizontal letterbox padding
            float yPad = (dispH - imageHeight * s) * 0.5f;  // vertical letterbox padding
            float halfW = dispW * 0.5f;
            float halfH = dispH * 0.5f;

            int channels = output.shape[1];
            int boxes = output.shape[2];

            var candidates = new List<(BoundingBox box, int classId, float conf)>();

            for (int i = 0; i < boxes; i++)
            {
                float x = 0f, y = 0f, w = 0f, h = 0f, confidence = 0f;
                int classId = 0;
                bool keep = false;

                if (channels == 84)
                {
                    float objConf = output[0, 4, i];
                    float maxClassConf = 0f;
                    int best = 0;

                    for (int j = 5; j < 84; j++)
                    {
                        float classConf = output[0, j, i];
                        if (classConf > maxClassConf)
                        {
                            maxClassConf = classConf;
                            best = j - 5;
                        }
                    }

                    confidence = objConf * maxClassConf;
                    if (confidence >= 0.25f)
                    {
                        x = output[0, 0, i];
                        y = output[0, 1, i];
                        w = output[0, 2, i];
                        h = output[0, 3, i];
                        classId = best;
                        keep = true;

                        if (classId == RIP_CLASS_ID)
                        {
                            if (Time.time - lastScreenshotTime > screenshotCooldown)
                            {
                                lastScreenshotTime = Time.time;
                                var texture = m_runManager.CapturePassthroughFrame(m_webCamTextureManager.WebCamTexture);
                                m_runManager.SaveTextureAsPNG(texture, $"rip_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
                                ShowScreenshotMessage();
                            }
                            if (!posterDisplayed && ripCurrentPosterObject != null)
                            {
                                posterDisplayed = true;
                                StartCoroutine(ShowPosterWithDelay());
                            }
                        }
                    }
                }
                else if (channels == 6)
                {
                    float obj = output[0, 4, i];
                    float clsLogit = output[0, 5, i];

                    float objProb = (obj < 0f || obj > 1f) ? Sigmoid(obj) : obj;

                    int boxId = i;

                    bool isSediment = clsLogit > 0.0001f;
                    int currentClass = isSediment ? 1 : 0;

                    if (lastClassAssignments.TryGetValue(boxId, out int lastClass))
                    {
                        if (lastClass != currentClass && Mathf.Abs(clsLogit) < SWITCH_THRESHOLD)
                        {
                            currentClass = lastClass;
                        }
                    }
                    lastClassAssignments[boxId] = currentClass;

                    float classProb = Sigmoid(clsLogit);
                    confidence = objProb * classProb;

                    if (confidence >= 0.15f)
                    {
                        x = output[0, 0, i];
                        y = output[0, 1, i];
                        w = output[0, 2, i];
                        h = output[0, 3, i];

                        classId = currentClass;
                        keep = true;

                        if (!posterDisplayed && ripCurrentPosterObject != null)
                        {
                            posterDisplayed = true;
                            StartCoroutine(ShowPosterWithDelay());
                        }
                        if (Time.time - lastScreenshotTime > screenshotCooldown)
                        {
                            lastScreenshotTime = Time.time;
                            var texture = m_runManager.CapturePassthroughFrame(m_webCamTextureManager.WebCamTexture);
                            m_runManager.SaveTextureAsPNG(texture, $"rip_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
                            ShowScreenshotMessage();
                        }
                    }
                }
                else if (channels == 5)
                {
                    confidence = output[0, 4, i];
                    if (confidence >= 0.25f)
                    {
                        x = output[0, 0, i];
                        y = output[0, 1, i];
                        w = output[0, 2, i];
                        h = output[0, 3, i];
                        classId = 0;
                        keep = true;

                        if (!posterDisplayed && ripCurrentPosterObject != null)
                        {
                            posterDisplayed = true;
                            StartCoroutine(ShowPosterWithDelay());
                        }
                        if (Time.time - lastScreenshotTime > screenshotCooldown)
                        {
                            lastScreenshotTime = Time.time;
                            var texture = m_runManager.CapturePassthroughFrame(m_webCamTextureManager.WebCamTexture);
                            m_runManager.SaveTextureAsPNG(texture, $"rip_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
                            ShowScreenshotMessage();
                        }
                    }
                }
                else
                {
                    Debug.LogWarning("Unsupported model output shape");
                    return;
                }

                if (!keep) continue;

                // If model outputs normalized coords, convert to pixels first
                if (w <= 1f && h <= 1f)
                {
                    x *= imageWidth;
                    y *= imageHeight;
                    w *= imageWidth;
                    h *= imageHeight;
                }

                // Optional horizontal flip for mirrored sources
                float xSrc = m_flipX ? (imageWidth - x) : x;

                // Map model pixels -> RawImage local UI coords (origin at center)
                float cx = xSrc * s - halfW + xPad;
                float cy = y * s - halfH + yPad;
                float ww = Mathf.Max(1f, w * s);
                float hh = Mathf.Max(1f, h * s);

                string safeLabel = (m_labels != null && classId >= 0 && classId < m_labels.Length)
                    ? m_labels[classId].Trim()
                    : $"class_{classId}";

                var box = new BoundingBox
                {
                    CenterX = cx,
                    CenterY = cy,
                    Width   = ww,
                    Height  = hh,
                    Label   = $"{safeLabel} ({(confidence * 100f):0.0}% confidence)",
                    ClassName = safeLabel,
                    Confidence = confidence,
                    WorldPos = new Vector3(0, 0, 0.5f)
                };

                candidates.Add((box, classId, confidence));
            }

            var keepIdx = NmsIndices(candidates, 0.5f);

            foreach (var idx in keepIdx)
            {
                var (b, cid, conf) = candidates[idx];

                BoxDrawn.Add(b);
                var color = GetColorForClass(b.ClassName);

                DrawBox(b, m_activeBoxCount, color);
                m_activeBoxCount++;

                var arrow = GetOrCreateArrow();
                if (arrow != null)
                {
                    arrow.transform.SetParent(m_arrowCanvasParent, false);

                    // Ensure arrow ignores layout too (just in case parent has a LayoutGroup)
                    var le = arrow.GetComponent<LayoutElement>() ?? arrow.AddComponent<LayoutElement>();
                    le.ignoreLayout = true;

                    var rt = arrow.GetComponent<RectTransform>();
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2(b.CenterX, -b.CenterY); // UI Y-down
                    rt.localRotation = Quaternion.identity;
                    rt.localScale = Vector3.one;
                    arrow.SetActive(true);
                }
            }
            OnObjectsDetected?.Invoke(keepIdx.Count);
        }

        private void CleanupOldAssignments()
        {
            if (lastClassAssignments.Count > 100)
            {
                var keysToRemove = lastClassAssignments.Keys.Take(50).ToList();
                foreach (var key in keysToRemove)
                    lastClassAssignments.Remove(key);
            }
        }

        private void Update()
        {
            if (Time.frameCount % 30 == 0)
                CleanupOldAssignments();
        }

        private static float IoU(BoundingBox a, BoundingBox b)
        {
            float ax1 = a.CenterX - a.Width * 0.5f;
            float ay1 = a.CenterY - a.Height * 0.5f;
            float ax2 = a.CenterX + a.Width * 0.5f;
            float ay2 = a.CenterY + a.Height * 0.5f;

            float bx1 = b.CenterX - b.Width * 0.5f;
            float by1 = b.CenterY - b.Height * 0.5f;
            float bx2 = b.CenterX + b.Width * 0.5f;
            float by2 = b.CenterY + b.Height * 0.5f;

            float interX1 = Mathf.Max(ax1, bx1);
            float interY1 = Mathf.Max(ay1, by1);
            float interX2 = Mathf.Min(ax2, bx2);
            float interY2 = Mathf.Min(ay2, by2);

            float interW = Mathf.Max(0f, interX2 - interX1);
            float interH = Mathf.Max(0f, interY2 - interY1);
            float inter = interW * interH;

            float areaA = (ax2 - ax1) * (ay2 - ay1);
            float areaB = (bx2 - bx1) * (by2 - by1);
            float denom = areaA + areaB - inter + 1e-6f;

            return inter / denom;
        }

        private static List<int> NmsIndices(List<(BoundingBox box, int classId, float conf)> cands, float iouThr = 0.5f)
        {
            var idxs = new List<int>();
            var order = new List<int>();
            for (int i = 0; i < cands.Count; i++) order.Add(i);

            order.Sort((i, j) => cands[j].conf.CompareTo(cands[i].conf));

            while (order.Count > 0)
            {
                int i = order[0];
                idxs.Add(i);
                order.RemoveAt(0);

                for (int k = order.Count - 1; k >= 0; k--)
                {
                    int j = order[k];
                    if (IoU(cands[i].box, cands[j].box) > iouThr)
                        order.RemoveAt(k);
                }
            }
            return idxs;
        }

        private GameObject GetOrCreateArrow()
        {
            foreach (var arrow in m_arrowPool)
            {
                if (!arrow.activeInHierarchy)
                    return arrow;
            }

            if (m_arrowPool.Count >= 5)
                return null;

            GameObject newArrow = Instantiate(m_arrowPrefab);
            m_arrowPool.Add(newArrow);
            return newArrow;
        }

        private void ClearAnnotations()
        {
            foreach (var box in m_boxPool)
            {
                box?.SetActive(false);
            }
            BoxDrawn.Clear();

            foreach (var arrow in m_arrowPool)
            {
                arrow?.SetActive(false);
            }
            m_activeBoxCount = 0;
        }

        private void DrawBox(BoundingBox box, int poolIndex, Color color)
        {
            GameObject panel;
            if (poolIndex < m_boxPool.Count)
            {
                panel = m_boxPool[poolIndex] ?? CreateNewBox(color);
                panel.SetActive(true);
            }
            else
            {
                panel = CreateNewBox(color);
            }

            var img = panel.GetComponent<Image>();
            if (img != null) img.color = color;

            var rt = panel.GetComponent<RectTransform>();
            rt.anchoredPosition = new Vector2(box.CenterX, -box.CenterY); // UI Y-down
            rt.sizeDelta = new Vector2(box.Width, box.Height);
            rt.localRotation = Quaternion.identity;
            rt.localScale = Vector3.one;

            // Only face camera for World Space canvases
            var canvas = panel.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
            {
                panel.transform.rotation =
                    Quaternion.LookRotation(panel.transform.position - m_detectionCanvas.GetCapturedCameraPosition());
            }

            var label = panel.GetComponentInChildren<Text>();
            label.text = box.Label;

            if (box.ClassName.Equals("sediment_rip", System.StringComparison.OrdinalIgnoreCase))
                label.color = new Color(0.216f, 0.847f, 0.259f);
            else if (box.ClassName.Equals("rip", System.StringComparison.OrdinalIgnoreCase))
                label.color = new Color(0.847f, 0.212f, 0.447f);
            else
                label.color = m_fontColor;

            label.fontSize = 12;
        }

        private GameObject CreateNewBox(Color color)
        {
            // Create the box and set image
            var panel = new GameObject("ObjectBox");
            _ = panel.AddComponent<CanvasRenderer>();
            var img = panel.AddComponent<Image>();
            img.color = color;
            img.sprite = m_boxTexture;
            img.type = Image.Type.Sliced;
            img.fillCenter = false;
            panel.transform.SetParent(m_displayLocation, false);

            // Centered anchors/pivot so multiple boxes don't stretch/slide
            var rt = panel.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;

            // Ignore parent LayoutGroups trying to move/resize us
            var le = panel.AddComponent<LayoutElement>();
            le.ignoreLayout = true;

            // Create the label
            var text = new GameObject("ObjectLabel");
            _ = text.AddComponent<CanvasRenderer>();
            text.transform.SetParent(panel.transform, false);
            var txt = text.AddComponent<Text>();
            txt.font = m_font;
            txt.color = m_fontColor;
            txt.fontSize = m_fontSize;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;

            var rt2 = text.GetComponent<RectTransform>();
            rt2.anchorMin = new Vector2(0, 0);
            rt2.anchorMax = new Vector2(1, 1);
            rt2.offsetMin = new Vector2(20, 0);
            rt2.offsetMax = new Vector2(0, 30);

            m_boxPool.Add(panel);
            return panel;
        }
    }
}