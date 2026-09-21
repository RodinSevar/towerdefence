using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The tower build menu (WC3-style command card). Shows one button per tower of the active race
/// (<see cref="TowerManager.BuildableTowers"/>), padded with empty slots up to <see cref="SlotCount"/>,
/// and rebuilds whenever the active race changes.
/// </summary>
public class TowerSelectionUI : MonoBehaviour
{
    private const int SlotCount = 12; // 4 columns x 3 rows

    private static readonly Color NormalColor = new Color(0.3f, 0.3f, 0.3f);
    private static readonly Color SelectedColor = new Color(0.8f, 0.8f, 0.2f);
    private static readonly Color EmptySlotColor = new Color(0.1f, 0.1f, 0.1f, 0.6f);

    [SerializeField]
    private Transform towersContainer;

    private readonly Dictionary<TowerData, Button> buttonMap = new Dictionary<TowerData, Button>();

    private void Start()
    {
        if (towersContainer == null)
            towersContainer = transform;

        RaceManager.Instance.OnRacesChanged += Rebuild;
        Rebuild();
    }

    private void OnDestroy()
    {
        if (RaceManager.Instance != null)
            RaceManager.Instance.OnRacesChanged -= Rebuild;
    }

    private void Rebuild()
    {
        foreach (Transform child in towersContainer) Destroy(child.gameObject);
        buttonMap.Clear();

        TowerData[] towers = TowerManager.Instance.BuildableTowers;
        if (towers.Length > SlotCount)
            Debug.LogWarning($"{towers.Length} buildable towers but the menu only has {SlotCount} slots.");

        for (int i = 0; i < SlotCount; i++)
        {
            if (i < towers.Length && towers[i] != null)
                CreateTowerButton(towers[i]);
            else
                CreateEmptySlot();
        }
    }

    private void CreateEmptySlot()
    {
        var slot = new GameObject("EmptySlot", typeof(RectTransform), typeof(Image));
        slot.transform.SetParent(towersContainer, false);
        var image = slot.GetComponent<Image>();
        image.color = EmptySlotColor;
        image.raycastTarget = false;
    }

    private void CreateTowerButton(TowerData tower)
    {
        var buttonObj = new GameObject("TowerButton_" + tower.displayName, typeof(RectTransform));
        buttonObj.transform.SetParent(towersContainer, false);

        Image buttonImage = buttonObj.AddComponent<Image>();
        buttonImage.color = NormalColor;
        if (tower.icon != null) buttonImage.sprite = tower.icon;

        Button button = buttonObj.AddComponent<Button>();
        button.targetGraphic = buttonImage;

        var textObj = new GameObject("Text", typeof(RectTransform));
        textObj.transform.SetParent(buttonObj.transform, false);
        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = $"{tower.displayName}\n${tower.BaseCost}";
        text.enableAutoSizing = true;
        text.fontSizeMin = 5;
        text.fontSizeMax = 10;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.raycastTarget = false;

        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(2, 2);
        textRect.offsetMax = new Vector2(-2, -2);

        button.onClick.AddListener(() => TowerManager.Instance.SelectTower(tower));
        buttonMap[tower] = button;
    }

    private void Update()
    {
        TowerData selected = TowerManager.Instance.GetSelectedTower();
        foreach (var kvp in buttonMap)
        {
            ColorBlock colors = kvp.Value.colors;
            colors.normalColor = kvp.Key == selected ? SelectedColor : NormalColor;
            kvp.Value.colors = colors;
        }
    }
}
