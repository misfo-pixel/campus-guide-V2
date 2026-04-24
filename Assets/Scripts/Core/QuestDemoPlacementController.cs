using System.Collections;
using UnityEngine;

public class QuestDemoPlacementController : MonoBehaviour
{
    [Header("Scene Roots")]
    [SerializeField] private Transform spriteSystemRoot;
    [SerializeField] private Transform environmentRoot;

    [Header("Placement")]
    [SerializeField] private bool keepSpriteInFrontOfUser = true;
    [SerializeField] private bool repositionEnvironmentOnStart = false;
    [SerializeField] private float spriteDistance = 0.9f;
    [SerializeField] private bool useCameraRelativeHeight = true;
    [SerializeField] private float cameraRelativeHeightOffset = -0.85f;
    [SerializeField] private float spriteHeightOffset = 1.55f;
    [SerializeField] private float environmentBackwardOffset = 2f;
    [SerializeField] private bool runInEditor = true;

    private Transform resolvedCameraTransform;

    private IEnumerator Start()
    {
        if (!Application.isEditor && !Application.isPlaying)
        {
            yield break;
        }

        if (!runInEditor && Application.isEditor)
        {
            yield break;
        }

        for (int frame = 0; frame < 60; frame++)
        {
            if (Camera.main != null)
            {
                resolvedCameraTransform = Camera.main.transform;
                PlaceDemo(resolvedCameraTransform);
                yield break;
            }

            yield return null;
        }

        Debug.LogWarning("[QuestDemoPlacementController] Could not find Camera.main to place demo roots.");
    }

    private void LateUpdate()
    {
        if (!keepSpriteInFrontOfUser)
        {
            return;
        }

        if (resolvedCameraTransform == null && Camera.main != null)
        {
            resolvedCameraTransform = Camera.main.transform;
        }

        if (resolvedCameraTransform == null || spriteSystemRoot == null)
        {
            return;
        }

        Vector3 flatForward = resolvedCameraTransform.forward;
        flatForward.y = 0f;

        if (flatForward.sqrMagnitude < 0.001f)
        {
            flatForward = Vector3.forward;
        }
        else
        {
            flatForward.Normalize();
        }

        Quaternion facingRotation = Quaternion.LookRotation(flatForward, Vector3.up);
        Vector3 targetSpritePosition = resolvedCameraTransform.position + (flatForward * spriteDistance);
        targetSpritePosition.y = ResolveSpriteHeight(resolvedCameraTransform.position.y);
        spriteSystemRoot.position = targetSpritePosition;
        spriteSystemRoot.rotation = facingRotation;
    }

    private void PlaceDemo(Transform cameraTransform)
    {
        Vector3 flatForward = cameraTransform.forward;
        flatForward.y = 0f;

        if (flatForward.sqrMagnitude < 0.001f)
        {
            flatForward = Vector3.forward;
        }
        else
        {
            flatForward.Normalize();
        }

        Quaternion facingRotation = Quaternion.LookRotation(flatForward, Vector3.up);

        if (spriteSystemRoot != null)
        {
            Vector3 targetSpritePosition = cameraTransform.position + (flatForward * spriteDistance);
            targetSpritePosition.y = ResolveSpriteHeight(cameraTransform.position.y);
            spriteSystemRoot.position = targetSpritePosition;
            spriteSystemRoot.rotation = facingRotation;
        }

        if (repositionEnvironmentOnStart && environmentRoot != null)
        {
            environmentRoot.position = cameraTransform.position - (flatForward * environmentBackwardOffset);
            environmentRoot.rotation = facingRotation;
        }

        Debug.Log("[QuestDemoPlacementController] Demo roots placed in front of headset.");
    }

    private float ResolveSpriteHeight(float cameraY)
    {
        if (useCameraRelativeHeight)
        {
            return cameraY + cameraRelativeHeightOffset;
        }

        return spriteHeightOffset;
    }
}