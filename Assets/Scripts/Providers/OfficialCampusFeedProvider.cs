using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEngine;
using UnityEngine.Networking;

public class OfficialCampusFeedProvider : MonoBehaviour
{
    // Current status:
    // - `https://events.tc.umn.edu/all/feed` returns 200 in Unity.
    // - But the payload does not reliably deserialize into a normal RSS item list for this project.
    // - When we tried a loose fallback, it captured generic page copy instead of real events.
    //
    // So this provider is intentionally conservative right now:
    // - Only keep parsed items that look like real event titles.
    // - If parsing is unclear, report "temporarily unavailable" rather than feeding bad text to the LLM.
    //
    // Best next debugging step:
    // 1. Inspect the exact 200-response body from Unity for this endpoint.
    // 2. Confirm whether UMN exposes a more stable LiveWhale RSS/JSON endpoint for events.
    // 3. Replace this parser with a schema-specific parser once the response format is confirmed.
    private static readonly string[] GenericNonEventPhrases =
    {
        "Find events on the UMN Twin Cities Events Calendar",
        "Events Calendar",
        "Use search to filter by date, location, or category",
        "University of Minnesota Twin Cities"
    };

    [Header("Official UMN Feed")]
    [SerializeField] private string feedUrl = "https://events.tc.umn.edu/live/json/events/max/3";
    [SerializeField] private int maxItems = 3;
    [SerializeField] private int timeoutSeconds = 15;

    private List<string> latestItems = new List<string>();
    private bool hasLoaded = false;

    public bool HasLoaded => hasLoaded;

    private void Start()
    {
        Debug.LogWarning("[OfficialCampusFeedProvider] Starting feed fetch: " + feedUrl);
        StartCoroutine(FetchFeed());
    }

    public IEnumerator FetchFeed()
    {
        using UnityWebRequest req = UnityWebRequest.Get(feedUrl);
        req.timeout = timeoutSeconds;
        yield return req.SendWebRequest();

        Debug.LogWarning(
            "[OfficialCampusFeedProvider] Feed request finished. " +
            "Result=" + req.result +
            " Code=" + req.responseCode);

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(
                "[OfficialCampusFeedProvider] Feed request failed. " +
                "Result=" + req.result +
                " Code=" + req.responseCode +
                "\n" + req.error +
                "\n" + req.downloadHandler.text);
            yield break;
        }

        string xmlText = req.downloadHandler.text;
        
        Debug.Log($"[OfficialCampusFeedProvider] Response length: {xmlText?.Length ?? 0}");
        Debug.Log($"[OfficialCampusFeedProvider] Response start: {xmlText?.Substring(0, Mathf.Min(xmlText?.Length ?? 0, 300))}");

        try
        {
            // Try JSON first (LiveWhale JSON API)
            if (xmlText.TrimStart().StartsWith("[") || xmlText.TrimStart().StartsWith("{"))
            {
                ParseJson(xmlText);
            }
            else
            {
                // Preferred path: parse a real RSS payload with <item><title>...</title></item>.
                ParseRss(xmlText);
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogError(
                "[OfficialCampusFeedProvider] RSS parse failed.\n" +
                ex +
                "\nResponse preview:\n" +
                GetPreview(xmlText));

            // Last-resort fallback for inspection/debugging.
            // This is intentionally strict and may still produce zero items.
            // Zero items is safer than sending page boilerplate to the model.
            ParseRssFallback(xmlText);
        }

        hasLoaded = true;
        Debug.LogWarning(
            "[OfficialCampusFeedProvider] Feed loaded successfully. " +
            "Items=" + latestItems.Count +
            " Summary=" + GetEventsSummary());
    }

    private void ParseRssFallback(string xmlText)
    {
        latestItems.Clear();

        // UMN's LiveWhale calendar renders event titles inside specific HTML patterns.
        // Try multiple extraction strategies from most specific to least.

        // Strategy 1: LiveWhale event titles in <a> tags with /event/ URLs
        MatchCollection eventLinkMatches = Regex.Matches(
            xmlText,
            @"<a[^>]+href=""[^""]*\/event\/[^""]*""[^>]*>\s*(.*?)\s*</a>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        for (int i = 0; i < eventLinkMatches.Count && latestItems.Count < maxItems; i++)
        {
            string title = System.Net.WebUtility.HtmlDecode(
                Regex.Replace(eventLinkMatches[i].Groups[1].Value, @"<[^>]+>", "")).Trim();

            if (!string.IsNullOrWhiteSpace(title) && title.Length >= 8 && title.Length <= 200 &&
                !LooksLikeGenericPageCopy(title) &&
                !title.Equals("RSS", System.StringComparison.OrdinalIgnoreCase))
            {
                latestItems.Add(title);
            }
        }

        // Strategy 2: <h3> tags (LiveWhale often uses h3 for event titles)
        if (latestItems.Count == 0)
        {
            MatchCollection h3Matches = Regex.Matches(
                xmlText,
                @"<h3[^>]*>\s*(.*?)\s*</h3>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            for (int i = 0; i < h3Matches.Count && latestItems.Count < maxItems; i++)
            {
                string heading = System.Net.WebUtility.HtmlDecode(
                    Regex.Replace(h3Matches[i].Groups[1].Value, @"<[^>]+>", "")).Trim();

                if (!string.IsNullOrWhiteSpace(heading) && heading.Length >= 8 && heading.Length <= 200 &&
                    !LooksLikeGenericPageCopy(heading))
                {
                    latestItems.Add(heading);
                }
            }
        }

        // Strategy 3: <title> tags as last resort (original fallback)
        if (latestItems.Count == 0)
        {
            MatchCollection titleMatches = Regex.Matches(
                xmlText,
                @"<title>\s*(.*?)\s*</title>",
                RegexOptions.IgnoreCase | RegexOptions.Singleline);

            for (int i = 0; i < titleMatches.Count && latestItems.Count < maxItems; i++)
            {
                string title = System.Net.WebUtility.HtmlDecode(titleMatches[i].Groups[1].Value).Trim();

                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                if (LooksLikeGenericPageCopy(title) ||
                    title.Equals("RSS", System.StringComparison.OrdinalIgnoreCase) ||
                    title.Length < 8)
                {
                    continue;
                }

                latestItems.Add(title);
            }
        }

        Debug.Log($"[OfficialCampusFeedProvider] Fallback parser found {latestItems.Count} items");
    }

    private string GetPreview(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return "<empty>";
        }

        const int maxPreviewLength = 500;
        return text.Length <= maxPreviewLength ? text : text.Substring(0, maxPreviewLength);
    }

    /// <summary>
    /// Parses LiveWhale JSON API response. The response is a JSON array of event objects
    /// with fields like "title", "date_utc", "location", etc.
    /// </summary>
    private void ParseJson(string jsonText)
    {
        latestItems.Clear();

        // LiveWhale returns a JSON array: [{"title":"Event Name","date_utc":"...","location":"..."}, ...]
        // Use regex to extract title fields since Unity's JsonUtility doesn't handle arrays of unknown objects well
        MatchCollection titleMatches = Regex.Matches(
            jsonText,
            @"""title""\s*:\s*""([^""]+)""",
            RegexOptions.IgnoreCase);

        MatchCollection dateMatches = Regex.Matches(
            jsonText,
            @"""date""\s*:\s*""([^""]+)""",
            RegexOptions.IgnoreCase);

        MatchCollection locationMatches = Regex.Matches(
            jsonText,
            @"""location""\s*:\s*""([^""]+)""",
            RegexOptions.IgnoreCase);

        for (int i = 0; i < titleMatches.Count && latestItems.Count < maxItems; i++)
        {
            string title = titleMatches[i].Groups[1].Value.Trim();
            title = title.Replace("\\u0026", "&").Replace("\\u0027", "'").Replace("\\/", "/");

            if (string.IsNullOrWhiteSpace(title) || LooksLikeGenericPageCopy(title) ||
                title.StartsWith("Learn more", System.StringComparison.OrdinalIgnoreCase))
                continue;

            string entry = title;

            // Add date if available
            if (i < dateMatches.Count)
            {
                string date = dateMatches[i].Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(date))
                    entry += " (" + date + ")";
            }

            // Add location if available
            if (i < locationMatches.Count)
            {
                string location = locationMatches[i].Groups[1].Value.Trim();
                if (!string.IsNullOrWhiteSpace(location))
                    entry += " at " + location;
            }

            latestItems.Add(entry);
        }

        Debug.Log($"[OfficialCampusFeedProvider] JSON parser found {latestItems.Count} events");
    }

    private void ParseRss(string xmlText)
    {
        latestItems.Clear();

        XmlDocument doc = new XmlDocument();
        doc.LoadXml(xmlText);

        // Assumes a standard RSS shape. If UMN's endpoint is HTML or a nonstandard feed,
        // this will either return zero <item> nodes or throw before we get here.
        XmlNodeList itemNodes = doc.GetElementsByTagName("item");

        int count = Mathf.Min(maxItems, itemNodes.Count);
        for (int i = 0; i < count; i++)
        {
            XmlNode item = itemNodes[i];

            string title = GetChildInnerText(item, "title");
            string pubDate = GetChildInnerText(item, "pubDate");

            if (!string.IsNullOrWhiteSpace(title))
            {
                if (LooksLikeGenericPageCopy(title))
                {
                    continue;
                }

                string line = title;
                if (!string.IsNullOrWhiteSpace(pubDate))
                {
                    line += " (" + pubDate + ")";
                }

                latestItems.Add(line);
            }
        }
    }

    private string GetChildInnerText(XmlNode parent, string childName)
    {
        if (parent == null) return "";

        foreach (XmlNode child in parent.ChildNodes)
        {
            if (child.Name == childName)
            {
                return child.InnerText.Trim();
            }
        }

        return "";
    }

    public string GetEventsSummary()
    {
        if (!hasLoaded)
        {
            return "Official campus events are still loading.";
        }

        if (latestItems.Count == 0)
        {
            // Important: avoid hallucination-by-ingestion.
            // If we cannot verify concrete event items, tell the caller the official feed is unavailable.
            return "Official campus events are temporarily unavailable.";
        }

        return "Official Campus Events: " + string.Join(" | ", latestItems);
    }

    private bool LooksLikeGenericPageCopy(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        for (int i = 0; i < GenericNonEventPhrases.Length; i++)
        {
            if (text.IndexOf(GenericNonEventPhrases[i], System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
