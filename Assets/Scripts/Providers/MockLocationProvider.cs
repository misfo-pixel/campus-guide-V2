using UnityEngine;

public class MockLocationProvider : MonoBehaviour
{
    [SerializeField] private string campusArea = "UMN East Bank";
    [SerializeField] private string buildingHint = "Keller Hall";

    public string GetLocationSummary()
    {
        return campusArea + " / " + buildingHint;
    }

    public string GetBuildingHint()
    {
        return buildingHint;
    }
}