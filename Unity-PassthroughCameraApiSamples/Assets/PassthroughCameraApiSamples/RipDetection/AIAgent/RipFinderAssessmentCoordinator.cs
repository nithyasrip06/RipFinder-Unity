using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
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

    [Header("Detection persistence")]
    [Tooltip("Number of most recent inference frames considered.")]
    [SerializeField]
    [Min(1)]
    private int persistenceWindow = 5;

    [Tooltip("How many of those frames must contain a rip before it counts as detected.")]
    [SerializeField]
    [Min(1)]
    private int requiredRipFrames = 3;

    private struct DetectionSample
    {
        public bool Rip;
        public float Confidence;
        public int Count;
    }

    private readonly Queue<DetectionSample> recentSamples = new();

    // True while live detection has a rip in enough recent frames.
    // Single still images (test images, mock) never set this.
    public bool RipPersisting { get; private set; }

    private float lastRequestTime = float.NegativeInfinity;

    // While Time.time is below this, live detections do not trigger
    // requests, so a demo-button assessment stays on the panel.
    private float holdUntilTime = float.NegativeInfinity;

    // When set, the next sample (a mock detection or a still test image)
    // is sent right away, bypassing persistence and the cooldown.
    private bool sendNextImmediately = false;

    // Last state actually sent to the backend. Changes are measured against
    // this, so a change that happens during the cooldown is still sent once
    // the cooldown ends.
    private bool hasSentData = false;
    private bool sentRipDetected = false;
    private int sentDetectionCount = 0;
    private float sentConfidence = 0f;

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

        if (sendNextImmediately)
        {
            sendNextImmediately = false;
            recentSamples.Clear();
            StartCoroutine(SendWhenClientFree(
                ripDetected, highestConfidence, detectionCount));
            return;
        }

        recentSamples.Enqueue(new DetectionSample
        {
            Rip = ripDetected,
            Confidence = highestConfidence,
            Count = detectionCount,
        });

        while (recentSamples.Count > persistenceWindow)
        {
            recentSamples.Dequeue();
        }

        if (recentSamples.Count < persistenceWindow)
        {
            return;
        }

        SmoothRecentSamples(
            out bool smoothedRip,
            out float smoothedConfidence,
            out int smoothedCount);

        RipPersisting = smoothedRip;

        bool cooldownFinished =
            Time.time - lastRequestTime >= cooldownSeconds;

        bool holdFinished =
            Time.time >= holdUntilTime;

        if (HasChangedSinceLastSend(smoothedRip, smoothedConfidence, smoothedCount) &&
            cooldownFinished &&
            holdFinished &&
            !agentClient.IsRequestRunning)
        {
            Send(smoothedRip, smoothedConfidence, smoothedCount);
        }
    }

    // A rip counts as present only if it appears in enough recent frames;
    // confidence and count are then averaged over those rip frames.
    private void SmoothRecentSamples(
        out bool ripDetected,
        out float confidence,
        out int count)
    {
        int ripFrames = 0;
        float confidenceSum = 0f;
        int countSum = 0;

        foreach (DetectionSample sample in recentSamples)
        {
            if (!sample.Rip) continue;
            ripFrames++;
            confidenceSum += sample.Confidence;
            countSum += sample.Count;
        }

        ripDetected = ripFrames >= requiredRipFrames;
        confidence = ripDetected ? confidenceSum / ripFrames : 0f;
        count = ripDetected ? Mathf.Max(1, Mathf.RoundToInt((float)countSum / ripFrames)) : 0;
    }

    private bool HasChangedSinceLastSend(
        bool ripDetected,
        float confidence,
        int count)
    {
        return
            !hasSentData ||
            ripDetected != sentRipDetected ||
            count != sentDetectionCount ||
            Mathf.Abs(confidence - sentConfidence) >= confidenceChangeThreshold;
    }

    private IEnumerator SendWhenClientFree(
        bool ripDetected,
        float confidence,
        int count)
    {
        while (agentClient.IsRequestRunning)
        {
            yield return null;
        }

        Send(ripDetected, confidence, count);
    }

    private void Send(
        bool ripDetected,
        float confidence,
        int count)
    {
        Debug.Log(
            $"Sending real detection data: " +
            $"rip={ripDetected}, " +
            $"confidence={confidence:F2}, " +
            $"count={count}");

        agentClient.RequestAssessment(
            beach,
            ripDetected,
            confidence,
            count,
            forecast);

        lastRequestTime = Time.time;
        hasSentData = true;
        sentRipDetected = ripDetected;
        sentDetectionCount = count;
        sentConfidence = confidence;
    }

    public string ForecastStatus { get; private set; } =
        "NOAA forecast unavailable";

    public void SetForecast(string newForecast)
    {
        forecast = newForecast ?? "";
        ForecastStatus = BuildForecastStatus(forecast);
    }

    // Short on-screen summary of the forecast the AI is using, from the
    // first (current period) Surf Height and Rip Current Risk lines.
    private static string BuildForecastStatus(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "NOAA forecast unavailable";
        }

        Match surf = Regex.Match(
            text, @"Surf Height\.+\s*(.+?)\.?\s*$", RegexOptions.Multiline);
        Match rip = Regex.Match(
            text, @"Rip Current Risk\*?\.+\s*(\w+)");

        string status = "NOAA:";
        if (surf.Success) status += $" Surf {surf.Groups[1].Value}";
        if (rip.Success) status += $" \u00B7 Rip risk {rip.Groups[1].Value}";
        return surf.Success || rip.Success ? status : "NOAA: forecast loaded";
    }

    // Stops live detections from replacing the current assessment
    // for the given number of seconds.
    public void HoldLiveUpdates(float seconds)
    {
        holdUntilTime = Time.time + seconds;

        // The panel now shows something other than the live state, so send
        // the live state again once the hold ends.
        hasSentData = false;
    }

    // Clears the cooldown, hold, sent state and frame history, so live
    // detection starts fresh (it still has to persist before it is sent).
    public void ResetThrottle()
    {
        lastRequestTime = float.NegativeInfinity;
        holdUntilTime = float.NegativeInfinity;
        hasSentData = false;
        recentSamples.Clear();
        RipPersisting = false;
        sendNextImmediately = false;
    }

    // Sends the next sample at once, skipping persistence and the cooldown.
    // Used for a mock detection or a single still test image.
    public void SendNextSampleImmediately()
    {
        ResetThrottle();
        sendNextImmediately = true;
    }
}