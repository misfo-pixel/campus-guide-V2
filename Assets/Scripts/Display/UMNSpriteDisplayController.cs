using UnityEngine;

public class UMNSpriteDisplayController : MonoBehaviour
{
    [Header("Optional Visual References")]
    [SerializeField] private GameObject visualRoot;

    private SpriteStateData currentState;

    public void ApplyState(SpriteStateData state)
    {
        currentState = state;

        if (visualRoot != null)
        {
            visualRoot.SetActive(true);
        }

        // Future expansion:
        // - switch animation by state.Mode
        // - change glow/material
        // - rotate toward target
        // - play speaking/listening visual feedback
    }
}
