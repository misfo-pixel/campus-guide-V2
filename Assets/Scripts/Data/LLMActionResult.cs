using System;

[Serializable]
public class LLMActionResult
{
    public string action;   // greet / point / explain / alert / idle
    public string title;
    public string body;
}