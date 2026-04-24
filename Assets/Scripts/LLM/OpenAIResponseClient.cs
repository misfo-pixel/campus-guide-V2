using System;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;

public class OpenAIResponseClient : MonoBehaviour
{
    [Header("Azure OpenAI")]
    [SerializeField] private string apiKey = "";
    [FormerlySerializedAs("endpoint")]
    [SerializeField] private string responsesEndpoint = "";
    [SerializeField] private string model = "gpt-5";
    [SerializeField] private int timeoutSeconds = 30;
    [SerializeField] private bool verboseLogging = false;

    public void RequestResponse(string fullContext, Action<LLMActionResult> onSuccess, Action<string> onError)
    {
        LocalSecrets secrets = LocalSecrets.Load();
        string resolvedApiKey = string.IsNullOrWhiteSpace(apiKey) ? secrets.azureOpenAIApiKey : apiKey;
        string resolvedEndpoint = string.IsNullOrWhiteSpace(responsesEndpoint) ? secrets.azureOpenAIResponsesEndpoint : responsesEndpoint;

        if (string.IsNullOrWhiteSpace(resolvedApiKey))
        {
            onError?.Invoke("Azure OpenAI apiKey is empty.");
            return;
        }

        if (string.IsNullOrWhiteSpace(resolvedEndpoint))
        {
            onError?.Invoke("Azure OpenAI responsesEndpoint is empty.");
            return;
        }

        StartCoroutine(SendRequest(fullContext, resolvedApiKey, resolvedEndpoint, onSuccess, onError));
    }

    private IEnumerator SendRequest(string fullContext, string resolvedApiKey, string resolvedEndpoint, Action<LLMActionResult> onSuccess, Action<string> onError)
    {
        string systemPrompt =
@"You are UMN Sprite, an intelligent campus companion.
Return ONLY valid JSON.
Use exactly these fields:
action, title, body

Allowed action values:
greet, explain, point, alert, idle

Keep title short.
Keep body under 30 words.";

        string json = BuildRequestBody(systemPrompt, fullContext);
        string requestUrl = BuildResponsesUrl(resolvedEndpoint);

        if (verboseLogging)
        {
            Debug.Log("[OpenAIResponseClient] Sending request to: " + requestUrl);
        }

        using UnityWebRequest req = new UnityWebRequest(requestUrl, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.timeout = timeoutSeconds;

        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("api-key", resolvedApiKey);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            onError?.Invoke(req.error + "\n" + req.downloadHandler.text);
            yield break;
        }

        string raw = req.downloadHandler.text;
        if (verboseLogging)
        {
            Debug.Log("[OpenAIResponseClient] Raw response:\n" + raw);
        }

        if (TryExtractResultFromRaw(raw, out LLMActionResult directResult))
        {
            onSuccess?.Invoke(directResult);
            yield break;
        }

        string outputText = ExtractFirstTextField(raw);

        if (string.IsNullOrEmpty(outputText))
        {
            onError?.Invoke("Could not extract model text from response.");
            yield break;
        }

        try
        {
            string cleaned = NormalizeJsonCandidate(outputText);
            LLMActionResult result = JsonUtility.FromJson<LLMActionResult>(cleaned);
            if (result == null)
            {
                onError?.Invoke("JSON parse produced null result.\nExtracted text:\n" + cleaned);
                yield break;
            }
            onSuccess?.Invoke(result);
        }
        catch (Exception e)
        {
            onError?.Invoke("JSON parse failed.\nExtracted text:\n" + outputText + "\n\n" + e.Message);
        }
    }

    private string BuildResponsesUrl(string resolvedEndpoint)
    {
        string trimmed = resolvedEndpoint.Trim();

        if (trimmed.Contains("/responses?", StringComparison.OrdinalIgnoreCase) ||
            trimmed.EndsWith("/responses", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        if (trimmed.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed.TrimEnd('/') + "/responses";
        }

        if (trimmed.EndsWith("/openai", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed.TrimEnd('/') + "/v1/responses";
        }

        return trimmed.TrimEnd('/') + "/openai/v1/responses";
    }

    private string BuildRequestBody(string systemPrompt, string userPrompt)
    {
        string s = EscapeJson(systemPrompt);
        string u = EscapeJson(userPrompt);

        return
$@"{{
  ""model"": ""{model}"",
  ""input"": [
    {{
      ""role"": ""system"",
      ""content"": [
        {{ ""type"": ""input_text"", ""text"": ""{s}"" }}
      ]
    }},
    {{
      ""role"": ""user"",
      ""content"": [
        {{ ""type"": ""input_text"", ""text"": ""{u}"" }}
      ]
    }}
  ]
}}";
    }

    private string EscapeJson(string input)
    {
        input ??= string.Empty;
        return input
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "");
    }

    private string ExtractFirstTextField(string raw)
    {
        Match m = Regex.Match(raw, "\"text\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"");
        if (!m.Success) return null;

        string text = m.Groups[1].Value;
        text = text.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");
        return text;
    }

    private static string NormalizeJsonCandidate(string candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return "{}";
        }

        string cleaned = candidate.Trim();

        if (cleaned.StartsWith("```", StringComparison.Ordinal))
        {
            cleaned = Regex.Replace(cleaned, "^```(?:json)?\\s*", string.Empty, RegexOptions.IgnoreCase);
            cleaned = Regex.Replace(cleaned, "\\s*```\\s*$", string.Empty);
        }

        int firstBrace = cleaned.IndexOf('{');
        int lastBrace = cleaned.LastIndexOf('}');
        if (firstBrace >= 0 && lastBrace >= firstBrace)
        {
            cleaned = cleaned.Substring(firstBrace, lastBrace - firstBrace + 1);
        }

        return cleaned.Trim();
    }

    private static bool TryExtractResultFromRaw(string raw, out LLMActionResult result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        Match objectMatch = Regex.Match(raw, "\\{\\s*\"action\"\\s*:\\s*\"(?:[^\"\\\\]|\\\\.)*\"\\s*,\\s*\"title\"\\s*:\\s*\"(?:[^\"\\\\]|\\\\.)*\"\\s*,\\s*\"body\"\\s*:\\s*\"(?:[^\"\\\\]|\\\\.)*\"\\s*\\}");
        if (!objectMatch.Success)
        {
            return false;
        }

        try
        {
            string candidate = NormalizeJsonCandidate(objectMatch.Value);
            result = JsonUtility.FromJson<LLMActionResult>(candidate);
            return result != null;
        }
        catch
        {
            return false;
        }
    }
}
