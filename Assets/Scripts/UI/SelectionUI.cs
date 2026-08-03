using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SelectionUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI statsText;
    [SerializeField] private Button sellButton;
    [SerializeField] private TextMeshProUGUI sellButtonText;
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
            PlayerInteraction.Instance.DeselectUnit();
        }
    }
}
