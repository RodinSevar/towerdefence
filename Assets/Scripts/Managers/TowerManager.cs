using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class TowerManager : MonoBehaviour
{
    public static TowerManager Instance { get; private set; }

    private List<Tower> activeTowers = new List<Tower>();
    private Tower selectedTowerPrefab = null;
    private bool isPlacingTower = false;

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
        if (isPlacingTower && Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryPlaceTower();
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
    }

    private void TryPlaceTower()
    {
        if (selectedTowerPrefab == null) return;

        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            // Check if we hit the ground plane
            if (hit.collider.CompareTag("Ground"))
            {
                // Snap to grid
                Vector3 snappedPos = GridManager.Instance.SnapToGrid(hit.point);
                
                // Check if building is allowed at this position
                if (!GridManager.Instance.CanBuildAt(snappedPos))
                {
                    return; // Can't build here
                }
                
                int cost = selectedTowerPrefab.GetComponent<Tower>().GetCost();
                if (GameManager.Instance.GetCurrentGold() < cost)
                {
                    return; // Not enough gold
                }
                
                // Temporarily occupy to validate maze
                Vector2Int cell = GridManager.Instance.WorldToGridCell(snappedPos);
                GridManager.Instance.OccupyCell(cell);
                
                if (!PathManager.Instance.ValidateFullMaze())
                {
                    // Revert and deny placement
                    GridManager.Instance.FreeCell(cell);
                    Debug.Log("Tower placement blocked: Prevents path completion.");
                    return;
                }
                
                if (GameManager.Instance.TrySpendGold(cost))
                {
                    Tower newTower = Instantiate(selectedTowerPrefab, snappedPos, Quaternion.identity);
                    newTower.gameObject.SetActive(true);
                    
                    RegisterTower(newTower);
                    CancelPlacement();
                    
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
    }

    public bool IsPlacingTower() => isPlacingTower;
}
