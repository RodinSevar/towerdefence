using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    [Header("Pan Settings")]
    public float panSpeed = 30f;
    public float edgeScrollThickness = 20f;
    public bool useEdgeScrolling = true;

    [Header("Zoom Settings")]
    public float scrollSpeed = 5000f;
    public float minY = 20f;
    public float maxY = 150f;

    [Header("Map Boundaries")]
    // Since our grid is 196x196, half of that is 98
    public Vector2 panLimitX = new Vector2(-98f, 98f);
    public Vector2 panLimitZ = new Vector2(-98f, 98f);

    private void Update()
    {
        // Don't move camera if Game Over
        if (GameManager.Instance != null && GameManager.Instance.IsGameOver())
            return;

        HandlePanning();
        HandleZooming();
    }

    private void HandlePanning()
    {
        Vector3 pos = transform.position;

        // Keyboard Input (New Input System)
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
            {
                pos.z += panSpeed * Time.deltaTime;
            }
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
            {
                pos.z -= panSpeed * Time.deltaTime;
            }
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
            {
                pos.x += panSpeed * Time.deltaTime;
            }
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
            {
                pos.x -= panSpeed * Time.deltaTime;
            }
        }

        // Edge Scrolling (Mouse - New Input System)
        if (useEdgeScrolling)
        {
            Mouse mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 mousePos = mouse.position.ReadValue();
                
                if (mousePos.y >= Screen.height - edgeScrollThickness)
                {
                    pos.z += panSpeed * Time.deltaTime;
                }
                if (mousePos.y <= edgeScrollThickness)
                {
                    pos.z -= panSpeed * Time.deltaTime;
                }
                if (mousePos.x >= Screen.width - edgeScrollThickness)
                {
                    pos.x += panSpeed * Time.deltaTime;
                }
                if (mousePos.x <= edgeScrollThickness)
                {
                    pos.x -= panSpeed * Time.deltaTime;
                }
            }
        }

        // Clamp to map boundaries
        pos.x = Mathf.Clamp(pos.x, panLimitX.x, panLimitX.y);
        pos.z = Mathf.Clamp(pos.z, panLimitZ.x, panLimitZ.y);

        transform.position = pos;
    }

    private void HandleZooming()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                // Normalize scroll value so trackpads (small values) and mice (120) both zoom at a usable speed
                float normalizedScroll = Mathf.Clamp(scroll, -1f, 1f);
                
                Vector3 pos = transform.position;
                
                // Move camera down/up along the Y axis
                pos.y -= normalizedScroll * scrollSpeed * Time.deltaTime;
                
                // Move camera slightly forward/backward as it zooms
                pos.z += normalizedScroll * scrollSpeed * 0.5f * Time.deltaTime;
                
                // Clamp Zoom Level
                pos.y = Mathf.Clamp(pos.y, minY, maxY);

                transform.position = pos;
            }
        }
    }
}
