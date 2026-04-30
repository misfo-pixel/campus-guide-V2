using UnityEngine;

/// <summary>
/// Sets up the Meta Scene Model so that real-world surfaces (walls, desks, floors, doors)
/// get colliders that the gaze raycast can hit. This replaces the need for manually placed
/// invisible collider proxies.
///
/// Attach this to a GameObject in the scene. It will:
/// 1. Request Scene permission if not already granted
/// 2. Load the room model from the user's Space Setup
/// 3. Automatically add MeshColliders to scene anchors (planes/volumes)
///
/// Prerequisites:
/// - User must have completed Space Setup on their Quest headset
/// - AndroidManifest must include USE_SCENE and USE_ANCHOR_API permissions (already present)
/// - OVRManager must be in the scene (already present)
///
/// If the user hasn't done Space Setup, this script logs a warning and falls back
/// to spawning a large invisible collider plane in front of the user as a catch-all
/// so the gaze raycast has something to hit for passthrough camera capture.
/// </summary>
public class SceneColliderSetup : MonoBehaviour
{
    [Header("Scene Model")]
    [Tooltip("If true, automatically request scene capture if no room model is found.")]
    [SerializeField] private bool requestSceneCaptureOnMissing = false;

    [Header("Fallback")]
    [Tooltip("If true, spawn a large invisible collider plane when no scene model is available.")]
    [SerializeField] private bool useFallbackCollider = true;

    [Tooltip("Distance in meters to place the fallback collider in front of the camera.")]
    [SerializeField] private float fallbackDistance = 2.0f;

    [Tooltip("Size of the fallback collider plane in meters.")]
    [SerializeField] private float fallbackSize = 10.0f;

    [Header("Debug")]
    [SerializeField] private bool verboseLogging = true;

    private OVRSceneManager sceneManager;
    private GameObject fallbackColliderObject;
    private bool sceneModelLoaded;
    private bool sceneEventReceived;
    private float startTime;

    private void Start()
    {
        startTime = Time.time;
        Debug.Log("[SceneColliderSetup] Start() called. Setting up scene model...");
        SetupSceneModel();
    }

    private void Update()
    {
        // Keep the fallback collider in front of the user if scene model isn't loaded
        if (!sceneModelLoaded && fallbackColliderObject != null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                Vector3 forward = mainCam.transform.forward;
                forward.y = 0;
                if (forward.sqrMagnitude < 0.01f)
                    forward = Vector3.forward;
                forward.Normalize();

                fallbackColliderObject.transform.position = mainCam.transform.position + forward * fallbackDistance;
                fallbackColliderObject.transform.rotation = Quaternion.LookRotation(forward);
            }
        }

        // Timeout: if no scene event received within 5 seconds, spawn fallback anyway
        if (!sceneEventReceived && !sceneModelLoaded && useFallbackCollider && fallbackColliderObject == null)
        {
            if (Time.time - startTime > 5f)
            {
                Debug.LogWarning("[SceneColliderSetup] No scene model event received after 5s — spawning fallback collider now.");
                sceneEventReceived = true;
                SpawnFallbackCollider();
            }
        }
    }

    private void SetupSceneModel()
    {
        // Check if OVRSceneManager already exists in the scene
        sceneManager = FindObjectOfType<OVRSceneManager>();

        if (sceneManager == null)
        {
            if (verboseLogging)
                Debug.Log("[SceneColliderSetup] No OVRSceneManager found. Creating one...");

            GameObject sceneManagerObj = new GameObject("OVRSceneManager");
            sceneManager = sceneManagerObj.AddComponent<OVRSceneManager>();
        }

        // Subscribe to scene model events
        sceneManager.SceneModelLoadedSuccessfully += OnSceneModelLoaded;
        sceneManager.NoSceneModelToLoad += OnNoSceneModel;

        if (verboseLogging)
            Debug.Log("[SceneColliderSetup] OVRSceneManager configured. Waiting for scene model...");
    }

    private void OnSceneModelLoaded()
    {
        sceneModelLoaded = true;
        sceneEventReceived = true;
        if (verboseLogging)
            Debug.Log("[SceneColliderSetup] Scene model loaded successfully!");

        // Find all scene anchors and ensure they have colliders
        OVRSceneAnchor[] anchors = FindObjectsOfType<OVRSceneAnchor>();
        int colliderCount = 0;

        foreach (OVRSceneAnchor anchor in anchors)
        {
            // OVRSceneManager typically adds colliders automatically via prefabs,
            // but let's ensure every anchor has one
            if (anchor.GetComponent<Collider>() == null)
            {
                // For plane anchors (walls, floor, ceiling, desk surfaces)
                OVRScenePlane plane = anchor.GetComponent<OVRScenePlane>();
                if (plane != null)
                {
                    BoxCollider box = anchor.gameObject.AddComponent<BoxCollider>();
                    Vector2 dims = plane.Dimensions;
                    box.size = new Vector3(dims.x, dims.y, 0.02f);
                    box.center = Vector3.zero;
                    colliderCount++;

                    if (verboseLogging)
                        Debug.Log($"[SceneColliderSetup] Added BoxCollider to plane anchor '{anchor.gameObject.name}' ({dims.x:F2}x{dims.y:F2}m)");
                    continue;
                }

                // For volume anchors (furniture, objects)
                OVRSceneVolume volume = anchor.GetComponent<OVRSceneVolume>();
                if (volume != null)
                {
                    BoxCollider box = anchor.gameObject.AddComponent<BoxCollider>();
                    Vector3 dims = volume.Dimensions;
                    box.size = dims;
                    box.center = new Vector3(0, dims.y * 0.5f, 0);
                    colliderCount++;

                    if (verboseLogging)
                        Debug.Log($"[SceneColliderSetup] Added BoxCollider to volume anchor '{anchor.gameObject.name}' ({dims.x:F2}x{dims.y:F2}x{dims.z:F2}m)");
                    continue;
                }

                // For mesh anchors, add MeshCollider if there's a MeshFilter
                MeshFilter meshFilter = anchor.GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    MeshCollider meshCol = anchor.gameObject.AddComponent<MeshCollider>();
                    meshCol.sharedMesh = meshFilter.sharedMesh;
                    colliderCount++;

                    if (verboseLogging)
                        Debug.Log($"[SceneColliderSetup] Added MeshCollider to mesh anchor '{anchor.gameObject.name}'");
                }
            }
            else
            {
                colliderCount++;
            }
        }

        Debug.Log($"[SceneColliderSetup] Scene model ready: {anchors.Length} anchors, {colliderCount} with colliders.");

        // Remove fallback if scene model loaded
        RemoveFallbackCollider();
    }

    private void OnNoSceneModel()
    {
        sceneEventReceived = true;
        Debug.LogWarning("[SceneColliderSetup] No scene model available! The user needs to complete Space Setup on their Quest headset.");
        Debug.LogWarning("[SceneColliderSetup] Go to: Quest Settings > Physical Space > Space Setup > Set Up Room");

        if (requestSceneCaptureOnMissing)
        {
            Debug.Log("[SceneColliderSetup] Requesting scene capture...");
            sceneManager.RequestSceneCapture();
        }

        if (useFallbackCollider)
        {
            SpawnFallbackCollider();
        }
    }

    /// <summary>
    /// Spawns a large invisible collider plane in front of the user.
    /// This is a catch-all so the gaze raycast has something to hit even without
    /// a proper room model. The passthrough camera capture will still work because
    /// it captures whatever the camera sees, not the collider itself.
    /// </summary>
    private void SpawnFallbackCollider()
    {
        if (fallbackColliderObject != null)
            return;

        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogWarning("[SceneColliderSetup] No main camera found for fallback collider placement.");
            return;
        }

        fallbackColliderObject = new GameObject("GazeFallbackCollider");
        fallbackColliderObject.layer = 0; // Default layer

        // Place it in front of the user
        Vector3 forward = mainCam.transform.forward;
        forward.y = 0; // Keep it level
        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.forward;
        forward.Normalize();

        fallbackColliderObject.transform.position = mainCam.transform.position + forward * fallbackDistance;
        fallbackColliderObject.transform.rotation = Quaternion.LookRotation(forward);

        BoxCollider box = fallbackColliderObject.AddComponent<BoxCollider>();
        box.size = new Vector3(fallbackSize, fallbackSize, 0.01f);

        Debug.Log($"[SceneColliderSetup] Spawned fallback collider at {fallbackColliderObject.transform.position} ({fallbackSize}x{fallbackSize}m plane)");
    }

    private void RemoveFallbackCollider()
    {
        if (fallbackColliderObject != null)
        {
            Destroy(fallbackColliderObject);
            fallbackColliderObject = null;

            if (verboseLogging)
                Debug.Log("[SceneColliderSetup] Removed fallback collider (scene model is now available).");
        }
    }

    private void OnDestroy()
    {
        if (sceneManager != null)
        {
            sceneManager.SceneModelLoadedSuccessfully -= OnSceneModelLoaded;
            sceneManager.NoSceneModelToLoad -= OnNoSceneModel;
        }

        RemoveFallbackCollider();
    }
}
