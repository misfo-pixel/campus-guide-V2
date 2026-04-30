using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;

[RequireComponent(typeof(AudioSource))]
public class AzureSpeechTTSClient : MonoBehaviour
{
    [Header("Azure OpenAI")]
    [SerializeField] private string apiKey = "";
    [FormerlySerializedAs("endpoint")]
    [SerializeField] private string speechEndpoint = "";
    [SerializeField] private string model = "gpt-4o-mini-tts";
    [SerializeField] private string voice = "alloy";
    [SerializeField] private string responseFormat = "mp3";
    [SerializeField] private int timeoutSeconds = 30;
    [SerializeField] private bool verboseLogging = false;

    [Header("Playback")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private bool autoPlayOnSuccess = true;

    private Coroutine activeRequest;

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
        }
    }

    public void SpeakText(string text, Action onSuccess = null, Action<string> onError = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            onError?.Invoke("Speech text is empty.");
            return;
        }

        LocalSecrets secrets = LocalSecrets.Load();
        string resolvedApiKey = string.IsNullOrWhiteSpace(apiKey) ? secrets.azureOpenAIApiKey : apiKey;
        string resolvedEndpoint = ResolveSpeechEndpoint(secrets);

        if (string.IsNullOrWhiteSpace(resolvedApiKey))
        {
            onError?.Invoke("Azure OpenAI apiKey is empty.");
            return;
        }

        if (string.IsNullOrWhiteSpace(resolvedEndpoint))
        {
            onError?.Invoke("Azure OpenAI speech endpoint is empty.");
            return;
        }

        if (activeRequest != null)
        {
            StopCoroutine(activeRequest);
            activeRequest = null;
        }

        activeRequest = StartCoroutine(SendRequest(text, resolvedApiKey, resolvedEndpoint, onSuccess, onError));
    }

    public void StopPlayback()
    {
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
        }
    }

    private IEnumerator SendRequest(string text, string resolvedApiKey, string resolvedEndpoint, Action onSuccess, Action<string> onError)
    {
        string requestUrl = resolvedEndpoint.Trim();
        string json = BuildRequestBody(text);
        AudioType audioType = ResolveAudioType();

        if (verboseLogging)
        {
            Debug.Log("[AzureSpeechTTSClient] Sending speech request to: " + requestUrl);
        }

        using UnityWebRequest req = new UnityWebRequest(requestUrl, "POST");
        req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        req.downloadHandler = new DownloadHandlerAudioClip(requestUrl, audioType);
        req.timeout = timeoutSeconds;

        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("api-key", resolvedApiKey);

        yield return req.SendWebRequest();

        activeRequest = null;

        if (req.result != UnityWebRequest.Result.Success)
        {
            string responseBody = TryGetErrorBody(req);
            Debug.LogError("[AzureSpeechTTSClient] Speech request failed. Result=" + req.result + " Code=" + req.responseCode + "\n" + req.error + "\n" + responseBody);
            onError?.Invoke(req.error + "\n" + responseBody);
            yield break;
        }

        AudioClip clip = DownloadHandlerAudioClip.GetContent(req);
        if (clip == null)
        {
            Debug.LogError("[AzureSpeechTTSClient] Speech request returned no audio clip.");
            onError?.Invoke("Azure OpenAI speech request succeeded, but no audio clip was returned.");
            yield break;
        }

        if (autoPlayOnSuccess && audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = clip;
            audioSource.Play();
        }

        onSuccess?.Invoke();
    }

    private string ResolveSpeechEndpoint(LocalSecrets secrets)
    {
        if (!string.IsNullOrWhiteSpace(speechEndpoint))
        {
            return speechEndpoint;
        }

        if (!string.IsNullOrWhiteSpace(secrets.azureOpenAISpeechEndpoint))
        {
            string ep = secrets.azureOpenAISpeechEndpoint.Trim();
            // If it already contains /audio/speech, use as-is
            if (ep.Contains("/audio/speech", StringComparison.OrdinalIgnoreCase))
            {
                return ep;
            }
        }

        // Build deployment-based URL: /openai/deployments/{model}/audio/speech?api-version=...
        string baseEndpoint = !string.IsNullOrWhiteSpace(secrets.azureOpenAIResponsesEndpoint)
            ? secrets.azureOpenAIResponsesEndpoint
            : secrets.azureOpenAITranscriptionEndpoint;

        if (string.IsNullOrWhiteSpace(baseEndpoint))
        {
            baseEndpoint = "https://csci5629-group8-resource.openai.azure.com/openai/v1";
        }

        string trimmed = baseEndpoint.Trim();
        int openAiIndex = trimmed.IndexOf("/openai", StringComparison.OrdinalIgnoreCase);
        string resourceBase = openAiIndex >= 0 ? trimmed.Substring(0, openAiIndex) : trimmed.TrimEnd('/');
        string url = resourceBase + "/openai/deployments/" + model + "/audio/speech?api-version=2025-04-01-preview";
        Debug.Log($"[AzureSpeechTTSClient] Resolved speech endpoint: {url}");
        return url;
    }

    private string BuildRequestBody(string text)
    {
        return
$@"{{
  ""model"": ""{EscapeJson(model)}"",
  ""input"": ""{EscapeJson(text)}"",
  ""voice"": ""{EscapeJson(voice)}"",
  ""response_format"": ""{EscapeJson(responseFormat)}""
}}";
    }

    private AudioType ResolveAudioType()
    {
        string lower = responseFormat.ToLowerInvariant();
        if (lower == "wav")
        {
            return AudioType.WAV;
        }

        if (lower == "ogg")
        {
            return AudioType.OGGVORBIS;
        }

        return AudioType.MPEG;
    }

    private static string EscapeJson(string input)
    {
        return (input ?? string.Empty)
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", string.Empty);
    }

    private static string TryGetErrorBody(UnityWebRequest req)
    {
        if (req == null || req.downloadHandler == null)
        {
            return "(no response body)";
        }

        try
        {
            return string.IsNullOrWhiteSpace(req.downloadHandler.text)
                ? "(empty response body)"
                : req.downloadHandler.text;
        }
        catch (NotSupportedException)
        {
            byte[] data = req.downloadHandler.data;
            if (data == null || data.Length == 0)
            {
                return "(binary or unavailable response body)";
            }

            return Encoding.UTF8.GetString(data);
        }
    }
}
