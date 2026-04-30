using System;
using UnityEngine;

public class GazeRaycastController : MonoBehaviour
{
    [Header("Eye Tracking")]
    [SerializeField] private OVREyeGaze eyeGaze;

    [Header("Raycast Settings")]
    [SerializeField] private float maxRaycastDistance = 10.0f;
    [SerializeField] private LayerMask raycastLayerMask;

    [Header("Dwell Settings")]
    [SerializeField] private float dwellTimeThreshold = 1.5f;
    [SerializeField] private float dwellPositionTolerance = 0.05f;

    [Header("Cooldown")]
    [SerializeField] private float cooldownAfterCapture = 3.0f;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    public event Action<GazeHitData> OnDwellComplete;

    private bool isEnabled = true;
    private float accumulatedDwellTime;
    private float cooldownTimer;
    private bool isInCooldown;
    private Vector3 previousHitPoint;
    private bool hasPreviousHit;
    private GazeHitData currentGazeHit;
    private bool isTracking;

    public bool IsTracking => isTracking;

    public GazeHitData CurrentGazeHit => currentGazeHit;

    public float CurrentDwellProgress => Mathf.Clamp01(accumulatedDwellTime / dwellTimeThreshold);

    public void SetEnabled(bool enabled)
    {
        isEnabled = enabled;
        if (!enabled)
        {
            ResetDwell();
        }
    }

    public void ResetDwell()
    {
        accumulatedDwellTime = 0f;
        hasPreviousHit = false;
        currentGazeHit = null;
        isTracking = false;
    }

    private void Awake()
    {
        Debug.Log("[GazeRaycastController] Awake() called on: " + gameObject.name);
    }

    private float logTimer;

    private void Start()
    {
        if (eyeGaze == null)
        {
            Debug.LogError("[GazeRaycastController] eyeGaze is NOT assigned! Drag an OVREyeGaze component (e.g. LeftEyeAnchor) into this field.");
        }
        else
        {
            Debug.Log($"[GazeRaycastController] Started. EyeGaze: {eyeGaze.gameObject.name}, EyeId: {eyeGaze.Eye}, LayerMask: {raycastLayerMask.value}, maxDist: {maxRaycastDistance}");
        }

        // --- SCENE COLLIDER DIAGNOSTIC ---
        // Find all colliders in the scene and report them so we know what the raycast can hit
        Collider[] allColliders = FindObjectsOfType<Collider>();
        Debug.Log($"[GazeRaycastController] === SCENE COLLIDER AUDIT: {allColliders.Length} colliders found ===");
        if (allColliders.Length == 0)
        {
            Debug.LogError("[GazeRaycastController] NO COLLIDERS IN SCENE! Raycasts will never hit anything. Add BoxCollider/MeshCollider to your objects.");
        }
        else
        {
            for (int i = 0; i < allColliders.Length; i++)
            {
                Collider c = allColliders[i];
                string layerName = LayerMask.LayerToName(c.gameObject.layer);
                bool inMask = raycastLayerMask == (raycastLayerMask | (1 << c.gameObject.layer));
                Debug.Log($"[GazeRaycastController]   [{i}] '{c.gameObject.name}' — type={c.GetType().Name}, layer={c.gameObject.layer} ({layerName}), inRaycastMask={inMask}, enabled={c.enabled}, activeInHierarchy={c.gameObject.activeInHierarchy}, bounds={c.bounds.center} size={c.bounds.size}");
            }
        }

        // Check for MeshRenderers without colliders (common mistake)
        MeshRenderer[] allRenderers = FindObjectsOfType<MeshRenderer>();
        int missingColliderCount = 0;
        foreach (MeshRenderer mr in allRenderers)
        {
            if (mr.GetComponent<Collider>() == null)
            {
                missingColliderCount++;
                if (missingColliderCount <= 10) // cap to avoid log spam
                {
                    Debug.LogWarning($"[GazeRaycastController] MISSING COLLIDER: '{mr.gameObject.name}' has a MeshRenderer but NO Collider — raycast will pass through it! Add a BoxCollider or MeshCollider.");
                }
            }
        }
        if (missingColliderCount > 0)
        {
            Debug.LogWarning($"[GazeRaycastController] Total objects with MeshRenderer but no Collider: {missingColliderCount}");
        }
        Debug.Log("[GazeRaycastController] === END SCENE COLLIDER AUDIT ===");

        // If no usable colliders exist, spawn a fallback collider immediately
        // so the gaze raycast has something to hit in passthrough MR
        bool hasUsableCollider = false;
        foreach (Collider c in allColliders)
        {
            if (c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger &&
                c.gameObject.name != "XR Origin") // skip player's own collider
            {
                hasUsableCollider = true;
                break;
            }
        }

        if (!hasUsableCollider)
        {
            Debug.LogWarning("[GazeRaycastController] No usable colliders found! Spawning fallback gaze target...");
            SpawnFallbackGazeTarget();
        }
    }

    private GameObject fallbackGazeTarget;

    private void SpawnFallbackGazeTarget()
    {
        fallbackGazeTarget = new GameObject("GazeFallbackTarget");
        fallbackGazeTarget.layer = 0; // Default layer
        BoxCollider box = fallbackGazeTarget.AddComponent<BoxCollider>();
        box.size = new Vector3(20f, 20f, 0.01f); // Large thin plane
        Debug.Log("[GazeRaycastController] Fallback gaze target spawned (20x20m plane). Will follow gaze direction.");
    }

    private void UpdateFallbackPosition()
    {
        if (fallbackGazeTarget == null || eyeGaze == null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        // Place the fallback plane 2m in front of the camera, facing the camera
        Vector3 forward = cam.transform.forward;
        fallbackGazeTarget.transform.position = cam.transform.position + forward * 2f;
        fallbackGazeTarget.transform.rotation = Quaternion.LookRotation(forward);
    }

    private void Update()
    {
        UpdateFallbackPosition();

        if (!isEnabled || eyeGaze == null)
        {
            isTracking = false;
            return;
        }

        if (isInCooldown)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0f)
            {
                isInCooldown = false;
                if (verboseLogging) Debug.Log("[GazeRaycastController] Cooldown ended.");
            }
        }

        // Check if OVREyeGaze is actually providing valid tracking data
        if (!eyeGaze.EyeTrackingEnabled)
        {
            if (verboseLogging && logTimer >= 2f)
            {
                Debug.LogWarning("[GazeRaycastController] EyeTrackingEnabled=FALSE — eye tracking is not active. Check OVRManager eye tracking settings and headset permissions.");
            }
        }
        if (eyeGaze.Confidence < 0.5f)
        {
            if (verboseLogging && logTimer >= 2f)
            {
                Debug.LogWarning($"[GazeRaycastController] Low eye tracking confidence: {eyeGaze.Confidence:F2}. Gaze direction may be unreliable.");
            }
        }

        Vector3 gazeOrigin = eyeGaze.transform.position;
        Vector3 gazeDirection = eyeGaze.transform.forward;

        // Draw debug ray in Scene view (visible in Editor and with Immersive Debugger)
        Debug.DrawRay(gazeOrigin, gazeDirection * maxRaycastDistance, Color.red);

        // Log every 2 seconds to avoid spam
        logTimer += Time.deltaTime;
        if (verboseLogging && logTimer >= 2f)
        {
            logTimer = 0f;
            Debug.Log($"[GazeRaycastController] Gaze origin: {gazeOrigin}, direction: {gazeDirection}, layerMask: {raycastLayerMask.value}");
        }

        RaycastHit hit;
        bool didHit = Physics.Raycast(gazeOrigin, gazeDirection, out hit, maxRaycastDistance, raycastLayerMask);

        if (verboseLogging && logTimer < 0.1f && !didHit)
        {
            // Log raycast misses so we can tell if the ray never hits anything
            Debug.Log($"[GazeRaycastController] MISS — no collider hit. origin={gazeOrigin}, dir={gazeDirection}, maxDist={maxRaycastDistance}, layerMask={raycastLayerMask.value}");

            // Also do an unmasked raycast to see if the issue is the layer mask
            RaycastHit unmaskHit;
            if (Physics.Raycast(gazeOrigin, gazeDirection, out unmaskHit, maxRaycastDistance))
            {
                Debug.LogWarning($"[GazeRaycastController] UNMASKED HIT (layer mask is filtering it out!): '{unmaskHit.collider.gameObject.name}' on layer {unmaskHit.collider.gameObject.layer} ({LayerMask.LayerToName(unmaskHit.collider.gameObject.layer)})");
            }
            else
            {
                Debug.Log("[GazeRaycastController] UNMASKED also missed — no colliders in gaze path at all.");
            }
        }

        if (didHit)
        {
            isTracking = true;

            if (verboseLogging && logTimer < 0.1f)
            {
                Debug.Log($"[GazeRaycastController] HIT: '{hit.collider.gameObject.name}' at {hit.point}, distance: {hit.distance:F2}m, layer: {hit.collider.gameObject.layer} ({LayerMask.LayerToName(hit.collider.gameObject.layer)}), hasCollider: {hit.collider != null}");
            }

            currentGazeHit = new GazeHitData
            {
                hitPoint = hit.point,
                hitNormal = hit.normal,
                gazeOrigin = gazeOrigin,
                gazeDirection = gazeDirection,
                distance = hit.distance,
                hitObjectName = hit.collider != null ? hit.collider.gameObject.name : string.Empty
            };

            if (hasPreviousHit && Vector3.Distance(hit.point, previousHitPoint) <= dwellPositionTolerance)
            {
                accumulatedDwellTime += Time.deltaTime;

                if (!isInCooldown && accumulatedDwellTime >= dwellTimeThreshold)
                {
                    Debug.Log($"[GazeRaycastController] DWELL COMPLETE on '{currentGazeHit.hitObjectName}' — triggering capture! (subscribers: {OnDwellComplete?.GetInvocationList()?.Length ?? 0})");
                    OnDwellComplete?.Invoke(currentGazeHit);
                    EnterCooldown();
                }
                else if (verboseLogging && logTimer < 0.1f)
                {
                    Debug.Log($"[GazeRaycastController] Dwelling on '{currentGazeHit.hitObjectName}': {accumulatedDwellTime:F2}s / {dwellTimeThreshold:F1}s ({CurrentDwellProgress * 100f:F0}%), cooldown={isInCooldown}");
                }
            }
            else
            {
                if (verboseLogging && hasPreviousHit && logTimer < 0.1f)
                {
                    float movedDist = Vector3.Distance(hit.point, previousHitPoint);
                    Debug.Log($"[GazeRaycastController] Dwell RESET — gaze moved {movedDist:F4}m (tolerance={dwellPositionTolerance:F4}m). Was dwelling {accumulatedDwellTime:F2}s on previous point.");
                }
                accumulatedDwellTime = 0f;
            }

            previousHitPoint = hit.point;
            hasPreviousHit = true;
        }
        else
        {
            isTracking = false;
            currentGazeHit = null;
            accumulatedDwellTime = 0f;
            hasPreviousHit = false;
        }
    }

    private void EnterCooldown()
    {
        isInCooldown = true;
        cooldownTimer = cooldownAfterCapture;
        accumulatedDwellTime = 0f;
    }
}
