using UnityEngine;

/// <summary>
/// One tower "element" (race). In the original map a wisp spends 1 lumber to build the race's main building,
/// which trains the race's constructor unit, which builds the towers listed here.
/// Create via Assets > Create > Wintermaul.
/// </summary>
[CreateAssetMenu(menuName = "Wintermaul/Race Data", fileName = "NewRace")]
public class RaceData : ScriptableObject
{
    [Tooltip("Name of the race's main building, e.g. Crystal Castle")]
    public string displayName = "Race";
    [Tooltip("Name of the constructor unit the building trains")]
    public string constructorName;
    [TextArea] public string description;
    [Tooltip("Lumber needed to unlock the race. 0 = owned from the start")]
    public int lumberCost = 1;
    public Color color = Color.white;

    [Tooltip("Towers the constructor can build, in command-card order (max 12)")]
    public TowerData[] towers;
}
