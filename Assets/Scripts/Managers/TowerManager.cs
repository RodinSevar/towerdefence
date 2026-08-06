using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class TowerManager : MonoBehaviour
{
    public static TowerManager Instance { get; private set; }

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

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Update()
    {
        if (isPlacingTower)
        {
            UpdatePreview();

            if (Mouse.current.leftButton.isPressed)
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
            Material mat = new Material(Shader.Find("Standard"));
            mat.SetFloat("_Mode", 3); // Transparent
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = 3000;
            rend.material = mat;
        }
        
        previewFloor = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(previewFloor.GetComponent<Collider>());
        previewFloor.transform.rotation = Quaternion.Euler(90, 0, 0);
        float size = GridManager.Instance.GetCellSize();
        previewFloor.transform.localScale = new Vector3(size, size, 1);
        
        floorRenderer = previewFloor.GetComponent<Renderer>();
        Material floorMat = new Material(Shader.Find("Standard"));
        floorMat.SetFloat("_Mode", 3);
        floorMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        floorMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        floorMat.SetInt("_ZWrite", 0);
        floorMat.EnableKeyword("_ALPHABLEND_ON");
        floorMat.renderQueue = 3001;
        floorRenderer.material = floorMat;
        
        lastHoveredCell = new Vector2Int(-999, -999);
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
                    lastHoverIsValid = false;

                    if (GridManager.Instance.CanBuildAt(snappedPos) && GameManager.Instance.GetCurrentGold() >= selectedTowerPrefab.GetComponent<Tower>().GetCost())
                    {
                        GridManager.Instance.OccupyCell(cell);
                        lastHoverIsValid = PathManager.Instance.ValidateFullMaze();
                        GridManager.Instance.FreeCell(cell);
                    }
                    
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
                
                // Check if building is allowed at this position
                if (!GridManager.Instance.CanBuildAt(snappedPos))
                {
                    Debug.Log($"Tower placement failed: Cannot build at {snappedPos} (Occupied or Unbuildable)");
                    return; // Can't build here
                }
                
                int cost = selectedTowerPrefab.GetComponent<Tower>().GetCost();
                if (GameManager.Instance.GetCurrentGold() < cost)
                {
                    Debug.Log($"Tower placement failed: Not enough gold (Cost: {cost}, Have: {GameManager.Instance.GetCurrentGold()})");
                    return; // Not enough gold
                }
                
                // Temporarily occupy to validate maze
                Vector2Int cell = GridManager.Instance.WorldToGridCell(snappedPos);
                GridManager.Instance.OccupyCell(cell);
                
                if (!PathManager.Instance.ValidateFullMaze())
                {
                    // Revert and deny placement
                    GridManager.Instance.FreeCell(cell);
                    Debug.Log($"Tower placement blocked: Placing at {cell} prevents path completion for at least one spawner.");
                    return;
                }
                
                if (GameManager.Instance.TrySpendGold(cost))
                {
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
                else
                {
                    GridManager.Instance.FreeCell(cell);
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
