using UnityEngine;

public class SpriteActionController : MonoBehaviour
{
    [SerializeField] private Transform visualRoot;

    private string currentAction = "idle";
    private Vector3 baseScale;
    private Vector3 baseLocalPos;
    private Vector3 baseEuler;

    private void Start()
    {
        if (visualRoot == null)
            visualRoot = transform;

        baseScale = visualRoot.localScale;
        baseLocalPos = visualRoot.localPosition;
        baseEuler = visualRoot.localEulerAngles;
    }

    private void Update()
    {
        if (visualRoot == null) return;

        visualRoot.localScale = baseScale;
        visualRoot.localPosition = baseLocalPos;
        visualRoot.localEulerAngles = baseEuler;

        float t = Time.time;

        switch (currentAction)
        {
            case "greet":
                visualRoot.localPosition = baseLocalPos + new Vector3(0f, Mathf.Sin(t * 4f) * 0.05f, 0f);
                break;

            case "explain":
                visualRoot.localEulerAngles = baseEuler + new Vector3(0f, Mathf.Sin(t * 3f) * 12f, 0f);
                break;

            case "alert":
                visualRoot.localScale = baseScale * (1f + Mathf.Abs(Mathf.Sin(t * 5f)) * 0.12f);
                break;

            case "point":
                visualRoot.localEulerAngles = baseEuler + new Vector3(0f, 18f, 0f);
                break;

            default:
                break;
        }
    }

    public void PlayAction(string action)
    {
        currentAction = string.IsNullOrEmpty(action) ? "idle" : action.ToLowerInvariant();
        Debug.Log("[SpriteActionController] Action = " + currentAction);
    }
}