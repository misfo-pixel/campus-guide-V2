using UnityEngine;

public class ProximityZoneTrigger : MonoBehaviour
{
    [Header("Target To Track")]
    [SerializeField] private Transform targetTransform;

    [Header("Runtime Placement")]
    [SerializeField] private bool anchorNearUserOnStart = true;
    [Tooltip("Offset from user: X=left/right, Y=up/down, Z=forward")]
    [SerializeField] private Vector3 startOffset = new Vector3(0f, 0f, 4f);  // 4 meters in front

    [Header("GPS Validation")]
    [SerializeField] private bool requireGPSBuildingMatch = true;
    [SerializeField] private string requiredBuilding = "Keller Hall";

    [Header("Zone Data")]
    [SerializeField] private string zoneId = "keller_3_180";
    [SerializeField] private string zoneTitle = "Keller 3-180";

    [TextArea]
    [SerializeField] private string zoneDescription = "You are near classroom 3-180 in Keller Hall.";

    [Header("Immediate Room Message")]
    [SerializeField] private bool showImmediateRoomMessage = true;
    [SerializeField] private string immediateTitle = "Keller Hall";
    [SerializeField][TextArea] private string immediateBody = "You are near room 3-180 in Keller Hall.";
    [SerializeField][TextArea] private string proximityQueryOverride = "Am I at the right room?";

    [Header("Proximity Settings")]
    [SerializeField] private float triggerRadius = 5f;
    [SerializeField] private bool autoTriggerLLM = true;
    [SerializeField] private bool showDebugSphere = true;

    private bool wasNearby = false;
    private bool hasAnchoredAtRuntime = false;

    private void Start()
    {
        TryResolveTargetTransform();

        Debug.Log("[ProximityZoneTrigger] Started on: " + gameObject.name + " | Radius: " + triggerRadius + "m");
    }

    private void Update()
    {
        if (targetTransform == null)
        {
            TryResolveTargetTransform();
        }

        if (anchorNearUserOnStart && !hasAnchoredAtRuntime && targetTransform != null)
        {
            // Place trigger in front of where user is facing
            Vector3 forward = targetTransform.forward;
            forward.y = 0; // Keep on horizontal plane
            forward.Normalize();
            
            Vector3 right = targetTransform.right;
            right.y = 0;
            right.Normalize();

            Vector3 anchoredPosition = targetTransform.position 
                + forward * startOffset.z   // Forward/back
                + right * startOffset.x     // Left/right
                + Vector3.up * startOffset.y; // Up/down
            
            anchoredPosition.y = 0; // Ground level
            transform.position = anchoredPosition;
            hasAnchoredAtRuntime = true;
            Debug.Log("[ProximityZoneTrigger] Anchored 4m in front of user at: " + transform.position);
        }

        if (targetTransform == null)
        {
            return;
        }

        Vector3 triggerPosition = transform.position;
        Vector3 targetPosition = targetTransform.position;
        triggerPosition.y = 0f;
        targetPosition.y = 0f;

        float distance = Vector3.Distance(triggerPosition, targetPosition);
        bool isNearby = distance <= triggerRadius;

        // Check GPS building match if required
        if (isNearby && requireGPSBuildingMatch && !IsAtRequiredBuilding())
        {
            // GPS says we're not at the required building, don't trigger
            return;
        }

        if (isNearby && !wasNearby)
        {
            Debug.Log("[ProximityZoneTrigger] Player is within " + triggerRadius + "m of " + zoneTitle + " (distance: " + distance.ToString("F2") + "m)");
            OnEnteredProximity();
        }

        if (!isNearby && wasNearby)
        {
            Debug.Log("[ProximityZoneTrigger] Player left proximity of " + zoneTitle);
        }

        wasNearby = isNearby;
    }

    private void OnEnteredProximity()
    {
        if (showImmediateRoomMessage)
        {
            ApplyImmediateRoomMessage();
        }

        SceneZoneData zoneData = new SceneZoneData
        {
            zoneId = zoneId,
            title = zoneTitle,
            description = zoneDescription
        };

        UMNContextManager contextManager = FindFirstObjectByType<UMNContextManager>();
        if (contextManager != null)
        {
            contextManager.SetSceneZone(zoneData);
        }

        InputContextAssembler assembler = FindFirstObjectByType<InputContextAssembler>();
        if (assembler != null)
        {
            assembler.SetCurrentZone(zoneTitle, zoneDescription);
        }

        DemoContextProvider demoContext = FindFirstObjectByType<DemoContextProvider>();
        if (demoContext != null)
        {
            demoContext.SetDetection(zoneTitle, zoneDescription);

            if (!string.IsNullOrWhiteSpace(proximityQueryOverride))
            {
                demoContext.SetTransientUserQueryOverride(proximityQueryOverride);
            }
        }

        // Update user location data with room info
        UserLocationDataManager locationData = FindFirstObjectByType<UserLocationDataManager>();
        if (locationData != null)
        {
            string building = string.IsNullOrWhiteSpace(immediateTitle) ? null : immediateTitle;
            locationData.UpdateRoomLocation(zoneTitle, building);
        }

        if (autoTriggerLLM)
        {
            LLMDemoRunner runner = FindFirstObjectByType<LLMDemoRunner>();
            if (runner != null)
            {
                Debug.Log("[ProximityZoneTrigger] Auto-triggering LLM for zone: " + zoneTitle);
                runner.RunDemo();
            }
        }
    }

    private void ApplyImmediateRoomMessage()
    {
        UMNContextManager contextManager = FindFirstObjectByType<UMNContextManager>();
        if (contextManager == null)
        {
            return;
        }

        SpriteStateData state = new SpriteStateData
        {
            Mode = SpriteMode.Info,
            Title = string.IsNullOrWhiteSpace(immediateTitle) ? zoneTitle : immediateTitle,
            Body = string.IsNullOrWhiteSpace(immediateBody) ? zoneDescription : immediateBody,
            ShowPanel = true
        };

        contextManager.SetState(state);
    }

    private void TryResolveTargetTransform()
    {
        if (targetTransform != null) return;

        if (Camera.main != null)
        {
            targetTransform = Camera.main.transform;
            return;
        }

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null)
        {
            targetTransform = taggedPlayer.transform;
            return;
        }
    }

    /// <summary>
    /// Check if GPS says we're at the required building
    /// </summary>
    private bool IsAtRequiredBuilding()
    {
        if (string.IsNullOrEmpty(requiredBuilding))
        {
            return true; // No building required, always pass
        }

        // Check UserLocationDataManager first
        UserLocationDataManager userData = UserLocationDataManager.Instance;
        if (userData != null && userData.CurrentRecord != null)
        {
            string currentBuilding = userData.CurrentRecord.currentBuilding;
            if (!string.IsNullOrEmpty(currentBuilding))
            {
                bool match = currentBuilding.Contains(requiredBuilding) || requiredBuilding.Contains(currentBuilding);
                Debug.Log($"[ProximityZoneTrigger] GPS building check: '{currentBuilding}' vs required '{requiredBuilding}' = {match}");
                return match;
            }
        }

        // Check GPSLocationService as fallback
        GPSLocationService gps = GPSLocationService.Instance;
        if (gps != null && gps.HasLocation && gps.CurrentLocation != null)
        {
            string gpsBuilding = gps.CurrentLocation.buildingHint;
            if (!string.IsNullOrEmpty(gpsBuilding))
            {
                bool match = gpsBuilding.Contains(requiredBuilding) || requiredBuilding.Contains(gpsBuilding);
                Debug.Log($"[ProximityZoneTrigger] GPS building check: '{gpsBuilding}' vs required '{requiredBuilding}' = {match}");
                return match;
            }
        }

        Debug.Log("[ProximityZoneTrigger] No GPS building data available, blocking trigger");
        return false; // No GPS data, don't trigger
    }

    private void OnDrawGizmos()
    {
        if (!showDebugSphere) return;

        Gizmos.color = new Color(0f, 1f, 0.5f, 0.25f);
        Gizmos.DrawSphere(transform.position, triggerRadius);
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, triggerRadius);
    }
}
