using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public sealed class RipFinderAgentClient : MonoBehaviour
{
    [Header("Connection")]
    [SerializeField]
    private string editorEndpoint =
        "http://127.0.0.1:8000/v1/assess";

    [SerializeField]
    private string headsetEndpoint =
        "https://replace-this-later.example.com/v1/assess";

    [SerializeField]
    [Min(1)]
    private int timeoutSeconds = 10;

    public bool IsRequestRunning { get; private set; }

    public event Action<RipFinderAssessmentResponse>
        AssessmentReceived;

    public event Action<string> AssessmentFailed;

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

    public void RequestAssessment(
        string beach,
        bool ripDetected,
        float confidence,
        int detectionCount,
        string forecast)
    {
        if (IsRequestRunning)
        {
            Debug.Log("Assessment request already in progress.");
            return;
        }

        var payload = new RipFinderAssessmentRequest
        {
            beach = string.IsNullOrWhiteSpace(beach)
                ? "Unknown beach"
                : beach,
            rip_detected = ripDetected,
            confidence = ripDetected
                ? Mathf.Clamp01(confidence)
                : 0f,
            detection_count = ripDetected
                ? Mathf.Max(1, detectionCount)
                : 0,
            forecast = forecast ?? string.Empty
        };

        StartCoroutine(SendAssessment(payload));
    }

    private IEnumerator SendAssessment(
        RipFinderAssessmentRequest payload)
    {
        IsRequestRunning = true;

        string json = JsonUtility.ToJson(payload);
        Debug.Log($"Sending assessment request: {json}");

        byte[] body = Encoding.UTF8.GetBytes(json);

        using var request = new UnityWebRequest(
            Endpoint,
            UnityWebRequest.kHttpVerbPOST);

        request.uploadHandler = new UploadHandlerRaw(body);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader(
            "Content-Type",
            "application/json");

        request.timeout = timeoutSeconds;

        yield return request.SendWebRequest();

        IsRequestRunning = false;

        if (request.result != UnityWebRequest.Result.Success)
        {
            string errorMessage =
                $"Assessment failed. HTTP {request.responseCode}: " +
                $"{request.error}\n" +
                $"{request.downloadHandler.text}";

            Debug.LogWarning(errorMessage);
            AssessmentFailed?.Invoke(errorMessage);
            yield break;
        }

        try
        {
            string responseJson = request.downloadHandler.text;

            Debug.Log(
                $"Assessment response received: {responseJson}");

            var response =
                JsonUtility.FromJson<RipFinderAssessmentResponse>(
                    responseJson);

            if (response == null ||
                string.IsNullOrWhiteSpace(response.risk_level))
            {
                throw new InvalidOperationException(
                    "The API response is missing risk_level.");
            }

            AssessmentReceived?.Invoke(response);
        }
        catch (Exception exception)
        {
            string errorMessage =
                "Could not parse the assessment response: " +
                exception.Message;

            Debug.LogError(errorMessage);
            AssessmentFailed?.Invoke(errorMessage);
        }
    }
}