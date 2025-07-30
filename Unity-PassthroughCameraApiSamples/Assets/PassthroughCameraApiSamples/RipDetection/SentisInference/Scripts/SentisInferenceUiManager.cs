// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using Meta.XR.Samples;
using Unity.Sentis;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Collections;

namespace PassthroughCameraSamples.MultiObjectDetection
{
    [MetaCodeSample("PassthroughCameraApiSamples-MultiObjectDetection")]
    public class SentisInferenceUiManager : MonoBehaviour
    {
        [Header("Placement configureation")]
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
        
        [SerializeField] private Text m_controlsInfoText;
        private Coroutine m_restoreTextCoroutine;
        private string m_baseFooterText = "Move the poster to reposition it.";
        private float lastScreenshotTime = 0f;
        private float screenshotCooldown = 3f;


        public List<BoundingBox> BoxDrawn = new();

        private string[] m_labels;
        private List<GameObject> m_boxPool = new();
        private Transform m_displayLocation;

        private const int RIP_CLASS_ID = 0; 

        //bounding box data
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

        #region Unity Functions
        private void Start()
        {
            m_displayLocation = m_displayImage.transform;
        }
        #endregion

        #region Detection Functions
        public void OnObjectDetectionError()
        {
            // Clear current boxes
            ClearAnnotations();

            // Set obejct found to 0
            OnObjectsDetected?.Invoke(0);
        }
        #endregion

        #region BoundingBoxes functions
        public void SetLabels(TextAsset labelsAsset)
        {
            //Parse neural net m_labels
            m_labels = labelsAsset.text.Split('\n');
        }

        public void ShowScreenshotMessage()
        {
            if (m_restoreTextCoroutine != null)
                StopCoroutine(m_restoreTextCoroutine);

            m_controlsInfoText.text = m_baseFooterText + "\n📸 Screenshot captured!";
            m_restoreTextCoroutine = StartCoroutine(RestoreFooterTextAfterDelay());
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
            ripCurrentPosterObject.SetActive(true);
        }

        public void DrawUIBoxes(Tensor<float> output, float imageWidth, float imageHeight)
        {
            m_detectionCanvas.UpdatePosition();
            ClearAnnotations();

            var displayWidth = m_displayImage.rectTransform.rect.width;
            var displayHeight = m_displayImage.rectTransform.rect.height;

            int channels = output.shape[1];
            int boxes = output.shape[2];

            for (int i = 0; i < boxes; i++)
            {
                float x, y, w, h, confidence;
                int classId = 0;

                if (channels == 84)
                {
                    // YOLOv5/v8-style model
                    float objConf = output[0, 4, i];
                    float maxClassConf = 0f;

                    for (int j = 5; j < 84; j++)
                    {
                        float classConf = output[0, j, i];
                        if (classConf > maxClassConf)
                        {
                            maxClassConf = classConf;
                            classId = j - 5;
                        }
                    }

                    confidence = objConf * maxClassConf;
                    if (confidence < 0.25f) continue;

                    x = output[0, 0, i];
                    y = output[0, 1, i];
                    w = output[0, 2, i];
                    h = output[0, 3, i];

                    if (classId == RIP_CLASS_ID)
                    {
                        if (Time.time - lastScreenshotTime > screenshotCooldown)
                        {
                            lastScreenshotTime = Time.time;
                            var texture = m_runManager.CapturePassthroughFrame(m_webCamTextureManager.WebCamTexture);
                            m_runManager.SaveTextureAsPNG(texture, $"rip_{System.DateTime.Now:yyyyMMdd_HHmmss}.png");
                            ShowScreenshotMessage();
                        }

                        // Show poster only once
                        if (!posterDisplayed && ripCurrentPosterObject != null)
                        {
                            posterDisplayed = true;
                            StartCoroutine(ShowPosterWithDelay());
                        }
                    }

                }
                else if (channels == 5)
                {
                    // YOLOv9Sentis-style
                    confidence = output[0, 4, i];
                    if (confidence < 0.25f) continue;

                    x = output[0, 0, i];
                    y = output[0, 1, i];
                    w = output[0, 2, i];
                    h = output[0, 3, i];

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
                else
                {
                    Debug.LogWarning("Unsupported model output shape");
                    return;
                }

                var scaleX = displayWidth / imageWidth;
                var scaleY = displayHeight / imageHeight;
                var halfWidth = displayWidth / 2f;
                var halfHeight = displayHeight / 2f;

                BoundingBox box = new BoundingBox
                {
                    CenterX = x * scaleX - halfWidth,
                    CenterY = y * scaleY - halfHeight,
                    Width = w * scaleX,
                    Height = h * scaleY,
                    Label = $"rip current ({(confidence * 100f):0.0}% confidence)",
                    ClassName = "rip",
                    Confidence = confidence,
                    WorldPos = new Vector3(0, 0, 0.5f)
                };

                BoxDrawn.Add(box);
                DrawBox(box, classId);
            }
        }

       

        private void ClearAnnotations()
        {
            foreach (var box in m_boxPool)
            {
                box?.SetActive(false);
            }
            BoxDrawn.Clear();
        }

        private void DrawBox(BoundingBox box, int id)
        {
            Debug.Log($"[DrawBox] Creating box {id} at position ({box.CenterX}, {box.CenterY}) with size ({box.Width}, {box.Height})");
            
            //Create the bounding box graphic or get from pool
            GameObject panel;
            if (id < m_boxPool.Count)
            {
                panel = m_boxPool[id];
                if (panel == null)
                {
                    panel = CreateNewBox(m_boxColor);
                }
                else
                {
                    panel.SetActive(true);
                }
            }
            else
            {
                panel = CreateNewBox(m_boxColor);
            }
            //Set box position
            panel.transform.localPosition = new Vector3(box.CenterX, -box.CenterY, box.WorldPos.HasValue ? box.WorldPos.Value.z : 0.0f);
            //Set box rotation
            panel.transform.rotation = Quaternion.LookRotation(panel.transform.position - m_detectionCanvas.GetCapturedCameraPosition());
            //Set box size
            var rt = panel.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(box.Width, box.Height);
            //Set label text
            var label = panel.GetComponentInChildren<Text>();
            label.text = box.Label;
            label.fontSize = 12;
            
            Debug.Log($"[DrawBox] Box {id} created successfully. Panel active: {panel.activeInHierarchy}");
        }

        private GameObject CreateNewBox(Color color)
        {
            //Create the box and set image
            var panel = new GameObject("ObjectBox");
            _ = panel.AddComponent<CanvasRenderer>();
            var img = panel.AddComponent<Image>();
            img.color = color;
            img.sprite = m_boxTexture;
            img.type = Image.Type.Sliced;
            img.fillCenter = false;
            panel.transform.SetParent(m_displayLocation, false);

            //Create the label
            var text = new GameObject("ObjectLabel");
            _ = text.AddComponent<CanvasRenderer>();
            text.transform.SetParent(panel.transform, false);
            var txt = text.AddComponent<Text>();
            txt.font = m_font;
            txt.color = m_fontColor;
            txt.fontSize = m_fontSize;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;

            var rt2 = text.GetComponent<RectTransform>();
            rt2.offsetMin = new Vector2(20, rt2.offsetMin.y);
            rt2.offsetMax = new Vector2(0, rt2.offsetMax.y);
            rt2.offsetMin = new Vector2(rt2.offsetMin.x, 0);
            rt2.offsetMax = new Vector2(rt2.offsetMax.x, 30);
            rt2.anchorMin = new Vector2(0, 0);
            rt2.anchorMax = new Vector2(1, 1);

            m_boxPool.Add(panel);
            return panel;
        }
        #endregion
    }
}