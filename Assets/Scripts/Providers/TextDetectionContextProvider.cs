using UnityEngine;

/// <summary>
/// Silently extracts room/text info from OCR results and stores it in context.
/// Shows detected text on the TextDetectionPanel for visual feedback.
/// Does NOT trigger the LLM or cause speech.
/// </summary>
public class TextDetectionContextProvider : MonoBehaviour
{
    [Header("Pipeline References")]
    [SerializeField] private DemoContextProvider demoContextProvider;
    [SerializeField] private CameraFrameCapturer cameraFrameCapturer;
    [SerializeField] private TextDetectionPanelController panelController;

    [Header("Detection Settings")]
    [SerializeField] private float minimumConfidence = 0.6f;

    private string currentDetectedText;

    private void OnEnable()
    {
        if (panelController == null)
            panelController = FindFirstObjectByType<TextDetectionPanelController>();
        if (demoContextProvider == null)
            demoContextProvider = FindFirstObjectByType<DemoContextProvider>();

        if (cameraFrameCapturer != null)
        {
            cameraFrameCapturer.OnTextExtracted += OnTextDetected;
            cameraFrameCapturer.OnExtractionFailed += OnExtractionFailed;
        }
    }

    private void OnDisable()
    {
        if (cameraFrameCapturer != null)
        {
            cameraFrameCapturer.OnTextExtracted -= OnTextDetected;
            cameraFrameCapturer.OnExtractionFailed -= OnExtractionFailed;
        }
    }

    public void OnTextDetected(OCRResult result)
    {
        if (result == null || result.confidence < minimumConfidence || string.IsNullOrEmpty(result.extractedText))
        {
            Debug.Log("[TextDetectionContextProvider] No usable text detected.");
            if (panelController != null)
                panelController.ShowNoTextFeedback();
            return;
        }

        currentDetectedText = result.extractedText;
        Debug.Log($"[TextDetectionContextProvider] Detected text: '{currentDetectedText.Substring(0, Mathf.Min(currentDetectedText.Length, 80))}'");

        // Show detected text on the panel for visual feedback
        if (panelController != null)
            panelController.ShowDetectedText(result);

        // Update context so it's available for the next LLM call (voice or welcome)
        if (demoContextProvider != null)
            demoContextProvider.SetDetection("Detected Text from Board/Sign", currentDetectedText);

        // Update location with room info from text
        UserLocationDataManager userData = FindFirstObjectByType<UserLocationDataManager>();
        if (userData != null)
        {
            string building = "";
            if (GPSLocationService.Instance != null && GPSLocationService.Instance.HasLocation)
                building = GPSLocationService.Instance.CurrentLocation.buildingHint;

            userData.UpdateRoomLocation(currentDetectedText, building);
        }
    }

    private void OnExtractionFailed(string errorMessage)
    {
        Debug.LogWarning("[TextDetectionContextProvider] Extraction failed: " + errorMessage);
    }

    public void ClearDetection()
    {
        currentDetectedText = null;
    }

    public string GetDetectedTextSummary()
    {
        return currentDetectedText;
    }
}
