using System;

[Serializable]
public class LocationData
{
    public string source;       // gps / wifi / mock
    public string campusArea;   // e.g. "UMN East Bank"
    public string buildingHint; // e.g. "Keller Hall"
    public double latitude;
    public double longitude;
}