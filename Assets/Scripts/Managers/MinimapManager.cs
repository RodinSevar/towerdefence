using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class MinimapManager : Singleton<MinimapManager>
{
    [SerializeField] private RectTransform minimapContainer;
    
    private Dictionary<Transform, RectTransform> unitDots = new Dictionary<Transform, RectTransform>();
    
    // Grid bounds
    private float gridHalfSize = 98f;
    private float minimapSize => minimapContainer != null ? minimapContainer.rect.width : 100f;

    public void RegisterUnit(Transform unitTransform, bool isEnemy)
    {
        if (minimapContainer == null) return;

        GameObject dotObj = new GameObject("Dot");
        dotObj.transform.SetParent(minimapContainer, false);
        
        Image dotImage = dotObj.AddComponent<Image>();
        dotImage.color = isEnemy ? Color.red : Color.blue;
        
        RectTransform dotRect = dotObj.GetComponent<RectTransform>();
        dotRect.sizeDelta = isEnemy ? new Vector2(3, 3) : new Vector2(4, 4); // Enemies slightly smaller
        dotRect.anchorMin = new Vector2(0.5f, 0.5f);
        dotRect.anchorMax = new Vector2(0.5f, 0.5f);
        
        unitDots[unitTransform] = dotRect;
        UpdateDotPosition(unitTransform, dotRect);
    }

    public void UnregisterUnit(Transform unitTransform)
    {
        if (unitDots.TryGetValue(unitTransform, out RectTransform dotRect))
        {
            if (dotRect != null && dotRect.gameObject != null)
            {
                Destroy(dotRect.gameObject);
            }
            unitDots.Remove(unitTransform);
        }
    }

    private void Update()
    {
        // Update all enemy positions (towers don't move, but we can just update all for simplicity)
        List<Transform> keysToRemove = null;
        
        foreach (var kvp in unitDots)
        {
            Transform unitTransform = kvp.Key;
            RectTransform dotRect = kvp.Value;

            if (unitTransform == null)
            {
                if (keysToRemove == null) keysToRemove = new List<Transform>();
                keysToRemove.Add(unitTransform);
                if (dotRect != null) Destroy(dotRect.gameObject);
                continue;
            }

            UpdateDotPosition(unitTransform, dotRect);
        }

        if (keysToRemove != null)
        {
            foreach (Transform t in keysToRemove)
            {
                unitDots.Remove(t);
            }
        }
    }

    private void UpdateDotPosition(Transform unitTransform, RectTransform dotRect)
    {
        // Map World coordinates to Minimap coordinates
        // World: -98 to 98
        // Minimap: -50 to 50 (since anchored to center 0.5)
        
        float mapRatio = (minimapSize / 2f) / gridHalfSize;
        
        float mapX = unitTransform.position.x * mapRatio;
        float mapY = unitTransform.position.z * mapRatio; // Map world Z to UI Y
        
        dotRect.anchoredPosition = new Vector2(mapX, mapY);
    }
}
