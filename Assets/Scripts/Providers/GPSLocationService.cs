using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Android;

/// <summary>
/// GPS Location Service for Android/Quest devices.
/// Gets rough location (building-level) for context awareness.
/// </summary>
public class GPSLocationService : MonoBehaviour
{
    public static GPSLocationService Instance { get; private set; }

    [Header("Settings")]
    [SerializeField] private float updateIntervalSeconds = 30f;
    [SerializeField] private float desiredAccuracyInMeters = 50f;
    [SerializeField] private float updateDistanceInMeters = 10f;
    [SerializeField] private bool startOnAwake = true;

    [Header("Debug")]
    [SerializeField] private bool useMockLocationInEditor = true;
    [SerializeField] private double mockLatitude = 44.9740;
    [SerializeField] private double mockLongitude = -93.2321;

    public event Action<LocationData> OnLocationUpdated;

    private LocationData currentLocation;
    private bool isRunning = false;
    private Coroutine locationCoroutine;

    public LocationData CurrentLocation => currentLocation;
    public bool IsRunning => isRunning;
    public bool HasLocation => currentLocation != null && !string.IsNullOrEmpty(currentLocation.source);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        currentLocation = new LocationData
        {
            source = "",
            campusArea = "",
            buildingHint = "",
            latitude = 0,
            longitude = 0
        };
    }

    private void Start()
    {
        if (startOnAwake)
        {
            StartLocationService();
        }
    }

    private void OnDestroy()
    {
        StopLocationService();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public void StartLocationService()
    {
        if (isRunning) return;

#if UNITY_EDITOR
        if (useMockLocationInEditor)
        {
            Debug.Log("[GPSLocationService] Using mock location in Editor");
            ApplyMockLocation();
            return;
        }
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        StartCoroutine(RequestPermissionAndStart());
#else
        Debug.Log("[GPSLocationService] GPS not supported on this platform, using mock");
        ApplyMockLocation();
#endif
    }

    public void StopLocationService()
    {
        isRunning = false;

        if (locationCoroutine != null)
        {
            StopCoroutine(locationCoroutine);
            locationCoroutine = null;
        }

        if (Input.location.isEnabledByUser)
        {
            Input.location.Stop();
        }

        Debug.Log("[GPSLocationService] Stopped");
    }

#if UNITY_ANDROID
    private IEnumerator RequestPermissionAndStart()
    {
        // Check if we have fine location permission
        if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            Debug.Log("[GPSLocationService] Requesting fine location permission...");
            Permission.RequestUserPermission(Permission.FineLocation);

            // Wait for user response
            float timeout = 30f;
            float elapsed = 0f;
            while (!Permission.HasUserAuthorizedPermission(Permission.FineLocation) && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!Permission.HasUserAuthorizedPermission(Permission.FineLocation))
            {
                Debug.LogWarning("[GPSLocationService] Fine location permission denied");
                ApplyMockLocation();
                yield break;
            }
        }

        // Also request coarse location as fallback
        if (!Permission.HasUserAuthorizedPermission(Permission.CoarseLocation))
        {
            Permission.RequestUserPermission(Permission.CoarseLocation);
            yield return new WaitForSeconds(0.5f);
        }

        locationCoroutine = StartCoroutine(LocationUpdateCoroutine());
    }
#endif

    private IEnumerator LocationUpdateCoroutine()
    {
        // Check if location service is enabled
        if (!Input.location.isEnabledByUser)
        {
            Debug.LogWarning("[GPSLocationService] Location services disabled by user");
            ApplyMockLocation();
            yield break;
        }

        // Start the location service
        Input.location.Start(desiredAccuracyInMeters, updateDistanceInMeters);
        Debug.Log("[GPSLocationService] Starting location service...");

        // Wait for initialization
        int maxWait = 20;
        while (Input.location.status == LocationServiceStatus.Initializing && maxWait > 0)
        {
            yield return new WaitForSeconds(1);
            maxWait--;
        }

        if (maxWait <= 0)
        {
            Debug.LogWarning("[GPSLocationService] Location service timed out");
            ApplyMockLocation();
            yield break;
        }

        if (Input.location.status == LocationServiceStatus.Failed)
        {
            Debug.LogWarning("[GPSLocationService] Unable to determine device location");
            ApplyMockLocation();
            yield break;
        }

        isRunning = true;
        Debug.Log("[GPSLocationService] Location service running");

        // Continuous update loop
        while (isRunning)
        {
            if (Input.location.status == LocationServiceStatus.Running)
            {
                UpdateLocationFromGPS();
            }

            yield return new WaitForSeconds(updateIntervalSeconds);
        }
    }

    private void UpdateLocationFromGPS()
    {
        LocationInfo info = Input.location.lastData;

        currentLocation.source = "gps";
        currentLocation.latitude = info.latitude;
        currentLocation.longitude = info.longitude;

        // Map GPS to building
        UMNBuildingMapper mapper = FindFirstObjectByType<UMNBuildingMapper>();
        if (mapper != null)
        {
            var buildingInfo = mapper.GetBuildingFromCoordinates(info.latitude, info.longitude);
            currentLocation.campusArea = buildingInfo.campusArea;
            currentLocation.buildingHint = buildingInfo.buildingName;
        }
        else
        {
            // Default to UMN if no mapper
            currentLocation.campusArea = "UMN Campus";
            currentLocation.buildingHint = "Unknown Building";
        }

        Debug.Log($"[GPSLocationService] Updated: {currentLocation.campusArea} / {currentLocation.buildingHint} " +
                  $"(lat: {currentLocation.latitude:F6}, lon: {currentLocation.longitude:F6})");

        OnLocationUpdated?.Invoke(currentLocation);
    }

    private void ApplyMockLocation()
    {
        currentLocation.source = "mock";
        currentLocation.latitude = mockLatitude;
        currentLocation.longitude = mockLongitude;

        // Try to map mock coordinates to building
        UMNBuildingMapper mapper = FindFirstObjectByType<UMNBuildingMapper>();
        if (mapper != null)
        {
            var buildingInfo = mapper.GetBuildingFromCoordinates(mockLatitude, mockLongitude);
            currentLocation.campusArea = buildingInfo.campusArea;
            currentLocation.buildingHint = buildingInfo.buildingName;
        }
        else
        {
            currentLocation.campusArea = "UMN East Bank";
            currentLocation.buildingHint = "Keller Hall";
        }

        Debug.Log($"[GPSLocationService] Mock location: {currentLocation.campusArea} / {currentLocation.buildingHint}");
        OnLocationUpdated?.Invoke(currentLocation);
    }

    /// <summary>
    /// Force refresh the location immediately
    /// </summary>
    public void RefreshLocation()
    {
#if UNITY_EDITOR
        if (useMockLocationInEditor)
        {
            ApplyMockLocation();
            return;
        }
#endif

        if (Input.location.status == LocationServiceStatus.Running)
        {
            UpdateLocationFromGPS();
        }
    }

    /// <summary>
    /// Get a formatted string summary of current location
    /// </summary>
    public string GetLocationSummary()
    {
        if (!HasLocation)
        {
            return "Location unknown";
        }

        return $"{currentLocation.campusArea} / {currentLocation.buildingHint}";
    }
}
