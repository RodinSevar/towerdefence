using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TowerSelectionUI : MonoBehaviour
{
    [SerializeField]
    private Transform towersContainer;

    [SerializeField]
    private GameObject towerButtonPrefab;

    private Tower[] towerPrefabs;

    private void Start()
    {
        // Get container if not assigned
        if (towersContainer == null)
            towersContainer = transform;

        // Create tower prefabs
        towerPrefabs = new Tower[3];
        
        // Gun Tower
        GameObject gunTowerObj = new GameObject("GunTower");
        gunTowerObj.SetActive(false);
        Tower gunTower = gunTowerObj.AddComponent<Tower>();
        gunTower.SetTowerType(Tower.TowerType.Gun);
        towerPrefabs[0] = gunTower;

        // Laser Tower
        GameObject laserTowerObj = new GameObject("LaserTower");
        laserTowerObj.SetActive(false);
        Tower laserTower = laserTowerObj.AddComponent<Tower>();
        laserTower.SetTowerType(Tower.TowerType.Laser);
        towerPrefabs[1] = laserTower;

        // Ice Tower
        GameObject iceTowerObj = new GameObject("IceTower");
        iceTowerObj.SetActive(false);
        Tower iceTower = iceTowerObj.AddComponent<Tower>();
        iceTower.SetTowerType(Tower.TowerType.Ice);
        towerPrefabs[2] = iceTower;

        CreateTowerButtons();
    }

    private void CreateTowerButtons()
    {
        foreach (Tower tower in towerPrefabs)
        {
            GameObject buttonObj = new GameObject("TowerButton");
            buttonObj.transform.SetParent(towersContainer);

            Image buttonImage = buttonObj.AddComponent<Image>();
            buttonImage.color = new Color(0.3f, 0.3f, 0.3f);

            Button button = buttonObj.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.3f, 0.3f, 0.3f);
            colors.highlightedColor = new Color(0.5f, 0.5f, 0.5f);
            colors.pressedColor = new Color(0.1f, 0.1f, 0.1f);
            button.colors = colors;

            LayoutElement layoutElement = buttonObj.AddComponent<LayoutElement>();
            layoutElement.preferredHeight = 60;
            layoutElement.preferredWidth = 130;

            GameObject buttonTextObj = new GameObject("Text");
            buttonTextObj.transform.SetParent(buttonObj.transform);
            TextMeshProUGUI buttonText = buttonTextObj.AddComponent<TextMeshProUGUI>();
            buttonText.text = $"{tower.GetDisplayName()}\n${tower.GetCost()}";
            buttonText.fontSize = 18;
            buttonText.alignment = TextAlignmentOptions.Center;
            buttonText.color = Color.white;

            RectTransform textRect = buttonTextObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
            buttonRect.sizeDelta = new Vector2(130, 60);

            button.onClick.AddListener(() => SelectTower(tower));
        }
    }

    private void SelectTower(Tower tower)
    {
        TowerManager.Instance.SelectTowerType(tower);
    }
}
