using System;

[Serializable]
public sealed class RipFinderAssessmentRequest
{
    public string beach;
    public bool rip_detected;
    public float confidence;
    public int detection_count;
    public string forecast;
}

[Serializable]
public sealed class RipFinderAssessmentResponse
{
    public string risk_level;
    public float risk_score;
    public string summary;
    public string recommendation;
    public string safety_note;
    public string assessment_source;
}