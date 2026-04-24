using UnityEngine;

public class VoiceTranscriptProvider : MonoBehaviour
{
    [SerializeField][TextArea] private string latestTranscript = "";

    public void SetTranscript(string text)
    {
        latestTranscript = text;
        Debug.Log("[VoiceTranscriptProvider] Transcript = " + latestTranscript);
    }

    public string GetTranscript()
    {
        return latestTranscript;
    }

    public void ClearTranscript()
    {
        latestTranscript = "";
    }
}