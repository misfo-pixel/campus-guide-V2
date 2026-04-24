using System;
using UnityEngine;

/// <summary>
/// Backward-compatible location facade used by legacy scripts.
/// Keeps the old API surface while sourcing building/campus data from GPSLocationService.
/// </summary>
public class UserLocationDataManager : MonoBehaviour
{
    [Serializable]
    public class LocationRecord
    {
        public string currentBuilding = "";
        public string currentRoom = "";
        public string currentCampusArea = "";
        public string source = "";
    }

    public static UserLocationDataManager Instance { get; private set; }

    private LocationRecord currentRecord = new LocationRecord();

    public LocationRecord CurrentRecord => currentRecord;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureRecordInitialized();
    }

    private void OnEnable()
    {
        if (GPSLocationService.Instance != null)
        {
            GPSLocationService.Instance.OnLocationUpdated += OnLocationUpdated;
            PullFromGps(GPSLocationService.Instance.CurrentLocation);
        }
    }

    private void OnDisable()
    {
        if (GPSLocationService.Instance != null)
        {
            GPSLocationService.Instance.OnLocationUpdated -= OnLocationUpdated;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public string GetLocationSummary()
    {
        EnsureRecordInitialized();

        if (string.IsNullOrWhiteSpace(currentRecord.currentCampusArea) &&
            string.IsNullOrWhiteSpace(currentRecord.currentBuilding))
        {
            return "Location unknown";
        }

        if (string.IsNullOrWhiteSpace(currentRecord.currentBuilding))
        {
            return currentRecord.currentCampusArea;
        }

        if (string.IsNullOrWhiteSpace(currentRecord.currentCampusArea))
        {
            return currentRecord.currentBuilding;
        }

        return currentRecord.currentCampusArea + " / " + currentRecord.currentBuilding;
    }

    public void UpdateRoomLocation(string room, string building = null)
    {
        EnsureRecordInitialized();

        if (!string.IsNullOrWhiteSpace(room))
        {
            currentRecord.currentRoom = room.Trim();
        }

        if (!string.IsNullOrWhiteSpace(building))
        {
            currentRecord.currentBuilding = building.Trim();
        }

        if (string.IsNullOrWhiteSpace(currentRecord.currentCampusArea))
        {
            currentRecord.currentCampusArea = "UMN Campus";
        }
    }

    private void OnLocationUpdated(LocationData locationData)
    {
        PullFromGps(locationData);
    }

    private void PullFromGps(LocationData locationData)
    {
        EnsureRecordInitialized();
        if (locationData == null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(locationData.buildingHint))
        {
            currentRecord.currentBuilding = locationData.buildingHint;
        }

        if (!string.IsNullOrWhiteSpace(locationData.campusArea))
        {
            currentRecord.currentCampusArea = locationData.campusArea;
        }

        if (!string.IsNullOrWhiteSpace(locationData.source))
        {
            currentRecord.source = locationData.source;
        }
    }

    private void EnsureRecordInitialized()
    {
        if (currentRecord == null)
        {
            currentRecord = new LocationRecord();
        }
    }
}
