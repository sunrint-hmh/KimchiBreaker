using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Mouse movement turns the view from a fixed seat. Escape releases the cursor.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public sealed class SeatedCameraLook : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float sensitivity = 0.12f;
    [SerializeField, Range(0f, 180f)] private float horizontalLimit = 80f;
    [SerializeField, Range(0f, 89f)] private float lookUpLimit = 55f;
    [SerializeField, Range(0f, 89f)] private float lookDownLimit = 55f;

    private Quaternion startingRotation;
    private float yaw;
    private float pitch;
    private bool looking;
    private bool captureOnNextFrame;
    private int captureFrame = -1;
    private CursorLockMode previousLock;
    private bool previousVisibility;

    public bool CanInteract => looking && Time.frameCount != captureFrame
        && Cursor.lockState == CursorLockMode.Locked;

    private void OnEnable()
    {
        startingRotation = transform.localRotation;
        yaw = pitch = 0f;
        captureOnNextFrame = true;
    }

    private void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null || !Application.isFocused || Time.timeScale == 0f)
        {
            captureOnNextFrame = false;
            ReleaseCursor();
            return;
        }

        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            captureOnNextFrame = false;
            ReleaseCursor();
            return;
        }

        // The Editor or operating system may release the pointer independently.
        if (looking && Cursor.lockState != CursorLockMode.Locked)
        {
            ReleaseCursor();
            return;
        }

        if (!looking)
        {
            if (!captureOnNextFrame && !mouse.leftButton.wasPressedThisFrame) return;
            captureOnNextFrame = false;
            previousLock = Cursor.lockState;
            previousVisibility = Cursor.visible;
            looking = true;
            captureFrame = Time.frameCount;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            // Ignore the capture frame, so locking never causes a view jump.
            return;
        }

        Vector2 delta = mouse.delta.ReadValue();
        // Mouse delta already represents this frame's movement; no deltaTime multiplier.
        yaw = Mathf.Clamp(yaw + delta.x * sensitivity, -horizontalLimit, horizontalLimit);
        pitch = Mathf.Clamp(pitch - delta.y * sensitivity, -lookUpLimit, lookDownLimit);
        transform.localRotation = Quaternion.AngleAxis(yaw, Vector3.up)
            * startingRotation * Quaternion.AngleAxis(pitch, Vector3.right);
    }

    private void ReleaseCursor()
    {
        if (!looking) return;
        Cursor.lockState = previousLock;
        Cursor.visible = previousVisibility;
        looking = false;
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused)
        {
            captureOnNextFrame = false;
            ReleaseCursor();
        }
    }

    private void OnDisable() => ReleaseCursor();
}
