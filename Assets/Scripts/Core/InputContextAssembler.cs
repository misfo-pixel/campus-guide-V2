using UnityEngine;

public class InputContextAssembler : MonoBehaviour
{
    [SerializeField] private MockLocationProvider locationProvider;
    [SerializeField] private CampusInfoDataBase campusInfoDatabase;
    [SerializeField] private UserQueryInput userQueryInput;

    private string currentZoneTitle = "Unknown Zone";
    private string currentZoneDescription = "No detection yet.";

    public void SetCurrentZone(string zoneTitle, string zoneDescription)
    {
        currentZoneTitle = zoneTitle;
        currentZoneDescription = zoneDescription;
    }

    public string BuildFullContext()
    {
        string userQuery = userQueryInput != null ? userQueryInput.GetCurrentQuery() : "No query";
        
        // Try GPS location first, fall back to mock
        string location = GetBestLocationSummary();
        string buildingHint = GetBestBuildingHint();
        string campusInfo = campusInfoDatabase != null ? campusInfoDatabase.GetCampusInfoSummary(buildingHint) : "No campus info";

        string fullContext =
            "User Query: " + userQuery + "\n" +
            "Location: " + location + "\n" +
            "Detection Title: " + currentZoneTitle + "\n" +
            "Detection Description: " + currentZoneDescription + "\n" +
            "Campus Info: " + campusInfo;

        return fullContext;
    }

    private string GetBestLocationSummary()
    {
        // Try UserLocationDataManager first (has both GPS and room data)
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

        // Fall back to mock provider
        if (locationProvider != null)
        {
            return locationProvider.GetLocationSummary();
        }

        return "No location";
    }

    private string GetBestBuildingHint()
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

        // Fall back to mock provider
        if (locationProvider != null)
        {
            return locationProvider.GetBuildingHint();
        }

        return "";
    }

    public void PrintFullContext()
    {
        Debug.Log("[InputContextAssembler] Full Context:\n" + BuildFullContext());
    }
}
