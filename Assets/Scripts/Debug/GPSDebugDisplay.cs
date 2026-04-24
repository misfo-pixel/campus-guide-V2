using TMPro;
using UnityEngine;

/// <summary>
/// Debug display for GPS status. Shows current location in VR.
/// Attach to a TextMeshPro UI element.
/// </summary>
public class GPSDebugDisplay : MonoBehaviour
{
    [Header("Display")]
    [SerializeField] private TMP_Text debugText;
    [SerializeField] private float updateInterval = 2f;
    [SerializeField] private bool showInBuild = true;

    private float lastUpdateTime;

    private void Start()
    {
        if (debugText == null)
        {
            debugText = GetComponent<TMP_Text>();
        }

        UpdateDisplay();
    }

    private void Update()
    {
        if (Time.time - lastUpdateTime > updateInterval)
        {
            UpdateDisplay();
            lastUpdateTime = Time.time;
        }
    }

    private void UpdateDisplay()
    {
        if (debugText == null) return;
        if (!showInBuild && !Application.isEditor) return;

        string status = "=== GPS DEBUG ===\n";

        // GPS Service Status
        GPSLocationService gps = GPSLocationService.Instance;
        if (gps != null)
        {
            status += $"GPS Active: {gps.IsRunning}\n";
            status += $"Has Location: {gps.HasLocation}\n";
            
            if (gps.HasLocation && gps.CurrentLocation != null)
            {
                status += $"Source: {gps.CurrentLocation.source}\n";
                status += $"Area: {gps.CurrentLocation.campusArea}\n";
                status += $"Building: {gps.CurrentLocation.buildingHint}\n";
                status += $"Lat: {gps.CurrentLocation.latitude:F4}\n";
                status += $"Lon: {gps.CurrentLocation.longitude:F4}\n";
            }
            else
            {
                status += "Waiting for location...\n";
            }
        }
        else
        {
            status += "GPSLocationService: NOT FOUND\n";
        }

        // User Data Status
        UserLocationDataManager userData = UserLocationDataManager.Instance;
        if (userData != null && userData.CurrentRecord != null)
        {
            status += "\n=== USER DATA ===\n";
            status += $"Building: {userData.CurrentRecord.currentBuilding}\n";
            status += $"Room: {userData.CurrentRecord.currentRoom}\n";
            status += $"Campus: {userData.CurrentRecord.currentCampusArea}\n";
        }

        debugText.text = status;
    }

    /// <summary>
    /// Call this to force an immediate update
    /// </summary>
    public void ForceUpdate()
    {
        UpdateDisplay();
    }
}
