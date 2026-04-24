using System;
using UnityEngine;

public enum SpriteMode
{
    Idle,
    Greeting,
    Info,
    Navigation,
    Reminder
}

[Serializable]
public class SpriteStateData
{
    public SpriteMode Mode = SpriteMode.Idle;
    public string Title = "UMN Sprite";
    [TextArea]
    public string Body = "Welcome. I am your campus companion.";
    public bool ShowPanel = true;
}

