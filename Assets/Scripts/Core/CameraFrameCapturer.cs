using System;
using System.Collections;
using Meta.XR;
using Unity.Collections;
using UnityEngine;

/// <summary>
/// Captures a frame from the Meta Quest passthrough camera using the PassthroughCameraAccess API
/// when gaze dwell completes, crops around the gaze point, sends to OCR, and forwards results.
/// </summary>
public class CameraFrameCapturer : MonoBehaviour
{
    [Header("Pipeline References")]
    [SerializeField] private GazeRaycastController gazeController;
    [SerializeField] private OCRProviderBase ocrProvider;
    [SerializeField] private TextDetectionPanelController panelController;

    [Header("Camera")]
    [SerializeField] private PassthroughCameraAccess passthroughCamera;

    [Header("Capture Settings")]
    [SerializeField] [Range(0.2f, 1.0f)] private float roiScale = 0.5f;

    [Header("UI to hide during capture")]
    [Tooltip("Panels to temporarily hide so they don't block the camera view")]
    [SerializeField] private GameObject[] hideDuringCapture;

    public event Action<OCRResult> OnTextExtracted;
    public event Action<string> OnExtractionFailed;
    public bool IsCapturing { get; private set; }

    private Texture2D captureTexture;
    private float captureStartTime;
    private const float CAPTURE_TIMEOUT = 30f;

    private void Start()
    {
        if (passthroughCamera == null)
            passthroughCamera = FindObjectOfType<PassthroughCameraAccess>();

        Debug.Log($"[CameraFrameCapturer] Started. PCA={passthroughCamera != null}, ocrProvider={ocrProvider != null}, gazeController={gazeController != null}");
    }

    private void Update()
    {
        if (IsCapturing && Time.time - captureStartTime > CAPTURE_TIMEOUT)
        {
            Debug.LogError("[CameraFrameCapturer] Capture TIMEOUT! Force-resetting.");
            IsCapturing = false;
        }
    }

    private void OnEnable()
    {
        if (gazeController != null)
            gazeController.OnDwellComplete += RequestCapture;
    }

    private void OnDisable()
    {
        if (gazeController != null)
            gazeController.OnDwellComplete -= RequestCapture;
    }

    private void OnDestroy()
    {
        if (captureTexture != null) Destroy(captureTexture);
    }

    public void RequestCapture(GazeHitData gazeHit)
    {
        Debug.Log($"[CameraFrameCapturer] RequestCapture. IsCapturing={IsCapturing}");

        if (IsCapturing)
        {
            Debug.LogWarning("[CameraFrameCapturer] Already capturing.");
            return;
        }

        if (gazeHit == null || !gazeHit.Validate())
        {
            HandleFailure("Invalid gaze hit data.");
            return;
        }

        if (ocrProvider == null || !ocrProvider.IsAvailable)
        {
            HandleFailure("OCR provider not available.");
            return;
        }

        if (passthroughCamera == null)
        {
            HandleFailure("PassthroughCameraAccess not found.");
            return;
        }

        if (!passthroughCamera.IsPlaying)
        {
            HandleFailure("Passthrough camera not playing yet. Wait for permission grant.");
            return;
        }

        IsCapturing = true;
        captureStartTime = Time.time;

        // Don't hide or show processing — just let the panel stay as-is during capture
        StartCoroutine(CaptureFrame(gazeHit));
    }

    public void CancelCapture() => IsCapturing = false;

    private IEnumerator CaptureFrame(GazeHitData gazeHit)
    {
        // Hide UI panels so they don't appear in the camera capture
        bool[] wasActive = HidePanels();

        // Wait for a fresh frame
        float waited = 0f;
        while (!passthroughCamera.IsUpdatedThisFrame && waited < 2f)
        {
            waited += Time.deltaTime;
            yield return null;
        }

        byte[] imageBytes = null;

        try
        {
            // Use PCA's WorldToViewportPoint to map gaze to camera image space
            // This accounts for the camera's actual position, FOV, and lens distortion
            Vector2 gazeViewport = passthroughCamera.WorldToViewportPoint(gazeHit.hitPoint);
            Debug.Log($"[CameraFrameCapturer] Gaze in camera viewport: ({gazeViewport.x:F2}, {gazeViewport.y:F2})");

            // Get the camera frame pixels
            Vector2Int res = passthroughCamera.CurrentResolution;
            Debug.Log($"[CameraFrameCapturer] Camera resolution: {res.x}x{res.y}");

            if (res.x <= 0 || res.y <= 0)
            {
                HandleFailure("Camera resolution is zero.");
                yield break;
            }

            NativeArray<Color32> colors = passthroughCamera.GetColors();
            if (!colors.IsCreated || colors.Length == 0)
            {
                HandleFailure("GetColors returned empty.");
                yield break;
            }

            Debug.Log($"[CameraFrameCapturer] Got {colors.Length} pixels from PCA");

            // Create full frame texture
            Texture2D fullFrame = new Texture2D(res.x, res.y, TextureFormat.RGBA32, false);
            fullFrame.SetPixelData(colors, 0);
            fullFrame.Apply();

            // Crop ROI around gaze point
            float clampedGazeX = Mathf.Clamp01(gazeViewport.x);
            float clampedGazeY = Mathf.Clamp01(gazeViewport.y);

            int roiW = Mathf.Max(64, Mathf.RoundToInt(res.x * roiScale));
            int roiH = Mathf.Max(64, Mathf.RoundToInt(res.y * roiScale));
            int roiX = Mathf.Clamp(Mathf.RoundToInt(clampedGazeX * res.x - roiW * 0.5f), 0, res.x - roiW);
            int roiY = Mathf.Clamp(Mathf.RoundToInt(clampedGazeY * res.y - roiH * 0.5f), 0, res.y - roiH);

            Debug.Log($"[CameraFrameCapturer] ROI: x={roiX}, y={roiY}, w={roiW}, h={roiH}");

            Color[] roiPixels = fullFrame.GetPixels(roiX, roiY, roiW, roiH);
            Destroy(fullFrame);

            if (captureTexture == null || captureTexture.width != roiW || captureTexture.height != roiH)
            {
                if (captureTexture != null) Destroy(captureTexture);
                captureTexture = new Texture2D(roiW, roiH, TextureFormat.RGB24, false);
            }

            captureTexture.SetPixels(roiPixels);
            captureTexture.Apply();

            imageBytes = captureTexture.EncodeToJPG(80);
            Debug.Log($"[CameraFrameCapturer] Encoded ROI to JPG: {imageBytes?.Length ?? 0} bytes");

            // Save debug image
            try
            {
                string path = System.IO.Path.Combine(Application.persistentDataPath, "last_capture.jpg");
                System.IO.File.WriteAllBytes(path, imageBytes);
                Debug.Log($"[CameraFrameCapturer] Debug image saved: {path}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CameraFrameCapturer] Debug save failed: {e.Message}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[CameraFrameCapturer] Capture exception: {ex.Message}\n{ex.StackTrace}");
            HandleFailure($"Capture failed: {ex.Message}");
            yield break;
        }

        if (imageBytes == null || imageBytes.Length == 0)
        {
            HandleFailure("Encoded image is empty.");
            yield break;
        }

        Debug.Log($"[CameraFrameCapturer] Sending {imageBytes.Length} bytes to OCR...");
        RestorePanels(wasActive);
        ocrProvider.ExtractTextAsync(imageBytes, OnOCRSuccess, OnOCRError);
    }

    private bool[] HidePanels()
    {
        if (hideDuringCapture == null || hideDuringCapture.Length == 0)
            return null;

        bool[] wasActive = new bool[hideDuringCapture.Length];
        for (int i = 0; i < hideDuringCapture.Length; i++)
        {
            if (hideDuringCapture[i] != null)
            {
                wasActive[i] = hideDuringCapture[i].activeSelf;
                hideDuringCapture[i].SetActive(false);
            }
        }
        return wasActive;
    }

    private void RestorePanels(bool[] wasActive)
    {
        if (hideDuringCapture == null || wasActive == null)
            return;

        for (int i = 0; i < hideDuringCapture.Length; i++)
        {
            if (hideDuringCapture[i] != null)
                hideDuringCapture[i].SetActive(wasActive[i]);
        }
    }

    private void OnOCRSuccess(OCRResult result)
    {
        IsCapturing = false;
        Debug.Log($"[CameraFrameCapturer] OCR SUCCESS — text='{result?.extractedText?.Substring(0, Mathf.Min(result?.extractedText?.Length ?? 0, 80))}', confidence={result?.confidence:F2}");
        OnTextExtracted?.Invoke(result);
    }

    private void OnOCRError(string errorMessage)
    {
        Debug.LogError($"[CameraFrameCapturer] OCR ERROR: {errorMessage}");
        HandleFailure(errorMessage);
    }

    private void HandleFailure(string errorMessage)
    {
        IsCapturing = false;
        Debug.LogWarning($"[CameraFrameCapturer] {errorMessage}");
        OnExtractionFailed?.Invoke(errorMessage);
        if (panelController != null)
            panelController.ShowErrorFeedback(errorMessage);
    }
}
