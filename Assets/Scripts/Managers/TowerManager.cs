using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class TowerManager : Singleton<TowerManager>
{
    private List<Tower> activeTowers = new List<Tower>();
    [Header("Tower setup")]
    [SerializeField] private Tower towerPrefab;
    private TowerData selectedTower = null;
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
        if (Mouse.current == null || Keyboard.current == null) return; // no input devices (e.g. headless run)

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

    /// <summary>Towers on the build menu: those of the active race.</summary>
    public TowerData[] BuildableTowers => RaceManager.Instance != null ? RaceManager.Instance.ActiveTowers : System.Array.Empty<TowerData>();

    public void SelectTower(TowerData tower)
    {
        selectedTower = tower;
        isPlacingTower = true;
        CreatePreviewObjects();
    }

    private void CreatePreviewObjects()
    {
        DestroyPreviewObjects();
        if (selectedTower == null) return;
        
        previewGhost = Instantiate(towerPrefab.gameObject);
        previewGhost.GetComponent<Tower>().Init(selectedTower); // applies level-1 visuals before we strip the scripts
        
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
        float size = GridManager.Instance.GetCellSize() * GridManager.Footprint;
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

                Vector3 snappedPos = GridManager.Instance.SnapToFootprint(hit.point);
                previewGhost.transform.position = snappedPos;
                previewFloor.transform.position = snappedPos + new Vector3(0, 0.05f, 0);

                Vector2Int cell = GridManager.Instance.FootprintOrigin(snappedPos);
                if (cell != lastHoveredCell)
                {
                    lastHoveredCell = cell;
                    lastHoverIsValid = CanPlaceAt(cell, snappedPos, selectedTower, out _);

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
    /// Whether <paramref name="tower"/> can be placed on <paramref name="cell"/>: cell is buildable,
    /// the player can afford it, and it would not cut off any spawner.
    /// </summary>
    private bool CanPlaceAt(Vector2Int cell, Vector3 worldPos, TowerData tower, out string failReason)
    {
        failReason = null;
        if (!GridManager.Instance.CanBuildFootprint(cell)) { failReason = "occupied or unbuildable"; return false; }
        if (GameManager.Instance.GetCurrentGold() < tower.BaseCost) { failReason = "not enough gold"; return false; }
        if (GameManager.Instance.GetCurrentLumber() < tower.lumberCost) { failReason = "not enough lumber"; return false; }

        GridManager.Instance.OccupyFootprint(cell);
        bool pathOk = PathManager.Instance.ValidateFullMaze();
        GridManager.Instance.FreeFootprint(cell);

        if (!pathOk) failReason = "would block the path for at least one spawner";
        return pathOk;
    }

    /// <summary>
    /// Places <paramref name="tower"/> at the grid-snapped position if it is affordable, buildable and does not cut off
    /// any spawner. Returns true if the tower was built. Independent of the build-menu selection and of mouse input.
    /// </summary>
    public bool TryPlaceTowerAt(TowerData tower, Vector3 worldPos)
    {
        Vector3 snappedPos = GridManager.Instance.SnapToFootprint(worldPos);
        Vector2Int cell = GridManager.Instance.FootprintOrigin(snappedPos);
        if (!CanPlaceAt(cell, snappedPos, tower, out string failReason))
        {
            Debug.Log($"Tower placement failed at {cell}: {failReason}");
            return false;
        }

        if (!GameManager.Instance.TrySpendGold(tower.BaseCost)) return false;
        if (!GameManager.Instance.TrySpendLumber(tower.lumberCost))
        {
            GameManager.Instance.AddGold(tower.BaseCost); // undo the gold spend
            return false;
        }

        GridManager.Instance.OccupyFootprint(cell);
        Tower newTower = Instantiate(towerPrefab, snappedPos, Quaternion.identity);
        newTower.Init(tower);
        RegisterTower(newTower);

        // Notify enemies that the maze has changed
        PathManager.Instance.NotifyMazeChanged();
        return true;
    }

    private void TryPlaceTower()
    {
        if (selectedTower == null) return;

        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            // Check if we hit the ground plane
            if (hit.collider.CompareTag("Ground") || hit.collider.GetComponent<TerrainBuilder>() != null)
            {
                // Snap to grid
                Vector3 snappedPos = GridManager.Instance.SnapToFootprint(hit.point);
                
                if (TryPlaceTowerAt(selectedTower, snappedPos) && !Keyboard.current.shiftKey.isPressed)
                {
                    CancelPlacement();
                }
            }
        }
    }

    public void CancelPlacement()
    {
        isPlacingTower = false;
        selectedTower = null;
        DestroyPreviewObjects();
    }
    
    public TowerData GetSelectedTower() => selectedTower;

    public bool IsPlacingTower() => isPlacingTower;
}
