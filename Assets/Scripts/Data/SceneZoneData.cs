using System;
using UnityEngine;

[Serializable]
public class SceneZoneData
{
    public string zoneId;
    public string title;

    [TextArea]
    public string description;
}