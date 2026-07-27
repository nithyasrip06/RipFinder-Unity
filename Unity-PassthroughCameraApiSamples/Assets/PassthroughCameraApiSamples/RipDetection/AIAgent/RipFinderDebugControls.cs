using UnityEngine;

public sealed class RipFinderDebugControls : MonoBehaviour
{
    [SerializeField]
    private RipFinderAgentClient agentClient;

    public void TestLowRisk()
    {
        agentClient.RequestAssessment(
            beach: "Pismo Beach",
            ripDetected: false,
            confidence: 0f,
            detectionCount: 0,
            forecast: "");
    }

    public void TestModerateRisk()
    {
        agentClient.RequestAssessment(
            beach: "Pismo Beach",
            ripDetected: true,
            confidence: 0.62f,
            detectionCount: 1,
            forecast: "Rip currents are possible.");
    }

    public void TestHighRisk()
    {
        agentClient.RequestAssessment(
            beach: "Pismo Beach",
            ripDetected: true,
            confidence: 0.91f,
            detectionCount: 2,
            forecast:
                "High rip current risk through this evening.");
    }
}