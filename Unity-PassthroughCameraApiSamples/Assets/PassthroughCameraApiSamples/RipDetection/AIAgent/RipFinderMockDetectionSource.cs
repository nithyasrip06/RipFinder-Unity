using UnityEngine;

public sealed class RipFinderMockDetectionSource : MonoBehaviour
{
    [SerializeField]
    private RipFinderAssessmentCoordinator assessmentCoordinator;

    public void SendMockDetection()
    {
        Debug.Log("Sending MOCK detection through coordinator.");

        assessmentCoordinator.ReceiveDetectionData(
            true,
            0.87f,
            2
        );
    }
}