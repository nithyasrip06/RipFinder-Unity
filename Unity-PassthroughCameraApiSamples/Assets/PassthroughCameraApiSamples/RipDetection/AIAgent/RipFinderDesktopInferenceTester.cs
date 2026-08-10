using UnityEngine;
using PassthroughCameraSamples.MultiObjectDetection;

public sealed class RipFinderDesktopInferenceTester : MonoBehaviour
{
    [Header("Sentis")]
    [SerializeField]
    private SentisInferenceRunManager inferenceManager;

    [Header("Test Image")]
    [SerializeField]
    private Texture2D testImage;

    public void RunTestImage()
    {
        if (inferenceManager == null)
        {
            Debug.LogError(
                "[Desktop Test] SentisInferenceRunManager is not assigned."
            );

            return;
        }

        if (testImage == null)
        {
            Debug.LogError(
                "[Desktop Test] No test image has been assigned."
            );

            return;
        }

        if (!inferenceManager.IsModelLoaded)
        {
            Debug.LogWarning(
                "[Desktop Test] Sentis model is not loaded yet."
            );

            return;
        }

        if (inferenceManager.IsRunning())
        {
            Debug.LogWarning(
                "[Desktop Test] Inference is already running."
            );

            return;
        }

        Debug.Log(
            $"[Desktop Test] Running inference on " +
            $"{testImage.name} ({testImage.width}x{testImage.height})"
        );

        inferenceManager.RunInference(testImage);
    }
}