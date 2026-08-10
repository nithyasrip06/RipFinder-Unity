using UnityEngine;

public sealed class RipFinderAssessmentCoordinator : MonoBehaviour
{
    [SerializeField]
    private RipFinderAgentClient agentClient;

    [SerializeField]
    private string beach = "Pismo Beach";

    [SerializeField]
    [TextArea]
    private string forecast = "";

    [SerializeField]
    private float cooldownSeconds = 15f;

    [SerializeField]
    private float confidenceChangeThreshold = 0.10f;

    private float lastRequestTime = float.NegativeInfinity;

    private bool hasPreviousData = false;
    private bool previousRipDetected = false;
    private int previousDetectionCount = 0;
    private float previousConfidence = 0f;

    public void ReceiveDetectionData(
        bool ripDetected,
        float highestConfidence,
        int detectionCount)
    {
        if (!ripDetected)
        {
            highestConfidence = 0f;
            detectionCount = 0;
        }

        bool stateChanged =
            !hasPreviousData ||
            ripDetected != previousRipDetected;

        bool countChanged =
            !hasPreviousData ||
            detectionCount != previousDetectionCount;

        bool confidenceChanged =
            !hasPreviousData ||
            Mathf.Abs(
                highestConfidence - previousConfidence)
            >= confidenceChangeThreshold;

        bool cooldownFinished =
            Time.time - lastRequestTime >= cooldownSeconds;

        bool shouldRequest =
            stateChanged ||
            countChanged ||
            confidenceChanged;

        if (shouldRequest &&
            cooldownFinished &&
            !agentClient.IsRequestRunning)
        {
            Debug.Log(
                $"Sending real detection data: " +
                $"rip={ripDetected}, " +
                $"confidence={highestConfidence:F2}, " +
                $"count={detectionCount}");

            agentClient.RequestAssessment(
                beach,
                ripDetected,
                highestConfidence,
                detectionCount,
                forecast);

            lastRequestTime = Time.time;
        }

        previousRipDetected = ripDetected;
        previousDetectionCount = detectionCount;
        previousConfidence = highestConfidence;
        hasPreviousData = true;
    }

    public void SetForecast(string newForecast)
    {
        forecast = newForecast ?? "";
    }
}