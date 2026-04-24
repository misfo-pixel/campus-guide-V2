using UnityEngine;

public class DemoContextProvider : MonoBehaviour
{
    [Header("Debug User Query")]
    [SerializeField] private VoiceTranscriptProvider voiceTranscriptProvider;
    [SerializeField][TextArea] private string fallbackUserQuery = "Where is my next class?";

    private string transientUserQueryOverride;

    [Header("Mock Location (Fallback)")]
    [SerializeField] private string campusArea = "UMN East Bank";
    [SerializeField] private string buildingHint = "Keller Hall";

    [Header("Mock Campus Info")]
    [SerializeField] private LocalJsonDataProvider localJsonDataProvider;
    [SerializeField] private OfficialCampusFeedProvider officialCampusFeedProvider;
    [SerializeField] private string roomName = "3-180";
    [SerializeField] private string eventTitle = "CSCI Lecture";
    [SerializeField] private string eventTime = "2:00 PM";
    [SerializeField][TextArea] private string campusNote = "This room is often used for computer science classes.";

    [Header("Detection / Scene Context")]
    [SerializeField] private string detectionTitle = "Keller Hall";
    [SerializeField][TextArea] private string detectionDescription = "You are in the main Keller Hall corridor near several classrooms.";

    public void SetDetection(string title, string description)
    {
        detectionTitle = title;
        detectionDescription = description;
        Debug.Log("[DemoContextProvider] Detection updated: " + title);
    }

    public void SetTransientUserQueryOverride(string query)
    {
        transientUserQueryOverride = query;
    }

    public bool IsOfficialFeedReady()
    {
        return officialCampusFeedProvider == null || officialCampusFeedProvider.HasLoaded;
    }

    /// <summary>
    /// Get the best available location string (GPS > MockLocation)
    /// </summary>
    private string GetLocationString()
    {
        // Try UserLocationDataManager first (has GPS + room data)
        UserLocationDataManager userData = FindFirstObjectByType<UserLocationDataManager>();
        if (userData != null && userData.CurrentRecord != null)
        {
            string summary = userData.GetLocationSummary();
            if (!string.IsNullOrEmpty(summary) && summary != "Location unknown")
            {
                return summary;
            }
        }

        // Try GPSLocationService
        GPSLocationService gps = GPSLocationService.Instance;
        if (gps != null && gps.HasLocation)
        {
            return gps.GetLocationSummary();
        }

        // Fall back to serialized mock values
        return campusArea + " / " + buildingHint;
    }

    /// <summary>
    /// Get the building hint for campus info lookup
    /// </summary>
    private string GetBuildingHintForLookup()
    {
        // Try UserLocationDataManager first
        UserLocationDataManager userData = FindFirstObjectByType<UserLocationDataManager>();
        if (userData != null && userData.CurrentRecord != null)
        {
            if (!string.IsNullOrEmpty(userData.CurrentRecord.currentBuilding))
            {
                return userData.CurrentRecord.currentBuilding;
            }
        }

        // Try GPSLocationService
        GPSLocationService gps = GPSLocationService.Instance;
        if (gps != null && gps.HasLocation && gps.CurrentLocation != null)
        {
            return gps.CurrentLocation.buildingHint;
        }

        return buildingHint;
    }

    public string BuildPrompt()
    {
        string query = fallbackUserQuery;

        if (!string.IsNullOrWhiteSpace(transientUserQueryOverride))
        {
            query = transientUserQueryOverride;
            transientUserQueryOverride = null;
        }

        if (voiceTranscriptProvider != null)
        {
            string transcript = voiceTranscriptProvider.GetTranscript();

            if (!string.IsNullOrWhiteSpace(transcript))
            {
                query = transcript;
            }
        }

        string currentBuildingHint = GetBuildingHintForLookup();
        string buildingSummary = localJsonDataProvider != null
            ? localJsonDataProvider.GetBuildingSummary(currentBuildingHint)
            : "No building info.";

        string nextClassSummary = localJsonDataProvider != null
            ? localJsonDataProvider.GetNextClassSummary()
            : "No class info.";

        string taskSummary = localJsonDataProvider != null
            ? localJsonDataProvider.GetUpcomingTaskSummary()
            : "No task info.";

        string eventsSummary = officialCampusFeedProvider != null
            ? officialCampusFeedProvider.GetEventsSummary()
            : "No official campus events.";

        Debug.LogWarning("[DemoContextProvider] Official Events Summary = " + eventsSummary);

        return
            "User Query: " + query + "\n" +
            "Location: " + GetLocationString() + "\n" +
            "Detection Title: " + detectionTitle + "\n" +
            "Detection Description: " + detectionDescription + "\n" +
            "Campus Info: " + eventTitle + " at " + eventTime + " in room " + roomName + ". " + campusNote +
            "Building Info: " + buildingSummary + "\n" +
            "Next Class: " + nextClassSummary + "\n" +
            "Upcoming Task: " + taskSummary + "\n" +
            "Official Events: " + eventsSummary;
    }
}
