using System.Collections;
using UnityEngine;

/// <summary>
/// On app startup: pause gaze, get GPS location, show welcome on panel,
/// trigger LLM for events, then resume gaze after speech finishes.
/// No "Processing" shown. No gaze interference during welcome.
/// </summary>
public class BuildingWelcomeController : MonoBehaviour
{
    [Header("Pipeline References")]
    [SerializeField] private DemoContextProvider demoContextProvider;
    [SerializeField] private LLMDemoRunner llmDemoRunner;
    [SerializeField] private OfficialCampusFeedProvider officialFeedProvider;
    [SerializeField] private WorldInfoPanelController worldInfoPanelController;

    [Header("Settings")]
    [SerializeField] private float maxWaitForLocationSeconds = 5f;
    [SerializeField] private float maxWaitForFeedSeconds = 8f;

    private bool hasWelcomed = false;

    private void Start()
    {
        // Auto-find references if not wired in Inspector
        if (demoContextProvider == null)
            demoContextProvider = FindFirstObjectByType<DemoContextProvider>();
        if (llmDemoRunner == null)
            llmDemoRunner = FindFirstObjectByType<LLMDemoRunner>();
        if (officialFeedProvider == null)
            officialFeedProvider = FindFirstObjectByType<OfficialCampusFeedProvider>();
        if (worldInfoPanelController == null)
            worldInfoPanelController = FindFirstObjectByType<WorldInfoPanelController>();

        StartCoroutine(WelcomeSequence());
    }

    private IEnumerator WelcomeSequence()
    {
        // Step 1: Wait for GPS
        float waited = 0f;
        while (waited < maxWaitForLocationSeconds)
        {
            if (GPSLocationService.Instance != null && GPSLocationService.Instance.HasLocation)
                break;
            waited += Time.deltaTime;
            yield return null;
        }

        string building = "this building";
        string campusArea = "UMN Campus";

        if (GPSLocationService.Instance != null && GPSLocationService.Instance.HasLocation)
        {
            building = GPSLocationService.Instance.CurrentLocation.buildingHint;
            campusArea = GPSLocationService.Instance.CurrentLocation.campusArea;
        }
        else
        {
            UserLocationDataManager userData = FindFirstObjectByType<UserLocationDataManager>();
            if (userData != null && userData.CurrentRecord != null && !string.IsNullOrEmpty(userData.CurrentRecord.currentBuilding))
            {
                building = userData.CurrentRecord.currentBuilding;
                campusArea = userData.CurrentRecord.currentCampusArea;
            }
        }

        Debug.Log($"[BuildingWelcomeController] Location: {building} / {campusArea}");

        // Step 2: Show welcome on panel immediately — no "Processing"
        if (worldInfoPanelController != null)
        {
            SpriteStateData state = new SpriteStateData
            {
                Mode = SpriteMode.Greeting,
                Title = $"Welcome to {building}!",
                Body = "Getting campus info...",
                ShowPanel = true
            };
            worldInfoPanelController.ApplyState(state);
        }

        // Step 3: Wait for feed
        waited = 0f;
        while (waited < maxWaitForFeedSeconds)
        {
            if (officialFeedProvider == null || officialFeedProvider.HasLoaded)
                break;
            waited += Time.deltaTime;
            yield return null;
        }

        // Step 4: Trigger LLM
        if (hasWelcomed) yield break;
        hasWelcomed = true;

        if (demoContextProvider == null || llmDemoRunner == null)
        {
            ResumeGaze();
            yield break;
        }

        demoContextProvider.SetDetection("Building Entry", $"User just opened the app at {building} in {campusArea}.");
        demoContextProvider.SetTransientUserQueryOverride(
            $"I just arrived at {building}. Welcome me and tell me: " +
            "1) What events are happening at UMN today? " +
            "2) Do I have any classes scheduled in this building? " +
            "Keep it short and friendly — this is a spoken greeting."
        );

        Debug.Log($"[BuildingWelcomeController] Triggering welcome LLM for {building}");
        llmDemoRunner.RunDemo();

        // Gaze will be resumed by LLMDemoRunner after speech finishes
        // (via ResumeGazeWhenSpeechDone coroutine in TrySpeakReply)
    }

    private void ResumeGaze()
    {
        GazeRaycastController gaze = FindFirstObjectByType<GazeRaycastController>();
        if (gaze != null)
            gaze.SetEnabled(true);
    }
}
