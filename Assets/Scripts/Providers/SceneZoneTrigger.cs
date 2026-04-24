using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class SceneZoneTrigger : MonoBehaviour
{
    [Header("Target To Track")]
    [SerializeField] private Transform targetTransform;

    [Header("Zone Data")]
    [SerializeField] private string zoneId = "keller_hall_zone";
    [SerializeField] private string zoneTitle = "Keller Hall";

    [TextArea]
    [SerializeField] private string zoneDescription = "You are in the main Keller Hall corridor near several classrooms.";

    private BoxCollider boxCollider;
    private bool wasInside = false;

    private void Awake()
    {
        boxCollider = GetComponent<BoxCollider>();
        TryResolveTargetTransform();
    }

    private void Start()
    {
        Debug.Log("[SceneZoneTrigger] Bounds-check mode started on: " + gameObject.name);
    }

    private void Update()
    {
        if (targetTransform == null)
        {
            TryResolveTargetTransform();
        }

        if (targetTransform == null || boxCollider == null)
            return;

        Bounds worldBounds = boxCollider.bounds;
        Vector3 targetPosition = targetTransform.position;
        bool isInside = worldBounds.Contains(targetPosition);

        if (isInside && !wasInside)
        {
            Debug.Log("[SceneZoneTrigger] Target entered zone: " + targetTransform.name);

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
                Debug.Log("[SceneZoneTrigger] ContextManager updated with zone: " + zoneId);
            }
            else
            {
                Debug.LogWarning("[SceneZoneTrigger] No UMNContextManager found in scene.");
            }

            InputContextAssembler assembler = FindFirstObjectByType<InputContextAssembler>();
            if (assembler != null)
            {
                assembler.SetCurrentZone(zoneTitle, zoneDescription);
                Debug.Log("[SceneZoneTrigger] InputContextAssembler updated with zone: " + zoneId);
            }
            else
            {
                Debug.LogWarning("[SceneZoneTrigger] No InputContextAssembler found in scene.");
            }
        }

        if (!isInside && wasInside)
        {
            Debug.Log("[SceneZoneTrigger] Target left zone: " + targetTransform.name);
        }

        wasInside = isInside;
    }

    private void TryResolveTargetTransform()
    {
        if (Camera.main != null)
        {
            if (targetTransform == null ||
                (targetTransform.CompareTag("Player") && targetTransform.GetComponent<Camera>() == null))
            {
                targetTransform = Camera.main.transform;
                Debug.Log("[SceneZoneTrigger] Auto-assigned targetTransform from Main Camera: " + targetTransform.name);
                return;
            }
        }

        if (targetTransform != null)
            return;

        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer != null)
        {
            targetTransform = taggedPlayer.transform;
            Debug.Log("[SceneZoneTrigger] Auto-assigned targetTransform from Player tag: " + targetTransform.name);
            return;
        }
    }
}
