using TMPro;
using UnityEngine;

public sealed class RipFinderAssessmentPanel : MonoBehaviour
{
    [SerializeField]
    private RipFinderAgentClient agentClient;

    [SerializeField]
    private TMP_Text statusText;

    [SerializeField]
    private TMP_Text riskLevelText;

    [SerializeField]
    private TMP_Text riskScoreText;

    [SerializeField]
    private TMP_Text summaryText;

    [SerializeField]
    private TMP_Text recommendationText;

    [SerializeField]
    private TMP_Text safetyNoteText;

    private RipFinderAssessmentResponse lastValidResponse;

    private void OnEnable()
    {
        if (agentClient == null)
        {
            Debug.LogError(
                "RipFinderAgentClient has not been assigned.");
            return;
        }

        agentClient.AssessmentReceived += ShowAssessment;
        agentClient.AssessmentFailed += ShowFailure;
    }

    private void OnDisable()
    {
        if (agentClient == null)
        {
            return;
        }

        agentClient.AssessmentReceived -= ShowAssessment;
        agentClient.AssessmentFailed -= ShowFailure;
    }

    private void ShowAssessment(
        RipFinderAssessmentResponse response)
    {
        lastValidResponse = response;

        statusText.text = "Assessment updated";
        riskLevelText.text =
            $"Risk: {response.risk_level}";
        riskScoreText.text =
            $"Score: {response.risk_score:0.00}";
        summaryText.text = response.summary;
        recommendationText.text = response.recommendation;
        safetyNoteText.text = response.safety_note;
    }

    private void ShowFailure(string error)
    {
        Debug.LogWarning(error);

        if (lastValidResponse != null)
        {
            statusText.text =
                "Refresh failed. Showing last assessment.";
            return;
        }

        statusText.text = "Assessment unavailable";
        riskLevelText.text = "Risk: UNAVAILABLE";
        riskScoreText.text = "";
        summaryText.text =
            "The assessment service could not be reached.";
        recommendationText.text =
            "Follow official beach warnings and lifeguards.";
        safetyNoteText.text = "";
    }
}