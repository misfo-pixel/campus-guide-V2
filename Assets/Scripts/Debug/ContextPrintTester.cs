using UnityEngine;
using UnityEngine.InputSystem;

public class ContextPrintTester : MonoBehaviour
{
    [SerializeField] private InputContextAssembler inputContextAssembler;

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            if (inputContextAssembler != null)
            {
                inputContextAssembler.PrintFullContext();
            }
            else
            {
                Debug.LogWarning("[ContextPrintTester] No InputContextAssembler assigned.");
            }
        }
    }
}