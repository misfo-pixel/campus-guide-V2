using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Dedicated UI panel for displaying text detection results.
/// Follows the <see cref="WorldInfoPanelController"/> pattern for billboard rotation,
/// fade animations, and panel structure.
/// </summary>
public class TextDetectionPanelController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text detectedTextField;
    [SerializeField] private TMP_Text llmResponseText;
    [SerializeField] private TMP_Text confidenceText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private CanvasGroup panelCanvasGroup;
    [SerializeField] private GameObject dwellProgressIndicator;

    [Header("Behavior")]
    [SerializeField] private bool followPlayer = true;
    [SerializeField] private bool faceCamera = true;
    [SerializeField] private float followSmoothing = 10f;
    [SerializeField] private float fadeDuration = 0.2f;
    [SerializeField] private float autoHideDelay = 0f;

    private Transform cameraTransform;
    private Coroutine fadeRoutine;
    private Coroutine autoHideRoutine;
    private bool hasFollowOffset;
    private Vector3 followOffsetInYawSpace;

    private void Awake()
    {
        EnsureCanvasGroup();
    }

    /// <summary>
    /// Shows the extracted OCR text on the panel.
    /// Activates the panel, sets detected text and confidence, and fades in.
    /// </summary>
    public void ShowDetectedText(OCRResult result)
    {
        if (result == null)
        {
            Debug.LogWarning("[TextDetectionPanelController] ShowDetectedText — ignoring null OCRResult.");
            return;
        }

        Debug.Log($"[TextDetectionPanelController] ShowDetectedText — text='{result.extractedText?.Substring(0, Mathf.Min(result.extractedText?.Length ?? 0, 60))}', confidence={result.confidence:F2}");

        if (detectedTextField != null)
        {
            detectedTextField.text = result.extractedText ?? string.Empty;
        }
        else
        {
            Debug.LogWarning("[TextDetectionPanelController] detectedTextField is NULL — cannot display text!");
        }

        if (confidenceText != null)
        {
            int percent = Mathf.RoundToInt(result.confidence * 100f);
            confidenceText.text = percent + "%";
        }

        SetPanelVisible(true);
    }

    /// <summary>
    /// Shows the LLM-processed response on the panel.
    /// </summary>
    public void ShowLLMResponse(LLMActionResult result)
    {
        if (result == null)
        {
            Debug.LogWarning("[TextDetectionPanelController] ShowLLMResponse — ignoring null LLMActionResult.");
            return;
        }

        Debug.Log($"[TextDetectionPanelController] ShowLLMResponse — title='{result.title}', body length={result.body?.Length ?? 0}");

        if (llmResponseText != null)
        {
            llmResponseText.text = result.title + "\n" + result.body;
        }
        else
        {
            Debug.LogWarning("[TextDetectionPanelController] llmResponseText is NULL — cannot display LLM response!");
        }
    }

    /// <summary>
    /// Shows a processing state indicator while capture/OCR is in progress.
    /// </summary>
    public void ShowProcessingFeedback()
    {
        Debug.Log("[TextDetectionPanelController] ShowProcessingFeedback — displaying 'Processing...'");

        if (detectedTextField != null)
        {
            detectedTextField.text = "Processing...";
        }

        SetPanelVisible(true);
    }

    /// <summary>
    /// Shows a "No text detected" message.
    /// </summary>
    public void ShowNoTextFeedback()
    {
        Debug.Log("[TextDetectionPanelController] ShowNoTextFeedback — displaying 'No text detected'");

        if (detectedTextField != null)
        {
            detectedTextField.text = "No text detected";
        }

        SetPanelVisible(true);
    }

    /// <summary>
    /// Shows an error message on the panel.
    /// </summary>
    public void ShowErrorFeedback(string message)
    {
        Debug.LogWarning($"[TextDetectionPanelController] ShowErrorFeedback — message='{message}'");

        if (detectedTextField != null)
        {
            detectedTextField.text = !string.IsNullOrWhiteSpace(message) ? message : "An error occurred";
        }

        SetPanelVisible(true);
    }

    /// <summary>
    /// Updates the dwell progress indicator (0–1 normalized).
    /// Uses Image.fillAmount if the indicator has an Image component, otherwise scales it.
    /// </summary>
    public void UpdateDwellProgress(float progress)
    {
        if (dwellProgressIndicator == null)
            return;

        float clamped = Mathf.Clamp01(progress);

        Image fillImage = dwellProgressIndicator.GetComponent<Image>();
        if (fillImage != null)
        {
            fillImage.fillAmount = clamped;
        }
        else
        {
            dwellProgressIndicator.transform.localScale = new Vector3(clamped, clamped, clamped);
        }
    }

    /// <summary>
    /// Sets the camera transform for billboard rotation.
    /// </summary>
    public void SetCameraTransform(Transform cam)
    {
        cameraTransform = cam;
        hasFollowOffset = false;
    }

    /// <summary>
    /// Hides the panel with a fade-out animation.
    /// </summary>
    public void Hide()
    {
        Debug.Log("[TextDetectionPanelController] Hide() called");
        SetPanelVisible(false);
    }

    private void LateUpdate()
    {
        if (cameraTransform == null)
            return;

        if (followPlayer)
        {
            Quaternion yawRotation = GetYawRotation(cameraTransform.forward);

            if (!hasFollowOffset)
            {
                followOffsetInYawSpace = Quaternion.Inverse(yawRotation) * (transform.position - cameraTransform.position);
                hasFollowOffset = true;
            }

            Vector3 targetPosition = cameraTransform.position + yawRotation * followOffsetInYawSpace;
            float t = 1f - Mathf.Exp(-followSmoothing * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPosition, t);
        }

        if (!faceCamera)
            return;

        Vector3 direction = transform.position - cameraTransform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    private static Quaternion GetYawRotation(Vector3 forward)
    {
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;
        return Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    private void EnsureCanvasGroup()
    {
        if (panelCanvasGroup == null && panelRoot != null)
        {
            panelCanvasGroup = panelRoot.GetComponent<CanvasGroup>();
        }
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelRoot == null)
        {
            Debug.LogWarning("[TextDetectionPanelController] panelRoot is NULL — cannot show/hide panel!");
            return;
        }

        Debug.Log($"[TextDetectionPanelController] SetPanelVisible({visible}), panelRoot active={panelRoot.activeSelf}");

        EnsureCanvasGroup();

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        if (autoHideRoutine != null)
        {
            StopCoroutine(autoHideRoutine);
            autoHideRoutine = null;
        }

        if (panelCanvasGroup == null)
        {
            panelRoot.SetActive(visible);
            if (visible && autoHideDelay > 0f)
            {
                autoHideRoutine = StartCoroutine(AutoHideAfterDelay());
            }
            return;
        }

        if (visible)
        {
            panelRoot.SetActive(true);
        }

        fadeRoutine = StartCoroutine(FadePanel(visible));

        if (visible && autoHideDelay > 0f)
        {
            autoHideRoutine = StartCoroutine(AutoHideAfterDelay());
        }
    }

    private IEnumerator AutoHideAfterDelay()
    {
        yield return new WaitForSeconds(autoHideDelay);
        autoHideRoutine = null;
        SetPanelVisible(false);
    }

    private IEnumerator FadePanel(bool visible)
    {
        float startAlpha = panelCanvasGroup.alpha;
        float targetAlpha = visible ? 1f : 0f;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            yield return null;
        }

        panelCanvasGroup.alpha = targetAlpha;

        if (!visible)
        {
            panelRoot.SetActive(false);
        }

        fadeRoutine = null;
    }
}
