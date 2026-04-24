using System;
using UnityEngine;

/// <summary>
/// Maps GPS coordinates to known UMN campus buildings.
/// Uses rough radius matching for building-level detection.
/// </summary>
public class UMNBuildingMapper : MonoBehaviour
{
    [Serializable]
    public struct BuildingInfo
    {
        public string buildingName;
        public string campusArea;
        public double latitude;
        public double longitude;
        public float radiusMeters;
    }

    public struct BuildingResult
    {
        public string buildingName;
        public string campusArea;
        public bool isKnownBuilding;
        public float distanceMeters;
    }

    [Header("Building Database")]
    [SerializeField] private BuildingInfo[] buildings = new BuildingInfo[]
    {
        // East Bank - Science & Engineering
        new BuildingInfo { buildingName = "Keller Hall", campusArea = "UMN East Bank", latitude = 44.9743, longitude = -93.2321, radiusMeters = 50 },
        new BuildingInfo { buildingName = "Walter Library", campusArea = "UMN East Bank", latitude = 44.9748, longitude = -93.2445, radiusMeters = 60 },
        new BuildingInfo { buildingName = "Tate Hall", campusArea = "UMN East Bank", latitude = 44.9738, longitude = -93.2333, radiusMeters = 40 },
        new BuildingInfo { buildingName = "Shepherd Labs", campusArea = "UMN East Bank", latitude = 44.9724, longitude = -93.2323, radiusMeters = 50 },
        new BuildingInfo { buildingName = "Smith Hall", campusArea = "UMN East Bank", latitude = 44.9736, longitude = -93.2350, radiusMeters = 40 },
        new BuildingInfo { buildingName = "Physics & Nanotechnology", campusArea = "UMN East Bank", latitude = 44.9720, longitude = -93.2335, radiusMeters = 50 },
        new BuildingInfo { buildingName = "Mechanical Engineering", campusArea = "UMN East Bank", latitude = 44.9742, longitude = -93.2303, radiusMeters = 50 },
        new BuildingInfo { buildingName = "Akerman Hall", campusArea = "UMN East Bank", latitude = 44.9745, longitude = -93.2295, radiusMeters = 45 },
        new BuildingInfo { buildingName = "Amundson Hall", campusArea = "UMN East Bank", latitude = 44.9748, longitude = -93.2308, radiusMeters = 45 },
        new BuildingInfo { buildingName = "Lind Hall", campusArea = "UMN East Bank", latitude = 44.9736, longitude = -93.2360, radiusMeters = 40 },

        // East Bank - Student Life
        new BuildingInfo { buildingName = "Coffman Memorial Union", campusArea = "UMN East Bank", latitude = 44.9727, longitude = -93.2349, radiusMeters = 80 },
        new BuildingInfo { buildingName = "Bruininks Hall", campusArea = "UMN East Bank", latitude = 44.9778, longitude = -93.2345, radiusMeters = 50 },
        new BuildingInfo { buildingName = "Northrop", campusArea = "UMN East Bank", latitude = 44.9753, longitude = -93.2427, radiusMeters = 60 },
        new BuildingInfo { buildingName = "Morrill Hall", campusArea = "UMN East Bank", latitude = 44.9759, longitude = -93.2430, radiusMeters = 50 },
        new BuildingInfo { buildingName = "Appleby Hall", campusArea = "UMN East Bank", latitude = 44.9771, longitude = -93.2318, radiusMeters = 40 },
        
        // East Bank - Residence Halls
        new BuildingInfo { buildingName = "Comstock Hall", campusArea = "UMN East Bank", latitude = 44.9709, longitude = -93.2433, radiusMeters = 60 },
        new BuildingInfo { buildingName = "Territorial Hall", campusArea = "UMN East Bank", latitude = 44.9725, longitude = -93.2429, radiusMeters = 50 },
        new BuildingInfo { buildingName = "Pioneer Hall", campusArea = "UMN East Bank", latitude = 44.9687, longitude = -93.2346, radiusMeters = 50 },
        new BuildingInfo { buildingName = "Frontier Hall", campusArea = "UMN East Bank", latitude = 44.9685, longitude = -93.2330, radiusMeters = 50 },
        new BuildingInfo { buildingName = "Centennial Hall", campusArea = "UMN East Bank", latitude = 44.9681, longitude = -93.2360, radiusMeters = 55 },
        
        // West Bank
        new BuildingInfo { buildingName = "Carlson School of Management", campusArea = "UMN West Bank", latitude = 44.9695, longitude = -93.2463, radiusMeters = 70 },
        new BuildingInfo { buildingName = "Blegen Hall", campusArea = "UMN West Bank", latitude = 44.9689, longitude = -93.2443, radiusMeters = 50 },
        new BuildingInfo { buildingName = "Anderson Hall", campusArea = "UMN West Bank", latitude = 44.9680, longitude = -93.2438, radiusMeters = 50 },
        new BuildingInfo { buildingName = "Wilson Library", campusArea = "UMN West Bank", latitude = 44.9707, longitude = -93.2469, radiusMeters = 60 },
        new BuildingInfo { buildingName = "Rarig Center", campusArea = "UMN West Bank", latitude = 44.9710, longitude = -93.2445, radiusMeters = 50 },
        new BuildingInfo { buildingName = "Ferguson Hall", campusArea = "UMN West Bank", latitude = 44.9719, longitude = -93.2459, radiusMeters = 50 },
        
        // St. Paul Campus
        new BuildingInfo { buildingName = "Coffey Hall", campusArea = "UMN St. Paul", latitude = 44.9860, longitude = -93.1804, radiusMeters = 60 },
        new BuildingInfo { buildingName = "Borlaug Hall", campusArea = "UMN St. Paul", latitude = 44.9872, longitude = -93.1796, radiusMeters = 60 },
        new BuildingInfo { buildingName = "McNeal Hall", campusArea = "UMN St. Paul", latitude = 44.9847, longitude = -93.1795, radiusMeters = 50 },
        new BuildingInfo { buildingName = "Peters Hall", campusArea = "UMN St. Paul", latitude = 44.9849, longitude = -93.1835, radiusMeters = 40 },
        new BuildingInfo { buildingName = "Biological Sciences Center", campusArea = "UMN St. Paul", latitude = 44.9832, longitude = -93.1801, radiusMeters = 60 },
        
        // Health Sciences
        new BuildingInfo { buildingName = "Moos Tower", campusArea = "UMN Health Sciences", latitude = 44.9711, longitude = -93.2280, radiusMeters = 60 },
        new BuildingInfo { buildingName = "Weaver-Densford Hall", campusArea = "UMN Health Sciences", latitude = 44.9723, longitude = -93.2292, radiusMeters = 50 },
        new BuildingInfo { buildingName = "Mayo Building", campusArea = "UMN Health Sciences", latitude = 44.9706, longitude = -93.2296, radiusMeters = 60 },
        new BuildingInfo { buildingName = "Phillips-Wangensteen", campusArea = "UMN Health Sciences", latitude = 44.9700, longitude = -93.2275, radiusMeters = 60 },
        
        // Stadium Area
        new BuildingInfo { buildingName = "Huntington Bank Stadium", campusArea = "UMN East Bank", latitude = 44.9761, longitude = -93.2245, radiusMeters = 150 },
        new BuildingInfo { buildingName = "Mariucci Arena", campusArea = "UMN East Bank", latitude = 44.9779, longitude = -93.2260, radiusMeters = 70 },
        new BuildingInfo { buildingName = "Williams Arena", campusArea = "UMN East Bank", latitude = 44.9800, longitude = -93.2245, radiusMeters = 80 },
        new BuildingInfo { buildingName = "Ridder Arena", campusArea = "UMN East Bank", latitude = 44.9810, longitude = -93.2270, radiusMeters = 60 },
    };

    [Header("Fallback Settings")]
    [SerializeField] private string defaultCampusArea = "UMN Campus";
    [SerializeField] private string defaultBuilding = "Near Campus";
    [SerializeField] private float campusBoundaryRadius = 1500f; // meters

    // UMN Campus center (roughly)
    private const double CAMPUS_CENTER_LAT = 44.9740;
    private const double CAMPUS_CENTER_LON = -93.2350;

    /// <summary>
    /// Get building information from GPS coordinates
    /// </summary>
    public BuildingResult GetBuildingFromCoordinates(double latitude, double longitude)
    {
        BuildingResult result = new BuildingResult
        {
            buildingName = defaultBuilding,
            campusArea = defaultCampusArea,
            isKnownBuilding = false,
            distanceMeters = float.MaxValue
        };

        // First check if we're near campus at all
        float distanceToCampusCenter = CalculateDistanceMeters(latitude, longitude, CAMPUS_CENTER_LAT, CAMPUS_CENTER_LON);
        if (distanceToCampusCenter > campusBoundaryRadius)
        {
            result.campusArea = "Off Campus";
            result.buildingName = "Not on campus";
            result.distanceMeters = distanceToCampusCenter;
            return result;
        }

        // Find the closest building
        float closestDistance = float.MaxValue;
        int closestIndex = -1;

        for (int i = 0; i < buildings.Length; i++)
        {
            float distance = CalculateDistanceMeters(latitude, longitude, buildings[i].latitude, buildings[i].longitude);
            
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        // Check if we're within the building's radius
        if (closestIndex >= 0 && closestDistance <= buildings[closestIndex].radiusMeters)
        {
            result.buildingName = buildings[closestIndex].buildingName;
            result.campusArea = buildings[closestIndex].campusArea;
            result.isKnownBuilding = true;
            result.distanceMeters = closestDistance;
            
            Debug.Log($"[UMNBuildingMapper] Found building: {result.buildingName} ({result.distanceMeters:F1}m away)");
        }
        else if (closestIndex >= 0)
        {
            // We're on campus but not at a specific building
            result.campusArea = buildings[closestIndex].campusArea;
            result.buildingName = $"Near {buildings[closestIndex].buildingName}";
            result.isKnownBuilding = false;
            result.distanceMeters = closestDistance;
            
            Debug.Log($"[UMNBuildingMapper] Near building: {buildings[closestIndex].buildingName} ({result.distanceMeters:F1}m away)");
        }

        return result;
    }

    /// <summary>
    /// Calculate distance between two GPS coordinates in meters
    /// Uses Haversine formula for accuracy
    /// </summary>
    private float CalculateDistanceMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double EARTH_RADIUS_METERS = 6371000;

        double lat1Rad = lat1 * Math.PI / 180;
        double lat2Rad = lat2 * Math.PI / 180;
        double deltaLat = (lat2 - lat1) * Math.PI / 180;
        double deltaLon = (lon2 - lon1) * Math.PI / 180;

        double a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                   Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                   Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);
        
        double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return (float)(EARTH_RADIUS_METERS * c);
    }

    /// <summary>
    /// Get all buildings in a specific campus area
    /// </summary>
    public BuildingInfo[] GetBuildingsInArea(string campusArea)
    {
        return Array.FindAll(buildings, b => b.campusArea == campusArea);
    }

    /// <summary>
    /// Get the full building database
    /// </summary>
    public BuildingInfo[] GetAllBuildings()
    {
        return buildings;
    }
}
