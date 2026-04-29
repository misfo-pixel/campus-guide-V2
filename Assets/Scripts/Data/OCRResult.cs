using System;

[Serializable]
public class OCRResult
{
    public string extractedText;
    public float confidence;
    public OCRTextRegion[] regions;
    public long timestampMs;

    public bool Validate()
    {
        if (extractedText == null)
        {
            UnityEngine.Debug.LogWarning("[OCRResult] Validate FAILED: extractedText is null");
            return false;
        }

        if (confidence < 0f || confidence > 1f)
        {
            UnityEngine.Debug.LogWarning($"[OCRResult] Validate FAILED: confidence={confidence} out of range [0,1]");
            return false;
        }

        if (regions == null)
        {
            UnityEngine.Debug.LogWarning("[OCRResult] Validate FAILED: regions is null");
            return false;
        }

        return true;
    }
}

[Serializable]
public class OCRTextRegion
{
    public string text;
    public float confidence;
    public float x;
    public float y;
    public float width;
    public float height;

    public bool Validate()
    {
        if (x < 0f || x > 1f || y < 0f || y > 1f || width < 0f || width > 1f || height < 0f || height > 1f)
        {
            UnityEngine.Debug.LogWarning($"[OCRTextRegion] Validate FAILED: bounding box out of [0,1] range. x={x}, y={y}, w={width}, h={height}");
            return false;
        }

        return true;
    }
}
