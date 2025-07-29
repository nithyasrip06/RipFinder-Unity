using UnityEngine.UI;
using UnityEngine;

public class InferenceStatsUI : MonoBehaviour
{
    public Text statsText;

    public void UpdateStats(float inferenceTimeMs)
    {
        float fps = 1000f / inferenceTimeMs;
        statsText.text = $"Inference Time: {inferenceTimeMs:0.00} ms\nFrame Rate: {fps:0.00} FPS";
    }

    public void ClearStats()
    {
        statsText.text = "";
    }
}