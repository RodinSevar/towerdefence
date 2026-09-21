using UnityEngine;
using UnityEngine.UI;

public class MinimapCameraView : MonoBehaviour
{
    private RectTransform[] lines = new RectTransform[4];
    
    private float gridHalfSize = 98f;
    private float minimapSize => ((RectTransform)transform).rect.width;
    private float lineThickness = 2f;

    private void Start()
    {
        for (int i = 0; i < 4; i++)
        {
            GameObject lineObj = new GameObject("CameraLine_" + i);
            lineObj.transform.SetParent(transform, false);
            
            Image img = lineObj.AddComponent<Image>();
            img.color = Color.white;
            
            RectTransform rect = lineObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f); // Pivot at start of line
            
            lines[i] = rect;
        }
    }

    private void Update()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Plane groundPlane = new Plane(Vector3.up, Vector3.zero);
        
        // 4 corners of the viewport
        Vector3[] viewportPoints = new Vector3[]
        {
            new Vector3(0, 0, 0), // Bottom Left
            new Vector3(0, 1, 0), // Top Left
            new Vector3(1, 1, 0), // Top Right
            new Vector3(1, 0, 0)  // Bottom Right
        };
        
        Vector2[] minimapPoints = new Vector2[4];

        for (int i = 0; i < 4; i++)
        {
            Ray ray = cam.ViewportPointToRay(viewportPoints[i]);
            if (groundPlane.Raycast(ray, out float enter))
            {
                Vector3 hitPoint = ray.GetPoint(enter);
                minimapPoints[i] = WorldToMinimapPosition(hitPoint);
            }
            else
            {
                // Fallback if camera somehow looks above horizon (shouldn't happen at 70 deg)
                minimapPoints[i] = Vector2.zero;
            }
        }

        // Draw 4 lines connecting the corners
        DrawLine(lines[0], minimapPoints[0], minimapPoints[1]); // Left edge
        DrawLine(lines[1], minimapPoints[1], minimapPoints[2]); // Top edge
        DrawLine(lines[2], minimapPoints[2], minimapPoints[3]); // Right edge
        DrawLine(lines[3], minimapPoints[3], minimapPoints[0]); // Bottom edge
    }

    private Vector2 WorldToMinimapPosition(Vector3 worldPos)
    {
        float mapRatio = (minimapSize / 2f) / gridHalfSize;
        float mapX = worldPos.x * mapRatio;
        float mapY = worldPos.z * mapRatio;
        
        return new Vector2(mapX, mapY);
    }

    private void DrawLine(RectTransform lineRect, Vector2 pointA, Vector2 pointB)
    {
        lineRect.anchoredPosition = pointA;
        
        Vector2 dir = pointB - pointA;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        
        lineRect.localRotation = Quaternion.Euler(0, 0, angle);
        lineRect.sizeDelta = new Vector2(dir.magnitude, lineThickness);
    }
}
