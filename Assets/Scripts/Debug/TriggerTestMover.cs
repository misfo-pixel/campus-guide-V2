using UnityEngine;
using UnityEngine.InputSystem;

public class TriggerTestMover : MonoBehaviour
{
    [SerializeField] private float speed = 2f;

    private void Start()
    {
        Debug.Log("[TriggerTestMover] Script started on: " + gameObject.name);
    }

    private void Update()
    {
        float x = 0f;
        float z = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed) x = -1f;
            if (Keyboard.current.dKey.isPressed) x = 1f;
            if (Keyboard.current.wKey.isPressed) z = 1f;
            if (Keyboard.current.sKey.isPressed) z = -1f;
        }

        Vector3 movement = new Vector3(x, 0f, z) * speed * Time.deltaTime;
        transform.Translate(movement, Space.World);
    }
}