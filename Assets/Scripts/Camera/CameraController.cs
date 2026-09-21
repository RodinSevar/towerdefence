using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    /// <summary>Start height and pitch. Like the original game, the camera starts at its widest view and can only zoom in.</summary>
    public const float DefaultHeight = 22f;
    public const float DefaultPitch = 62f;

    [Header("Pan Settings")]
    public float panSpeed = 30f;
    public float edgeScrollThickness = 20f;
    public bool useEdgeScrolling = true;
    public float dragPanSpeed = 0.1f;

    [Header("Zoom Settings")]
    public float scrollSpeed = 5000f;
    public float minY = 8f;
    [Tooltip("Highest the camera may go (the start height, so it cannot zoom out past the default view)")]
    public float maxY = DefaultHeight;

    [Header("Map Boundaries")]
    // The map is 192x192 cells, centred on the origin
    public Vector2 panLimitX = new Vector2(-96f, 96f);
    public Vector2 panLimitZ = new Vector2(-96f, 96f);

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

        // Mouse Panning (New Input System)
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            // Middle-Click Drag Panning
            if (mouse.middleButton.isPressed)
            {
                // mouse.delta is pixels moved this frame
                Vector2 delta = mouse.delta.ReadValue();
                
                // Scale movement by current height so it feels consistent at any zoom level
                float heightMultiplier = transform.position.y / 50f;
                
                // User specifically requested: "moving up should move the camera up"
                // delta.y is positive when mouse moves up, delta.x is positive when mouse moves right
                pos.x += delta.x * dragPanSpeed * heightMultiplier;
                pos.z += delta.y * dragPanSpeed * heightMultiplier;
            }
            // Edge Scrolling
            else if (useEdgeScrolling)
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
                
                // Desired change in Height (Y)
                float zoomY = normalizedScroll * scrollSpeed * Time.deltaTime;
                
                // Cast a ray from the cursor to the ground plane to find what world point we are looking at
                Ray ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());
                Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
                
                if (groundPlane.Raycast(ray, out float enter))
                {
                    Vector3 hitPoint = ray.GetPoint(enter);
                    
                    // We want to move along the line from the camera to the hitPoint.
                    // The ratio of our desired Y change to our total current Y height tells us exactly how far to move.
                    float currentY = transform.position.y;
                    
                    // Calculate proposed Y and clamp it
                    float proposedY = currentY - zoomY;
                    proposedY = Mathf.Clamp(proposedY, minY, maxY);
                    
                    // Recalculate the actual Y change after clamping
                    float actualZoomY = currentY - proposedY;
                    
                    // If we are already at the zoom limit, actualZoomY is 0, so we don't move
                    if (Mathf.Abs(actualZoomY) > 0.001f)
                    {
                        float ratio = actualZoomY / currentY;
                        
                        // Vector from camera to hit point
                        Vector3 toHit = hitPoint - transform.position;
                        
                        // Move the camera by that ratio
                        Vector3 proposedPos = transform.position + (toHit * ratio);
                        
                        // Clamp to map boundaries so zooming at edges doesn't push you out of bounds
                        proposedPos.x = Mathf.Clamp(proposedPos.x, panLimitX.x, panLimitX.y);
                        proposedPos.z = Mathf.Clamp(proposedPos.z, panLimitZ.x, panLimitZ.y);

                        transform.position = proposedPos;
                    }
                }
            }
        }
    }
}
