using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public sealed class RipFinderAgentClient : MonoBehaviour
{
    [Header("Connection")]

    [Tooltip("Used when running inside the Unity Editor.")]
    [SerializeField]
    private string editorEndpoint =
        "http://127.0.0.1:8000/v1/assess";

    [Tooltip(
        "Used on the Quest build. " +
        "For local testing, use your computer's LAN IP. " +
        "For deployment, use an HTTPS backend URL."
    )]
    [SerializeField]
    private string headsetEndpoint =
        "http://192.168.1.100:8000/v1/assess";

    [SerializeField]
    [Min(1)]
    private int timeoutSeconds = 10;

    public bool IsRequestRunning { get; private set; }

    public event Action<RipFinderAssessmentResponse>
        AssessmentReceived;

    public event Action<string>
        AssessmentFailed;

    private string Endpoint
    {
        get
        {
#if UNITY_EDITOR
            return editorEndpoint;
#else
            return headsetEndpoint;
#endif
        }
    }

    private void Start()
    {
        Debug.Log(
            "[RipFinder AI] Assessment endpoint: " +
            Endpoint
        );
    }

    public void RequestAssessment(
        string beach,
        bool ripDetected,
        float confidence,
        int detectionCount,
        string forecast)
    {
        if (IsRequestRunning)
        {
            Debug.Log(
                "[RipFinder AI] " +
                "Assessment request already in progress."
            );

            return;
        }

        var payload =
            new RipFinderAssessmentRequest
            {
                beach =
                    string.IsNullOrWhiteSpace(beach)
                        ? "Unknown beach"
                        : beach,

                rip_detected =
                    ripDetected,

                confidence =
                    ripDetected
                        ? Mathf.Clamp01(confidence)
                        : 0f,

                detection_count =
                    ripDetected
                        ? Mathf.Max(1, detectionCount)
                        : 0,

                forecast =
                    forecast ?? string.Empty
            };

        StartCoroutine(
            SendAssessment(payload)
        );
    }

    private IEnumerator SendAssessment(
        RipFinderAssessmentRequest payload)
    {
        IsRequestRunning = true;

        string json =
            JsonUtility.ToJson(payload);

        Debug.Log(
            "[RipFinder AI] Sending assessment request: " +
            json
        );

        byte[] body =
            Encoding.UTF8.GetBytes(json);

        using var request =
            new UnityWebRequest(
                Endpoint,
                UnityWebRequest.kHttpVerbPOST
            );

        request.uploadHandler =
            new UploadHandlerRaw(body);

        request.downloadHandler =
            new DownloadHandlerBuffer();

        request.SetRequestHeader(
            "Content-Type",
            "application/json"
        );

        request.timeout =
            timeoutSeconds;

        // RIPFINDER_TIMING (removable instrumentation): client-observed HTTP
        // round-trip. This is transport + serialization + full server processing
        // (deterministic engine + Claude generation), NOT pure network latency.
        double t_http0 = Time.realtimeSinceStartupAsDouble;

        yield return request.SendWebRequest();

        double t_http1 = Time.realtimeSinceStartupAsDouble;
        Debug.Log(
            $"RIPFINDER_TIMING,stage=http_round_trip,ms={(t_http1 - t_http0) * 1000.0:0.00}"
        );

        IsRequestRunning = false;

        if (
            request.result !=
            UnityWebRequest.Result.Success
        )
        {
            string responseBody =
                request.downloadHandler != null
                    ? request.downloadHandler.text
                    : string.Empty;

            string errorMessage =
                "[RipFinder AI] Assessment failed. " +
                $"HTTP {request.responseCode}: " +
                $"{request.error}\n" +
                responseBody;

            Debug.LogWarning(
                errorMessage
            );

            AssessmentFailed?.Invoke(
                errorMessage
            );

            yield break;
        }

        try
        {
            string responseJson =
                request.downloadHandler.text;

            Debug.Log(
                "[RipFinder AI] Assessment response received: " +
                responseJson
            );

            var response =
                JsonUtility
                    .FromJson<RipFinderAssessmentResponse>(
                        responseJson
                    );

            if (
                response == null ||
                string.IsNullOrWhiteSpace(
                    response.risk_level
                )
            )
            {
                throw new InvalidOperationException(
                    "The API response is missing risk_level."
                );
            }

            AssessmentReceived?.Invoke(
                response
            );
        }
        catch (Exception exception)
        {
            string errorMessage =
                "[RipFinder AI] Could not parse " +
                "the assessment response: " +
                exception.Message;

            Debug.LogError(
                errorMessage
            );

            AssessmentFailed?.Invoke(
                errorMessage
            );
        }
    }
}