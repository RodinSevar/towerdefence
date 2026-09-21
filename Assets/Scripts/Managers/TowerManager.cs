using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class TowerManager : Singleton<TowerManager>
{
    private List<Tower> activeTowers = new List<Tower>();
    private Tower selectedTowerPrefab = null;
    private bool isPlacingTower = false;
    
    private GameObject previewGhost;
    private GameObject previewFloor;
    private Renderer[] ghostRenderers;
    private Renderer floorRenderer;
    private Vector2Int lastHoveredCell = new Vector2Int(-999, -999);
    private bool lastHoverIsValid = false;

    public System.Action<Tower> OnTowerPlaced;

    private void Update()
    {
        if (isPlacingTower)
        {
            UpdatePreview();

            // Shift+drag keeps placing towers; otherwise one click = one attempt
            bool clicked = Mouse.current.leftButton.wasPressedThisFrame
                || (Keyboard.current.shiftKey.isPressed && Mouse.current.leftButton.isPressed);
            if (clicked)
            {
                // Ignore clicks over UI
                if (UnityEngine.EventSystems.EventSystem.current != null && 
                    UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
                {
                    return;
                }

                TryPlaceTower();
            }
        }

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            CancelPlacement();
        }
    }

    public void RegisterTower(Tower tower)
    {
        activeTowers.Add(tower);
        OnTowerPlaced?.Invoke(tower);
    }

    public void UnregisterTower(Tower tower)
    {
        activeTowers.Remove(tower);
    }

    public void SelectTowerType(Tower towerPrefab)
    {
        selectedTowerPrefab = towerPrefab;
        isPlacingTower = true;
        CreatePreviewObjects();
    }

    private void CreatePreviewObjects()
    {
        DestroyPreviewObjects();
        if (selectedTowerPrefab == null) return;
        
        previewGhost = Instantiate(selectedTowerPrefab.gameObject);
        
        // Clean up ghost
        foreach (var comp in previewGhost.GetComponentsInChildren<MonoBehaviour>()) Destroy(comp);
        foreach (var comp in previewGhost.GetComponentsInChildren<Collider>()) Destroy(comp);
        
        ghostRenderers = previewGhost.GetComponentsInChildren<Renderer>();
        foreach (var rend in ghostRenderers)
        {
            rend.material = CreateGhostMaterial(3000);
        }
        
        previewFloor = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(previewFloor.GetComponent<Collider>());
        previewFloor.transform.rotation = Quaternion.Euler(90, 0, 0);
        float size = GridManager.Instance.GetCellSize();
        previewFloor.transform.localScale = new Vector3(size, size, 1);
        
        floorRenderer = previewFloor.GetComponent<Renderer>();
        floorRenderer.material = CreateGhostMaterial(3001);
        
        lastHoveredCell = new Vector2Int(-999, -999);
    }
    
    // Sprites/Default is unlit, alpha-blended, tintable via color, and works in both built-in and URP.
    private static Material CreateGhostMaterial(int renderQueue)
    {
        return new Material(Shader.Find("Sprites/Default")) { renderQueue = renderQueue };
    }

    private void DestroyPreviewObjects()
    {
        if (previewGhost != null) Destroy(previewGhost);
        if (previewFloor != null) Destroy(previewFloor);
    }

    private void UpdatePreview()
    {
        if (previewGhost == null || previewFloor == null) return;

        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            if (hit.collider.CompareTag("Ground"))
            {
                previewGhost.SetActive(true);
                previewFloor.SetActive(true);

                Vector3 snappedPos = GridManager.Instance.SnapToGrid(hit.point);
                previewGhost.transform.position = snappedPos;
                previewFloor.transform.position = snappedPos + new Vector3(0, 0.05f, 0);

                Vector2Int cell = GridManager.Instance.WorldToGridCell(snappedPos);
                if (cell != lastHoveredCell)
                {
                    lastHoveredCell = cell;
                    lastHoverIsValid = CanPlaceAt(cell, snappedPos, selectedTowerPrefab.GetCost(), false, out _);

                    Color ghostColor = lastHoverIsValid ? new Color(0, 1, 0, 0.4f) : new Color(1, 0, 0, 0.4f);
                    Color floorColor = lastHoverIsValid ? new Color(0, 1, 0, 0.8f) : new Color(1, 0, 0, 0.8f);

                    foreach (var rend in ghostRenderers)
                    {
                        if (rend != null) rend.material.color = ghostColor;
                    }
                    if (floorRenderer != null) floorRenderer.material.color = floorColor;
                }
            }
            else
            {
                previewGhost.SetActive(false);
                previewFloor.SetActive(false);
            }
        }
        else
        {
            previewGhost.SetActive(false);
            previewFloor.SetActive(false);
        }
    }

    /// <summary>
    /// Whether a tower costing <paramref name="cost"/> can be placed on <paramref name="cell"/>: cell is buildable,
    /// the player can afford it, and it would not cut off any spawner. <paramref name="clearPathCache"/> is true
    /// for a real placement attempt (cache is refreshed around the check), false for hover previews.
    /// </summary>
    private bool CanPlaceAt(Vector2Int cell, Vector3 worldPos, int cost, bool clearPathCache, out string failReason)
    {
        failReason = null;
        if (!GridManager.Instance.CanBuildAt(worldPos)) { failReason = "occupied or unbuildable"; return false; }
        if (GameManager.Instance.GetCurrentGold() < cost) { failReason = "not enough gold"; return false; }

        GridManager.Instance.OccupyCell(cell);
        if (clearPathCache) PathManager.Instance.ClearCache();
        bool pathOk = PathManager.Instance.ValidateFullMaze();
        GridManager.Instance.FreeCell(cell);
        if (clearPathCache) PathManager.Instance.ClearCache();

        if (!pathOk) failReason = "would block the path for at least one spawner";
        return pathOk;
    }

    private void TryPlaceTower()
    {
        if (selectedTowerPrefab == null) return;

        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            // Check if we hit the ground plane
            if (hit.collider.CompareTag("Ground") || hit.collider.GetComponent<MapGenerator>() != null)
            {
                // Snap to grid
                Vector3 snappedPos = GridManager.Instance.SnapToGrid(hit.point);
                
                Vector2Int cell = GridManager.Instance.WorldToGridCell(snappedPos);
                int cost = selectedTowerPrefab.GetCost();
                if (!CanPlaceAt(cell, snappedPos, cost, true, out string failReason))
                {
                    Debug.Log($"Tower placement failed at {cell}: {failReason}");
                    return;
                }

                if (GameManager.Instance.TrySpendGold(cost))
                {
                    GridManager.Instance.OccupyCell(cell);
                    Tower newTower = Instantiate(selectedTowerPrefab, snappedPos, Quaternion.identity);
                    newTower.gameObject.SetActive(true);
                    
                    RegisterTower(newTower);
                    
                    if (!Keyboard.current.shiftKey.isPressed)
                    {
                        CancelPlacement();
                    }
                    
                    // Notify enemies that the maze has changed
                    PathManager.Instance.NotifyMazeChanged();
                }
            }
        }
    }

    public void CancelPlacement()
    {
        isPlacingTower = false;
        selectedTowerPrefab = null;
        DestroyPreviewObjects();
    }
    
    public Tower GetSelectedTowerPrefab() => selectedTowerPrefab;

    public bool IsPlacingTower() => isPlacingTower;
}
