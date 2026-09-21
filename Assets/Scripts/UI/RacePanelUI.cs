using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Element (race) selector, opened from the top-left "Race (F12)" button. Lists every race; selecting one shows
/// its description and towers, and the action button either activates an owned race or unlocks it for lumber.
/// (In the original map a wisp built the race's main building for 1 lumber; a constructor unit can replace this later.)
/// </summary>
public class RacePanelUI : MonoBehaviour
{
    private static readonly Color NormalColor = new Color(0.22f, 0.22f, 0.25f);
    private static readonly Color ActiveColor = new Color(0.25f, 0.5f, 0.25f);
    private static readonly Color PreviewColor = new Color(0.85f, 0.75f, 0.2f);

    [SerializeField] private GameObject content;
    [SerializeField] private Transform raceListContainer;
    [SerializeField] private TextMeshProUGUI infoText;
    [SerializeField] private Button actionButton;
    [SerializeField] private TextMeshProUGUI actionButtonText;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button toggleButton;

    private class Row
    {
        public Image image;
        public TextMeshProUGUI label;
    }

    private readonly Dictionary<RaceData, Row> rows = new Dictionary<RaceData, Row>();
    private RaceData previewed;

    private void Start()
    {
        toggleButton.onClick.AddListener(Toggle);
        closeButton.onClick.AddListener(Hide);
        actionButton.onClick.AddListener(OnActionClicked);

        foreach (RaceData race in RaceManager.Instance.Races)
        {
            if (race != null) CreateRow(race);
        }

        RaceManager.Instance.OnRacesChanged += Refresh;

        content.SetActive(false);
        Refresh();
    }

    private void OnDestroy()
    {
        if (RaceManager.Instance != null) RaceManager.Instance.OnRacesChanged -= Refresh;
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.f12Key.wasPressedThisFrame) Toggle();
    }

    private void Toggle()
    {
        content.SetActive(!content.activeSelf);
        Refresh();
    }

    private void Hide() { content.SetActive(false); }

    private void OnLumberChanged(int _) { Refresh(); }

    private void CreateRow(RaceData race)
    {
        var obj = new GameObject("Race_" + race.displayName, typeof(RectTransform));
        obj.transform.SetParent(raceListContainer, false);
        var image = obj.AddComponent<Image>();
        var button = obj.AddComponent<Button>();
        button.targetGraphic = image;
        obj.AddComponent<LayoutElement>().preferredHeight = 26;

        var labelObj = new GameObject("Text", typeof(RectTransform));
        labelObj.transform.SetParent(obj.transform, false);
        var label = labelObj.AddComponent<TextMeshProUGUI>();
        label.fontSize = 13;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.raycastTarget = false;
        RectTransform rect = label.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(8, 0);
        rect.offsetMax = new Vector2(-4, 0);

        button.onClick.AddListener(() => { previewed = race; Refresh(); });
        rows[race] = new Row { image = image, label = label };
    }

    private void Refresh()
    {
        if (previewed == null) previewed = RaceManager.Instance.ActiveRace != null ? RaceManager.Instance.ActiveRace : FirstRace();

        foreach (var kvp in rows)
        {
            RaceData race = kvp.Key;
            bool owned = RaceManager.Instance.IsOwned(race);
            bool active = RaceManager.Instance.ActiveRace == race;
            bool isPreview = race == previewed;
            kvp.Value.image.color = isPreview ? PreviewColor : (active ? ActiveColor : NormalColor);
            kvp.Value.label.color = isPreview ? Color.black : Color.white;
            kvp.Value.label.text = active ? $"{race.displayName}  (active)"
                                 : owned ? $"{race.displayName}  (owned)"
                                 : $"{race.displayName}  ({race.lumberCost} lumber)";
        }

        if (previewed == null)
        {
            infoText.text = "No races are configured.";
            actionButton.gameObject.SetActive(false);
            return;
        }

        infoText.text = BuildInfo(previewed);

        bool isOwned = RaceManager.Instance.IsOwned(previewed);
        bool isActive = RaceManager.Instance.ActiveRace == previewed;
        actionButton.gameObject.SetActive(true);
        if (isActive)
        {
            actionButtonText.text = "Active";
            actionButton.interactable = false;
        }
        else if (isOwned)
        {
            actionButtonText.text = "Use this element";
            actionButton.interactable = true;
        }
        else
        {
            actionButtonText.text = $"Unlock ({previewed.lumberCost} lumber)";
            actionButton.interactable = RaceManager.Instance.CanUnlock(previewed);
        }
    }

    private RaceData FirstRace()
    {
        foreach (RaceData race in RaceManager.Instance.Races)
        {
            if (race != null) return race;
        }
        return null;
    }

    private static string BuildInfo(RaceData race)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"<b>{race.displayName}</b>");
        if (!string.IsNullOrEmpty(race.constructorName)) sb.AppendLine($"Constructor: {race.constructorName}");
        sb.AppendLine();
        if (!string.IsNullOrEmpty(race.description)) sb.AppendLine(race.description);
        sb.AppendLine();
        sb.AppendLine("Towers:");
        if (race.towers != null)
        {
            foreach (TowerData tower in race.towers)
            {
                if (tower != null) sb.AppendLine($"  {tower.displayName} - {tower.BaseCost}g");
            }
        }
        return sb.ToString();
    }

    private void OnActionClicked()
    {
        if (previewed == null) return;
        if (RaceManager.Instance.IsOwned(previewed)) RaceManager.Instance.SetActive(previewed);
        else RaceManager.Instance.TryUnlock(previewed);
    }
}
