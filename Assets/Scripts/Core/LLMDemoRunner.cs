using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using XRCommonUsages = UnityEngine.XR.CommonUsages;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRInputDeviceCharacteristics = UnityEngine.XR.InputDeviceCharacteristics;
using XRInputDevices = UnityEngine.XR.InputDevices;

public class LLMDemoRunner : MonoBehaviour
{
    private enum SummonButton
    {
        PrimaryAction,
        Trigger,
        SecondaryButton
    }

    [SerializeField] private DemoContextProvider contextProvider;
    [SerializeField] private OpenAIResponseClient openAIClient;
    [SerializeField] private WorldInfoPanelController worldInfoPanelController;
    [SerializeField] private SpriteActionController spriteActionController;
    [Header("Speech Output")]
    [SerializeField] private bool enableReplySpeech = true;
    [SerializeField] private AzureSpeechTTSClient speechTtsClient;
    [SerializeField] private SummonButton summonButton = SummonButton.PrimaryAction;
    [SerializeField] private float officialFeedWaitTimeoutSeconds = 20f;
    [SerializeField] private bool verboseLogging = false;

    /// <summary>Fired when the LLM returns a successful response.</summary>
    public event System.Action<LLMActionResult> OnLLMResponseSuccess;

    /// <summary>Fired when the LLM request fails.</summary>
    public event System.Action<string> OnLLMResponseError;

    private readonly List<XRInputDevice> xrDevices = new List<XRInputDevice>();
    private bool isWaitingForOfficialFeed;
    private bool isRequestInFlight;
    private bool wasXRSummonHeldLastFrame;

    private void Start()
    {
        TryResolveSpeechTtsClient();
        if (verboseLogging)
        {
            Debug.Log("[LLMDemoRunner] Started on: " + gameObject.name);
        }
    }

    private void Update()
    {
        if (DidPressRunKey())
        {
            if (verboseLogging)
            {
                Debug.Log("[LLMDemoRunner] Summon input detected");
            }
            RunDemo();
        }
    }

    private bool DidPressRunKey()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            return true;
        }

        XRInputDevices.GetDevicesWithCharacteristics(
            XRInputDeviceCharacteristics.HeldInHand |
            XRInputDeviceCharacteristics.Controller,
            xrDevices);

        bool xrHeld = false;
        for (int i = 0; i < xrDevices.Count; i++)
        {
            if (WasSummonPressed(xrDevices[i]))
            {
                xrHeld = true;
                break;
            }
        }

        bool pressedThisFrame = xrHeld && !wasXRSummonHeldLastFrame;
        wasXRSummonHeldLastFrame = xrHeld;
        return pressedThisFrame;
    }

    private bool WasSummonPressed(XRInputDevice controller)
    {
        switch (summonButton)
        {
            case SummonButton.Trigger:
                return controller.TryGetFeatureValue(XRCommonUsages.triggerButton, out bool triggerPressed) && triggerPressed;

            case SummonButton.SecondaryButton:
                return controller.TryGetFeatureValue(XRCommonUsages.secondaryButton, out bool secondaryPressed) && secondaryPressed;

            default:
                return controller.TryGetFeatureValue(XRCommonUsages.primaryButton, out bool primaryPressed) && primaryPressed;
        }
    }

    /// <summary>
    /// Resets in-flight flag so a new RunDemo() call can proceed.
    /// Used by VoiceInputController to ensure voice queries go through.
    /// </summary>
    public void CancelCurrentRequest()
    {
        isRequestInFlight = false;
        isWaitingForOfficialFeed = false;
    }

    public void RunDemo()
    {
        if (isRequestInFlight)
        {
            if (verboseLogging)
            {
                Debug.Log("[LLMDemoRunner] Request in flight, skipping duplicate run.");
            }
            return;
        }

        if (contextProvider == null || openAIClient == null || worldInfoPanelController == null || spriteActionController == null)
        {
            Debug.LogWarning("[LLMDemoRunner] Missing references.");
            return;
        }

        if (!contextProvider.IsOfficialFeedReady())
        {
            if (!isWaitingForOfficialFeed)
            {
                Debug.LogWarning("[LLMDemoRunner] Official feed still loading. Waiting to auto-run...");
                StartCoroutine(WaitForOfficialFeedAndRun());
            }

            return;
        }

        RunDemoInternal();
    }

    private System.Collections.IEnumerator WaitForOfficialFeedAndRun()
    {
        isWaitingForOfficialFeed = true;
        float waitedSeconds = 0f;

        while (contextProvider != null && !contextProvider.IsOfficialFeedReady() && waitedSeconds < officialFeedWaitTimeoutSeconds)
        {
            waitedSeconds += Time.deltaTime;
            yield return null;
        }

        isWaitingForOfficialFeed = false;

        if (contextProvider == null)
        {
            yield break;
        }

        if (!contextProvider.IsOfficialFeedReady())
        {
            Debug.LogWarning("[LLMDemoRunner] Official feed did not finish loading in time.");
            yield break;
        }

        Debug.LogWarning("[LLMDemoRunner] Official feed ready. Auto-running now.");
        RunDemoInternal();
    }

    private void RunDemoInternal()
    {
        if (contextProvider == null || openAIClient == null || worldInfoPanelController == null || spriteActionController == null)
        {
            Debug.LogWarning("[LLMDemoRunner] Missing references.");
            return;
        }

        isRequestInFlight = true;
        worldInfoPanelController.ShowProcessingFeedback();

        string fullContext = contextProvider.BuildPrompt();
        if (verboseLogging)
        {
            Debug.Log("[LLMDemoRunner] Prompt:\n" + fullContext);
        }

        openAIClient.RequestResponse(
            fullContext,
            onSuccess: result =>
            {
                isRequestInFlight = false;
                if (verboseLogging)
                {
                    Debug.Log("[LLMDemoRunner] LLM success: " + result.title);
                }

                SpriteStateData state = new SpriteStateData
                {
                    Mode = SpriteMode.Info,
                    Title = result.title,
                    Body = result.body,
                    ShowPanel = true
                };

                worldInfoPanelController.ClearVoiceFeedback();
                worldInfoPanelController.ApplyState(state);
                spriteActionController.PlayAction(result.action);
                TrySpeakReply(result);
                OnLLMResponseSuccess?.Invoke(result);
            },
            onError: error =>
            {
                isRequestInFlight = false;
                Debug.LogError("[LLMDemoRunner] LLM error:\n" + error);
                worldInfoPanelController.ShowErrorFeedback("Assistant request failed. Please try again.");
                OnLLMResponseError?.Invoke(error);
            }
        );
    }

    private void TryResolveSpeechTtsClient()
    {
        if (speechTtsClient != null)
        {
            return;
        }

        speechTtsClient = GetComponent<AzureSpeechTTSClient>();

        if (speechTtsClient == null)
        {
            speechTtsClient = gameObject.AddComponent<AzureSpeechTTSClient>();
        }
    }

    private void TrySpeakReply(LLMActionResult result)
    {
        if (!enableReplySpeech)
        {
            return;
        }

        TryResolveSpeechTtsClient();
        if (speechTtsClient == null)
        {
            Debug.LogWarning("[LLMDemoRunner] Reply speech is enabled, but no AzureSpeechTTSClient is available.");
            return;
        }

        string spokenReply = BuildSpokenReply(result);
        if (string.IsNullOrWhiteSpace(spokenReply))
        {
            return;
        }

        speechTtsClient.SpeakText(
            spokenReply,
            onSuccess: () => Debug.Log("[LLMDemoRunner] Reply speech playback started."),
            onError: error => Debug.LogError("[LLMDemoRunner] Reply speech error:\n" + error));
    }

    private static string BuildSpokenReply(LLMActionResult result)
    {
        if (result == null)
        {
            return null;
        }

        string title = string.IsNullOrWhiteSpace(result.title) ? string.Empty : result.title.Trim();
        string body = string.IsNullOrWhiteSpace(result.body) ? string.Empty : result.body.Trim();

        if (string.IsNullOrEmpty(title))
        {
            return body;
        }

        if (string.IsNullOrEmpty(body))
        {
            return title;
        }

        return title + ". " + body;
    }
}
