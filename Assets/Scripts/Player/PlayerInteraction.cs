using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class PlayerInteraction : MonoBehaviour
{
    public static PlayerInteraction Instance { get; private set; }
    
    private ISelectable selectedUnit;
    
    public System.Action<ISelectable> OnUnitSelected;
    public System.Action OnUnitDeselected;

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

            TrySelectUnit();
        }
        
        // Deselect on right click or escape
        if (Mouse.current.rightButton.wasPressedThisFrame || Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            DeselectUnit();
        }
    }

    private void TrySelectUnit()
    {
        Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            ISelectable unit = hit.collider.GetComponentInParent<ISelectable>();
            
            if (unit != null)
            {
                SelectUnit(unit);
            }
            else
            {
                // Clicked on something else (like the ground)
                DeselectUnit();
            }
        }
    }

    public void SelectUnit(ISelectable unit)
    {
        if (selectedUnit != null)
        {
            selectedUnit.SetSelected(false);
        }
        
        selectedUnit = unit;
        selectedUnit.SetSelected(true);
        
        OnUnitSelected?.Invoke(selectedUnit);
    }

    public void DeselectUnit()
    {
        if (selectedUnit != null)
        {
            selectedUnit.SetSelected(false);
        }
        
        selectedUnit = null;
        OnUnitDeselected?.Invoke();
    }

    public ISelectable GetSelectedUnit()
    {
        return selectedUnit;
    }
}
