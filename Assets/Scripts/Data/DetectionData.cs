using System;

[Serializable]
public class DetectionData
{
    public string zoneId;
    public string zoneType;        // hallway / classroom / lobby
    public string semanticLabel;   // "classroom entrance"
    public string description;
}