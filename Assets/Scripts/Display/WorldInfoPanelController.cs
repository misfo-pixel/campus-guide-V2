using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WorldInfoPanelController : MonoBehaviour
{
    private enum VoiceFeedbackState
    {
        Idle,
        Listening,
        Processing,
        Error
    }

    [Header("UI References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text bodyText;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private CanvasGroup panelCanvasGroup;

    [Header("Optional")]
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private bool followPlayer = false;
    [SerializeField] private bool faceCamera = true;
    [SerializeField] private float followSmoothing = 10f;

    [Header("Voice Feedback")]
    [SerializeField] private Color idleColor = new Color(0.6320754f, 0.37393963f, 0.09242612f, 0.4627451f);
    [SerializeField] private Color listeningColor = new Color(0.2f, 0.7f, 1f, 0.8f);
    [SerializeField] private Color processingColor = new Color(1f, 0.65f, 0.2f, 0.82f);
    [SerializeField] private Color errorColor = new Color(1f, 0.3f, 0.3f, 0.85f);

    [Header("UI Polish")]
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.2f;
    [SerializeField, Min(0f)] private float pulseAmplitude = 0.08f;
    [SerializeField, Min(0f)] private float pulseSpeed = 3f;
    [SerializeField] private bool animatePanel = true;

    private bool hasFollowOffset;
    private Vector3 followOffsetInYawSpace;
    private SpriteStateData lastState;
    private VoiceFeedbackState voiceFeedbackState;
    private Coroutine fadeRoutine;
    private Vector3 panelBaseScale = Vector3.one;

    private void Awake()
    {
        if (panelRoot != null)
        {
            panelBaseScale = panelRoot.transform.localScale;
        }

        EnsureCanvasGroup();
    }

    public void ApplyState(SpriteStateData state)
    {
        if (state == null)
        {
            Debug.LogWarning("[WorldInfoPanelController] Ignoring null state.");
            return;
        }

        lastState = new SpriteStateData
        {
            Mode = state.Mode,
            Title = state.Title,
            Body = state.Body,
            ShowPanel = state.ShowPanel
        };

        if (titleText != null)
        {
            titleText.text = GetTitleForState(state.Title);
        }

        if (bodyText != null)
        {
            bodyText.text = string.IsNullOrWhiteSpace(state.Body)
                ? "I am here to help with your next step."
                : state.Body.Trim();
        }

        SetPanelVisible(state.ShowPanel, !animatePanel);

        ApplyVoiceFeedbackVisuals();
    }

    private void LateUpdate()
    {
        UpdateVoicePulse();

        if (cameraTransform == null)
        {
            TryResolveCameraTransform();
        }

        if (followPlayer && cameraTransform != null)
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

        if (!faceCamera || cameraTransform == null)
            return;

        Vector3 direction = cameraTransform.position - transform.position;
        direction.y = 0f;

        if (direction.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(direction);
        }
    }

    public void SetCameraTransform(Transform targetCamera)
    {
        cameraTransform = targetCamera;
        hasFollowOffset = false;
    }

    public void ShowListeningFeedback()
    {
        voiceFeedbackState = VoiceFeedbackState.Listening;
        ApplyVoiceFeedbackVisuals();
    }

    public void ShowProcessingFeedback()
    {
        voiceFeedbackState = VoiceFeedbackState.Processing;
        ApplyVoiceFeedbackVisuals();
    }

    public void ShowErrorFeedback(string errorMessage)
    {
        voiceFeedbackState = VoiceFeedbackState.Error;

        if (bodyText != null && !string.IsNullOrWhiteSpace(errorMessage))
        {
            bodyText.text = errorMessage;
        }

        ApplyVoiceFeedbackVisuals();
    }

    public void ClearVoiceFeedback()
    {
        voiceFeedbackState = VoiceFeedbackState.Idle;

        if (lastState != null)
        {
            ApplyState(lastState);
            return;
        }

        ApplyVoiceFeedbackVisuals();
    }

    private void TryResolveCameraTransform()
    {
        if (Camera.main != null)
        {
            SetCameraTransform(Camera.main.transform);
        }
    }

    private void ApplyVoiceFeedbackVisuals()
    {
        EnsureCanvasGroup();

        if (backgroundImage == null && panelRoot != null)
        {
            backgroundImage = panelRoot.GetComponent<Image>();
        }

        if (backgroundImage != null)
        {
            backgroundImage.color = GetBackgroundColorForState(voiceFeedbackState);
        }

        if (titleText != null && lastState != null)
        {
            titleText.text = GetTitleForState(lastState.Title);
        }
    }

    private Color GetBackgroundColorForState(VoiceFeedbackState state)
    {
        return state switch
        {
            VoiceFeedbackState.Listening => listeningColor,
            VoiceFeedbackState.Processing => processingColor,
            VoiceFeedbackState.Error => errorColor,
            _ => idleColor
        };
    }

    private string GetTitleForState(string baseTitle)
    {
        string resolvedTitle = string.IsNullOrWhiteSpace(baseTitle) ? "UMN Sprite" : baseTitle;

        return voiceFeedbackState switch
        {
            VoiceFeedbackState.Listening => resolvedTitle + "  <color=#A8E7FF><size=85%>LISTENING</size></color>",
            VoiceFeedbackState.Processing => resolvedTitle + "  <color=#FFD7A2><size=85%>PROCESSING</size></color>",
            VoiceFeedbackState.Error => resolvedTitle + "  <color=#FFC0C0><size=85%>ERROR</size></color>",
            _ => resolvedTitle
        };
    }

    private static Quaternion GetYawRotation(Vector3 forward)
    {
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

        return Quaternion.LookRotation(forward.normalized, Vector3.up);
    }

    private void UpdateVoicePulse()
    {
        if (panelRoot == null || !panelRoot.activeSelf)
        {
            return;
        }

        if (voiceFeedbackState == VoiceFeedbackState.Listening || voiceFeedbackState == VoiceFeedbackState.Processing)
        {
            float wave = 1f + (Mathf.Sin(Time.time * pulseSpeed) * 0.5f + 0.5f) * pulseAmplitude;
            panelRoot.transform.localScale = panelBaseScale * wave;
        }
        else
        {
            panelRoot.transform.localScale = panelBaseScale;
        }
    }

    private void EnsureCanvasGroup()
    {
        if (panelCanvasGroup == null && panelRoot != null)
        {
            panelCanvasGroup = panelRoot.GetComponent<CanvasGroup>();
        }
    }

    private void SetPanelVisible(bool visible, bool immediate)
    {
        if (panelRoot == null)
        {
            return;
        }

        EnsureCanvasGroup();

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        if (immediate || panelCanvasGroup == null)
        {
            panelRoot.SetActive(visible);
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = visible ? 1f : 0f;
            }
            return;
        }

        if (visible)
        {
            panelRoot.SetActive(true);
        }

        fadeRoutine = StartCoroutine(FadePanel(visible));
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
