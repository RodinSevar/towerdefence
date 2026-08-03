using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SelectionUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private Button sellButton;
    [SerializeField] private TextMeshProUGUI sellButtonText;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private TextMeshProUGUI upgradeButtonText;
    [SerializeField] private GameObject panelObject;

    private ISelectable currentUnit;
    private string lastKnownStats = "";

    private void Start()
    {
        if (PlayerInteraction.Instance != null)
        {
            PlayerInteraction.Instance.OnUnitSelected += HandleUnitSelected;
            PlayerInteraction.Instance.OnUnitDeselected += HandleUnitDeselected;
        }

        if (sellButton != null)
        {
            sellButton.onClick.AddListener(OnSellClicked);
        }
        
        if (upgradeButton != null)
        {
            upgradeButton.onClick.AddListener(OnUpgradeClicked);
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
            PlayerInteraction.Instance.OnUnitSelected -= HandleUnitSelected;
            PlayerInteraction.Instance.OnUnitDeselected -= HandleUnitDeselected;
        }
    }

    private void Update()
    {
        if (currentUnit != null)
        {
            MonoBehaviour mb = currentUnit as MonoBehaviour;
            if (mb == null)
            {
                // The unit's GameObject was destroyed (it died or was sold)
                if (statsText != null)
                {
                    statsText.text = lastKnownStats + "\n<color=red>[DEAD]</color>";
                }
                
                if (sellButton != null)
                {
                    sellButton.gameObject.SetActive(false);
                }
                
                if (upgradeButton != null)
                {
                    upgradeButton.gameObject.SetActive(false);
                }
                
                // Clear the reference so we stop checking
                currentUnit = null;
            }
            else
            {
                // Update live stats
                lastKnownStats = currentUnit.GetStatsText();
                if (statsText != null)
                {
                    statsText.text = lastKnownStats;
                }
                
                // Update upgrade button interactability dynamically (in case gold changes)
                if (upgradeButton != null && currentUnit.CanUpgrade())
                {
                    bool canAfford = GameManager.Instance.GetGold() >= currentUnit.GetUpgradeCost();
                    upgradeButton.interactable = canAfford;
                }
            }
        }
    }

    private void HandleUnitSelected(ISelectable unit)
    {
        currentUnit = unit;
        
        if (nameText != null)
        {
            nameText.text = unit.GetDisplayName();
        }

        if (statsText != null)
        {
            statsText.text = unit.GetStatsText();
        }

        if (sellButtonText != null)
        {
            int refund = unit.GetRefundAmount();
            sellButtonText.text = $"Sell (+{refund}G)";
        }

        if (sellButton != null)
        {
            sellButton.gameObject.SetActive(unit.IsSellable());
        }

        if (upgradeButton != null)
        {
            bool canUpgrade = unit.CanUpgrade();
            upgradeButton.gameObject.SetActive(canUpgrade);
            if (canUpgrade && upgradeButtonText != null)
            {
                int cost = unit.GetUpgradeCost();
                upgradeButtonText.text = $"Upgrade (-{cost}G)";
                upgradeButton.interactable = GameManager.Instance.GetGold() >= cost;
            }
        }

        if (panelObject != null)
        {
            panelObject.SetActive(true);
        }
    }

    private void HandleUnitDeselected()
    {
        currentUnit = null;
        if (panelObject != null)
        {
            panelObject.SetActive(false);
        }
    }

    private void OnSellClicked()
    {
        if (currentUnit != null && currentUnit.IsSellable())
        {
            currentUnit.Sell();
            HandleUnitDeselected();
        }
    }

    private void OnUpgradeClicked()
    {
        if (currentUnit != null && currentUnit.CanUpgrade())
        {
            int cost = currentUnit.GetUpgradeCost();
            if (GameManager.Instance.TrySpendGold(cost))
            {
                currentUnit.Upgrade();
                
                // Refresh UI immediately
                HandleUnitSelected(currentUnit);
            }
        }
    }
}
