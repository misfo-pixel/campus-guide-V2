using System;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// OCR provider that uses Azure OpenAI's GPT-4o vision capability to extract text from images.
/// Reuses the same Azure OpenAI credentials (azureOpenAIApiKey / azureOpenAIResponsesEndpoint)
/// as <see cref="OpenAIResponseClient"/>, so no separate Azure Computer Vision resource is needed.
/// </summary>
public class AzureVisionOCRProvider : OCRProviderBase
{
    [SerializeField] private int timeoutSeconds = 30;
    [SerializeField] private string model = "gpt-5";

    private string apiKey;
    private string endpoint;

    private void Start()
    {
        LoadCredentials();
    }

    private void LoadCredentials()
    {
        LocalSecrets secrets = LocalSecrets.Load();
        apiKey = secrets.azureOpenAIApiKey;
        endpoint = secrets.azureOpenAIResponsesEndpoint;

        if (!IsAvailable)
        {
            Debug.LogWarning("[AzureVisionOCRProvider] Azure OpenAI credentials are missing. OCR will not be available.");
        }
    }

    public override bool IsAvailable =>
        !string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(endpoint);

    public override void ExtractTextAsync(
        byte[] imageBytes,
        Action<OCRResult> onSuccess,
        Action<string> onError)
    {
        Debug.Log($"[AzureVisionOCRProvider] ExtractTextAsync called. imageBytes={imageBytes?.Length ?? 0}, IsAvailable={IsAvailable}");

        if (imageBytes == null || imageBytes.Length == 0)
        {
            onError?.Invoke("Image bytes are null or empty.");
            return;
        }

        if (!IsAvailable)
        {
            Debug.LogError($"[AzureVisionOCRProvider] Not available! apiKey empty={string.IsNullOrWhiteSpace(apiKey)}, endpoint empty={string.IsNullOrWhiteSpace(endpoint)}");
            onError?.Invoke("Azure OpenAI OCR is not available. Check API key and endpoint configuration.");
            return;
        }

        StartCoroutine(SendVisionRequest(imageBytes, onSuccess, onError));
    }

    private IEnumerator SendVisionRequest(
        byte[] imageBytes,
        Action<OCRResult> onSuccess,
        Action<string> onError)
    {
        string base64Image = Convert.ToBase64String(imageBytes);
        string requestUrl = BuildResponsesUrl();
        string requestBody = BuildRequestBody(base64Image);

        using UnityWebRequest req = new UnityWebRequest(requestUrl, "POST");
        byte[] bodyRaw = Encoding.UTF8.GetBytes(requestBody);

        Debug.Log($"[AzureVisionOCRProvider] Sending request to: {requestUrl} (body size: {bodyRaw.Length} bytes, timeout: {timeoutSeconds}s)");

        req.uploadHandler = new UploadHandlerRaw(bodyRaw);
        req.downloadHandler = new DownloadHandlerBuffer();
        req.timeout = timeoutSeconds;

        req.SetRequestHeader("Content-Type", "application/json");
        req.SetRequestHeader("api-key", apiKey);

        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError($"[AzureVisionOCRProvider] Request FAILED: result={req.result}, error='{req.error}', responseCode={req.responseCode}, body='{req.downloadHandler?.text?.Substring(0, Mathf.Min(req.downloadHandler?.text?.Length ?? 0, 300))}'");
            onError?.Invoke($"Azure OpenAI vision request failed: {req.error}");
            yield break;
        }

        string raw = req.downloadHandler.text;
        Debug.Log($"[AzureVisionOCRProvider] Response received ({raw?.Length ?? 0} chars). First 500: {raw?.Substring(0, Mathf.Min(raw?.Length ?? 0, 500))}");

        try
        {
            string extractedText = ExtractTextFromResponse(raw);
            Debug.Log($"[AzureVisionOCRProvider] Extracted text: '{extractedText}' (null={extractedText == null}, empty={string.IsNullOrWhiteSpace(extractedText)})");

            if (string.IsNullOrWhiteSpace(extractedText))
            {
                onSuccess?.Invoke(new OCRResult
                {
                    extractedText = "",
                    confidence = 0f,
                    regions = Array.Empty<OCRTextRegion>(),
                    timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
                });
                yield break;
            }

            onSuccess?.Invoke(new OCRResult
            {
                extractedText = extractedText.Trim(),
                confidence = 0.9f, // GPT-4o vision is high confidence when it returns text
                regions = Array.Empty<OCRTextRegion>(),
                timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }
        catch (Exception ex)
        {
            onError?.Invoke($"Failed to parse vision response: {ex.Message}");
        }
    }

    private string BuildRequestBody(string base64Image)
    {
        string systemPrompt = EscapeJson(
            "You are an OCR assistant. Extract ALL visible text from the image exactly as it appears. " +
            "Return ONLY the extracted text, nothing else. If no text is visible, return exactly: NO_TEXT_FOUND"
        );

        string dataUri = $"data:image/png;base64,{base64Image}";

        return
$@"{{
  ""model"": ""{model}"",
  ""input"": [
    {{
      ""role"": ""system"",
      ""content"": [
        {{ ""type"": ""input_text"", ""text"": ""{systemPrompt}"" }}
      ]
    }},
    {{
      ""role"": ""user"",
      ""content"": [
        {{ ""type"": ""input_image"", ""image_url"": ""{EscapeJson(dataUri)}"" }},
        {{ ""type"": ""input_text"", ""text"": ""Extract all text from this image."" }}
      ]
    }}
  ]
}}";
    }

    private string BuildResponsesUrl()
    {
        string trimmed = endpoint.Trim();

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

    private static string ExtractTextFromResponse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        // Try to extract the text field from the response JSON
        Match m = Regex.Match(raw, "\"text\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"");
        if (!m.Success)
            return null;

        string text = m.Groups[1].Value;
        text = text.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");

        // If the model says no text found, return empty
        if (text.Trim().Equals("NO_TEXT_FOUND", StringComparison.OrdinalIgnoreCase))
            return "";

        return text;
    }

    private static string EscapeJson(string input)
    {
        input ??= string.Empty;
        return input
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "");
    }
}
