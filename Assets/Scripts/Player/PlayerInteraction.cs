using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class PlayerInteraction : MonoBehaviour
{
    public static PlayerInteraction Instance { get; private set; }
    
    private Tower selectedTower;
    
    public System.Action<Tower> OnTowerSelected;
    public System.Action OnTowerDeselected;

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
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            // If the player is currently placing a tower, ignore selection clicks
            if (TowerManager.Instance != null && TowerManager.Instance.IsPlacingTower())
                return;
                
            // Check if pointer is over a UI element, don't deselect or select through UI
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                return;

            TrySelectTower();
        }
        
        // Deselect on right click or escape
        if (Mouse.current.rightButton.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            DeselectTower();
        }
    }

    private void TrySelectTower()
    {
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Tower tower = hit.collider.GetComponentInParent<Tower>();
            
            if (tower != null)
            {
                SelectTower(tower);
            }
            else
            {
                // Clicked on something else (like the ground)
                DeselectTower();
            }
        }
    }

    public void SelectTower(Tower tower)
    {
        if (selectedTower != null)
        {
            selectedTower.SetSelected(false);
        }
        
        selectedTower = tower;
        selectedTower.SetSelected(true);
        
        OnTowerSelected?.Invoke(selectedTower);
    }

    public void DeselectTower()
    {
        if (selectedTower != null)
        {
            selectedTower.SetSelected(false);
            selectedTower = null;
            
            OnTowerDeselected?.Invoke();
        }
    }

    public Tower GetSelectedTower()
    {
        return selectedTower;
    }
}
