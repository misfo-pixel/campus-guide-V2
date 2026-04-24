using System;
using UnityEngine;

[Serializable]
public class LocalSecrets
{
    public string azureOpenAIApiKey;
    public string azureOpenAIResponsesEndpoint;
    public string azureOpenAITranscriptionEndpoint;
    public string azureOpenAISpeechEndpoint;

    private static LocalSecrets cached;

    public static LocalSecrets Load()
    {
        if (cached != null)
        {
            return cached;
        }

        TextAsset asset = Resources.Load<TextAsset>("LocalSecrets");
        if (asset == null || string.IsNullOrWhiteSpace(asset.text))
        {
            cached = new LocalSecrets();
            return cached;
        }

        try
        {
            cached = JsonUtility.FromJson<LocalSecrets>(asset.text) ?? new LocalSecrets();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[LocalSecrets] Failed to parse Resources/LocalSecrets.json\n" + e.Message);
            cached = new LocalSecrets();
        }

        return cached;
    }
}