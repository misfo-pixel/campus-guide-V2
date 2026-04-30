using UnityEngine;

public class UMNContextManager : MonoBehaviour
{
    [Header("Display Targets")]
    [SerializeField] private UMNSpriteDisplayController spriteDisplayController;
    [SerializeField] private WorldInfoPanelController worldInfoPanelController;
    [SerializeField] private TextDetectionPanelController textDetectionPanelController;

    [Header("Initial State")]
    [SerializeField] private SpriteMode initialMode = SpriteMode.Greeting;
    [SerializeField] private string initialTitle = "UMN Sprite";
    [SerializeField][TextArea] private string initialBody = "Welcome. I am your campus companion.";
    [SerializeField] private bool initialShowPanel = true;

    private SpriteStateData currentState;

    private void Start()
    {
        currentState = new SpriteStateData
        {
            Mode = initialMode,
            Title = initialTitle,
            Body = initialBody,
            ShowPanel = initialShowPanel
        };

        ApplyCurrentState();

        if (Camera.main != null && worldInfoPanelController != null)
        {
            worldInfoPanelController.SetCameraTransform(Camera.main.transform);
        }

        if (Camera.main != null && textDetectionPanelController != null)
        {
            textDetectionPanelController.SetCameraTransform(Camera.main.transform);
        }
    }

    public void SetState(SpriteStateData newState)
    {
        if (newState == null)
        {
            Debug.LogWarning("[UMNContextManager] Ignoring null SetState input.");
            return;
        }

        Debug.Log($"[UMNContextManager] SetState — Mode={newState.Mode}, Title='{newState.Title}', ShowPanel={newState.ShowPanel}");

        currentState = new SpriteStateData
        {
            Mode = newState.Mode,
            Title = newState.Title,
            Body = newState.Body,
            ShowPanel = newState.ShowPanel
        };
        ApplyCurrentState();
    }

    public void SetSceneZone(SceneZoneData zoneData)
    {
        if (zoneData == null)
        {
            Debug.LogWarning("[UMNContextManager] Ignoring null zoneData.");
            return;
        }

        currentState = new SpriteStateData
        {
            Mode = SpriteMode.Info,
            Title = zoneData.title,
            Body = zoneData.description,
            ShowPanel = true
        };

        ApplyCurrentState();
    }

    protected void ApplyCurrentState()
    {
        Debug.Log($"[UMNContextManager] ApplyCurrentState — Mode={currentState.Mode}, Title='{currentState.Title}', ShowPanel={currentState.ShowPanel}");

        if (spriteDisplayController != null)
        {
            spriteDisplayController.ApplyState(currentState);
        }
        else
        {
            Debug.LogWarning("[UMNContextManager] spriteDisplayController is NULL");
        }

        // Always update both panels so they are both visible at all times
        if (worldInfoPanelController != null)
        {
            Debug.Log($"[UMNContextManager] Updating WorldInfoPanel (mode={currentState.Mode})");
            worldInfoPanelController.ApplyState(currentState);
        }
        else
        {
            Debug.LogWarning("[UMNContextManager] worldInfoPanelController is NULL");
        }

        if (currentState.Mode == SpriteMode.TextDetection)
        {
            Debug.Log("[UMNContextManager] Updating TextDetectionPanel (TextDetection mode)");
            if (textDetectionPanelController != null)
            {
                LLMActionResult result = new LLMActionResult
                {
                    title = currentState.Title,
                    body = currentState.Body
                };
                textDetectionPanelController.ShowLLMResponse(result);
            }
            else
            {
                Debug.LogError("[UMNContextManager] textDetectionPanelController is NULL — cannot display text detection results!");
            }
        }
    }
}
