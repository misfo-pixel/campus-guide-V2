using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;

public class AzureSpeechSTTClient : MonoBehaviour
{
    [Header("Azure OpenAI Transcription")]
    [FormerlySerializedAs("speechKey")]
    [SerializeField] private string apiKey = "";
    [FormerlySerializedAs("speechRegion")]
    [SerializeField] private string transcriptionEndpoint = "";
    [SerializeField] private string model = "gpt-4o-mini-transcribe";
    [SerializeField] private string recognitionLanguage = "";
    [SerializeField] private string audioMimeType = "audio/wav";
    [SerializeField] private int timeoutSeconds = 60;
    [SerializeField] private bool verboseLogging = false;

    public void TranscribeWav(byte[] wavBytes, Action<string> onSuccess, Action<string> onError)
    {
        LocalSecrets secrets = LocalSecrets.Load();
        string resolvedApiKey = string.IsNullOrWhiteSpace(apiKey) ? secrets.azureOpenAIApiKey : apiKey;
        string resolvedEndpoint = string.IsNullOrWhiteSpace(transcriptionEndpoint) ? secrets.azureOpenAITranscriptionEndpoint : transcriptionEndpoint;

        if (string.IsNullOrWhiteSpace(resolvedApiKey))
        {
            onError?.Invoke("Azure OpenAI transcription apiKey is empty.");
            return;
        }

        if (string.IsNullOrWhiteSpace(resolvedEndpoint))
        {
            onError?.Invoke("Azure OpenAI transcriptionEndpoint is empty.");
            return;
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            onError?.Invoke("Azure OpenAI transcription model is empty.");
            return;
        }

        StartCoroutine(SendTranscriptionRequest(wavBytes, resolvedApiKey, resolvedEndpoint, onSuccess, onError));
    }

    private IEnumerator SendTranscriptionRequest(byte[] wavBytes, string resolvedApiKey, string resolvedEndpoint, Action<string> onSuccess, Action<string> onError)
    {
        if (wavBytes == null || wavBytes.Length == 0)
        {
            onError?.Invoke("No WAV data to transcribe.");
            yield break;
        }

        List<IMultipartFormSection> formSections = new List<IMultipartFormSection>
        {
            new MultipartFormFileSection("file", wavBytes, "recording.wav", audioMimeType),
            new MultipartFormDataSection("model", model),
            new MultipartFormDataSection("response_format", "json")
        };

        string languageHint = NormalizeLanguageHint(recognitionLanguage);
        if (!string.IsNullOrWhiteSpace(languageHint))
        {
            formSections.Add(new MultipartFormDataSection("language", languageHint));
        }

        string requestUrl = BuildTranscriptionUrl(resolvedEndpoint);
        if (verboseLogging)
        {
            Debug.Log("[AzureSpeechSTTClient] Sending transcription request to: " + requestUrl);
            Debug.Log(
                "[AzureSpeechSTTClient] model=" + model +
                " language=" + languageHint +
                " audioBytes=" + wavBytes.Length +
                " mime=" + audioMimeType);
        }

        using UnityWebRequest req = UnityWebRequest.Post(requestUrl, formSections);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.timeout = timeoutSeconds;

        req.SetRequestHeader("api-key", resolvedApiKey);
        req.SetRequestHeader("Accept", "application/json");

        yield return req.SendWebRequest();

        if (verboseLogging)
        {
            Debug.Log(
                "[AzureSpeechSTTClient] Request finished. Result=" + req.result +
                " Code=" + req.responseCode);
        }

        if (verboseLogging && !string.IsNullOrEmpty(req.downloadHandler.text))
        {
            Debug.Log("[AzureSpeechSTTClient] Response body:\n" + req.downloadHandler.text);
        }

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke(req.error + "\n" + req.downloadHandler.text);
            yield break;
        }

        string raw = req.downloadHandler.text;
        if (verboseLogging)
        {
            Debug.Log("[AzureSpeechSTTClient] Raw transcription response:\n" + raw);
        }

        string text = ExtractTranscriptText(raw);

        if (string.IsNullOrEmpty(text))
        {
            onError?.Invoke("Could not extract text from Azure OpenAI transcription response.\n" + raw);
            yield break;
        }

        onSuccess?.Invoke(text);
    }

    private string BuildTranscriptionUrl(string resolvedEndpoint)
    {
        string trimmed = resolvedEndpoint.Trim();

        if (trimmed.EndsWith("/audio/transcriptions", StringComparison.OrdinalIgnoreCase) ||
            trimmed.Contains("/audio/transcriptions?", StringComparison.OrdinalIgnoreCase))
        {
            return EnsureApiVersion(trimmed);
        }

        string baseUrl = trimmed.TrimEnd('/');
        if (baseUrl.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
        {
            baseUrl += "/audio/transcriptions";
        }
        else
        {
            baseUrl += "/openai/v1/audio/transcriptions";
        }

        return EnsureApiVersion(baseUrl);
    }

    private static string EnsureApiVersion(string url)
    {
        if (url.Contains("api-version=", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        string separator = url.Contains("?") ? "&" : "?";
        return url + separator + "api-version=preview";
    }

    private static string NormalizeLanguageHint(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        string normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "en" => "english",
            "en-us" => "english",
            "en-gb" => "english",
            "zh" => "chinese",
            "zh-cn" => "chinese",
            "zh-tw" => "chinese",
            _ => value.Trim()
        };
    }

    private string ExtractTranscriptText(string raw)
    {
        Match m = Regex.Match(raw, "\"text\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"");
        if (!m.Success) return null;

        string text = m.Groups[1].Value;
        text = text.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");
        return text;
    }
}
