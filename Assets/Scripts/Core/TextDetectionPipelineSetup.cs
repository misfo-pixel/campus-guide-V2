using UnityEngine;

/// <summary>
/// Wiring script that validates all text detection pipeline references at startup
/// and forwards per-frame gaze dwell progress to the panel controller.
/// Attach to a single GameObject in the scene alongside the pipeline components.
/// </summary>
public class TextDetectionPipelineSetup : MonoBehaviour
{
    [Header("Pipeline Components")]
    [SerializeField] private GazeRaycastController gazeController;
    [SerializeField] private CameraFrameCapturer cameraFrameCapturer;
    [SerializeField] private OCRProviderBase ocrProvider;
    [SerializeField] private TextDetectionContextProvider textDetectionContextProvider;
    [SerializeField] private TextDetectionPanelController panelController;
    [SerializeField] private DemoContextProvider demoContextProvider;
    [SerializeField] private LLMDemoRunner llmDemoRunner;

    private void Awake()
    {
        Debug.Log("[TextDetectionPipelineSetup] Awake() called on: " + gameObject.name);
    }

    private void Start()
    {
        ValidateReferences();

        if (panelController != null && Camera.main != null)
        {
            panelController.SetCameraTransform(Camera.main.transform);
        }

        // Log a full pipeline health summary at startup
        Debug.Log("=== [TextDetectionPipelineSetup] PIPELINE HEALTH SUMMARY ===");
        Debug.Log($"  GazeRaycastController: {(gazeController != null ? gazeController.gameObject.name : "MISSING")}");
        Debug.Log($"  CameraFrameCapturer:   {(cameraFrameCapturer != null ? cameraFrameCapturer.gameObject.name : "MISSING")}");
        Debug.Log($"  OCRProvider:           {(ocrProvider != null ? $"{ocrProvider.gameObject.name} (available={ocrProvider.IsAvailable})" : "MISSING")}");
        Debug.Log($"  ContextProvider:       {(textDetectionContextProvider != null ? textDetectionContextProvider.gameObject.name : "MISSING")}");
        Debug.Log($"  PanelController:       {(panelController != null ? panelController.gameObject.name : "MISSING")}");
        Debug.Log($"  DemoContextProvider:   {(demoContextProvider != null ? demoContextProvider.gameObject.name : "MISSING")}");
        Debug.Log($"  LLMDemoRunner:         {(llmDemoRunner != null ? llmDemoRunner.gameObject.name : "MISSING")}");
        Debug.Log($"  Camera.main:           {(Camera.main != null ? Camera.main.gameObject.name : "MISSING")}");

        if (gazeController != null)
        {
            Debug.Log($"  GazeRaycast config: dwellThreshold={gazeController.GetType().GetField("dwellTimeThreshold", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(gazeController)}, enabled={gazeController.enabled}, isActiveAndEnabled={gazeController.isActiveAndEnabled}");
        }
        Debug.Log("=== END PIPELINE HEALTH SUMMARY ===");
    }

    private void Update()
    {
        if (gazeController != null && panelController != null)
        {
            panelController.UpdateDwellProgress(gazeController.CurrentDwellProgress);
        }
    }

    private void ValidateReferences()
    {
        if (gazeController == null)
        {
            Debug.LogWarning("[TextDetectionPipelineSetup] Missing reference: GazeRaycastController");
        }

        if (cameraFrameCapturer == null)
        {
            Debug.LogWarning("[TextDetectionPipelineSetup] Missing reference: CameraFrameCapturer");
        }

        if (ocrProvider == null)
        {
            Debug.LogWarning("[TextDetectionPipelineSetup] Missing reference: OCRProviderBase");
        }

        if (textDetectionContextProvider == null)
        {
            Debug.LogWarning("[TextDetectionPipelineSetup] Missing reference: TextDetectionContextProvider");
        }

        if (panelController == null)
        {
            Debug.LogWarning("[TextDetectionPipelineSetup] Missing reference: TextDetectionPanelController");
        }

        if (demoContextProvider == null)
        {
            Debug.LogWarning("[TextDetectionPipelineSetup] Missing reference: DemoContextProvider");
        }

        if (llmDemoRunner == null)
        {
            Debug.LogWarning("[TextDetectionPipelineSetup] Missing reference: LLMDemoRunner");
        }
    }
}
