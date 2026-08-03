using UnityEngine;
using UnityEngine.EventSystems;

public class MinimapInteraction : MonoBehaviour, IPointerDownHandler, IDragHandler
{
    private RectTransform rectTransform;
    
    // Based on GridManager (98 world units is the boundary)
    private float gridHalfSize = 98f;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        HandleMinimapInput(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        HandleMinimapInput(eventData);
    }

    private void HandleMinimapInput(PointerEventData eventData)
    {
        // Get local click position inside the RectTransform
        RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform, eventData.position, eventData.pressEventCamera, out Vector2 localPoint);
        
        // Rect is 100x100, anchored (0,0) to (0,1), offsetMin (0,0) to offsetMax(100,0).
        // Since it's bottom-left anchored in the layout group, localPoint might be relative to center if pivot is 0.5, 0.5.
        // Let's normalize it to 0-1 based on the rect's rect.
        
        Rect rect = rectTransform.rect;
        
        // Convert to 0 to 1 range
        float normalizedX = (localPoint.x - rect.xMin) / rect.width;
        float normalizedY = (localPoint.y - rect.yMin) / rect.height;
        
        // Clamp 0 to 1
        normalizedX = Mathf.Clamp01(normalizedX);
        normalizedY = Mathf.Clamp01(normalizedY);
        
        // Convert to World coordinates (-98 to 98)
        float worldX = Mathf.Lerp(-gridHalfSize, gridHalfSize, normalizedX);
        float worldZ = Mathf.Lerp(-gridHalfSize, gridHalfSize, normalizedY);
        
        MoveCameraTo(worldX, worldZ);
    }

    private void MoveCameraTo(float worldX, float worldZ)
    {
        Camera mainCam = Camera.main;
        if (mainCam == null) return;

        Vector3 camPos = mainCam.transform.position;
        
        // The camera looks down at an angle (70 degrees).
        // To center the view on (worldX, worldZ), we must offset the camera's Z position backwards.
        // tan(theta) = Opposite / Adjacent
        // tan(70) = Height(Y) / Distance(Z)
        // Distance(Z) = Height(Y) / tan(70)
        
        float angleRad = mainCam.transform.eulerAngles.x * Mathf.Deg2Rad;
        float zOffset = camPos.y / Mathf.Tan(angleRad);
        
        Vector3 newPos = new Vector3(worldX, camPos.y, worldZ - zOffset);
        mainCam.transform.position = newPos;
    }
}
