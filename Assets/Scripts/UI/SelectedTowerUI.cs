using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SelectedTowerUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private Button sellButton;
    [SerializeField] private TextMeshProUGUI sellButtonText;
    [SerializeField] private GameObject panelObject;

    private Tower currentTower;

    private void Start()
    {
        if (PlayerInteraction.Instance != null)
        {
            PlayerInteraction.Instance.OnTowerSelected += HandleTowerSelected;
            PlayerInteraction.Instance.OnTowerDeselected += HandleTowerDeselected;
        }

        if (sellButton != null)
        {
            sellButton.onClick.AddListener(OnSellClicked);
        }

        // Start hidden
        if (panelObject != null)
        {
            panelObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (PlayerInteraction.Instance != null)
        {
            PlayerInteraction.Instance.OnTowerSelected -= HandleTowerSelected;
            PlayerInteraction.Instance.OnTowerDeselected -= HandleTowerDeselected;
        }
    }

    private void HandleTowerSelected(Tower tower)
    {
        currentTower = tower;
        
        if (nameText != null)
        {
            nameText.text = tower.GetDisplayName();
        }

        if (statsText != null)
        {
            statsText.text = $"Damage: {tower.GetDamage()}\n" +
                             $"Range: {tower.GetRange()}\n" +
                             $"Speed: {tower.GetFireRate()}s";
        }

        if (sellButtonText != null)
        {
            int refund = tower.GetCost() / 2;
            sellButtonText.text = $"Sell (+{refund}G)";
        }

        if (panelObject != null)
        {
            panelObject.SetActive(true);
        }
    }

    private void HandleTowerDeselected()
    {
        currentTower = null;
        if (panelObject != null)
        {
            panelObject.SetActive(false);
        }
    }

    private void OnSellClicked()
    {
        if (currentTower != null)
        {
            currentTower.SellTower();
            PlayerInteraction.Instance.DeselectTower();
        }
    }
}
