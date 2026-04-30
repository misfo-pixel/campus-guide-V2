using System;
using UnityEngine;

/// <summary>
/// Abstract base class for OCR service providers.
/// Extend this to implement concrete OCR backends (Azure, Google, etc.)
/// without changing the capture pipeline.
/// </summary>
public abstract class OCRProviderBase : MonoBehaviour
{
    /// <summary>
    /// Sends image bytes to the OCR service and returns results asynchronously.
    /// </summary>
    /// <param name="imageBytes">Raw image data (e.g. PNG-encoded bytes).</param>
    /// <param name="onSuccess">Callback invoked with the OCR result on success.</param>
    /// <param name="onError">Callback invoked with a descriptive error message on failure.</param>
    public abstract void ExtractTextAsync(
        byte[] imageBytes,
        Action<OCRResult> onSuccess,
        Action<string> onError
    );

    /// <summary>
    /// Returns true when the provider has valid credentials and is ready to accept requests.
    /// </summary>
    public abstract bool IsAvailable { get; }
}
