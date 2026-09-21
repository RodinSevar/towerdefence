using UnityEngine;

public enum TowerAttackStyle
{
    Projectile, // fires a projectile that travels to the target
    Laser       // instant damage with a brief beam
}

/// <summary>One upgrade level of a tower.</summary>
[System.Serializable]
public class TowerLevel
{
    public int cost = 100;
    public float range = 10f;
    public float fireRate = 1f;
    public float damage = 10f;

    [Header("Visuals")]
    public Color visualColor = Color.blue;
    public float visualScale = 0.8f;

    [Header("Projectile")]
    public float projectileSpeed = 15f;
    public Color projectileColor = Color.yellow;
    public float projectileScale = 0.2f;

    [Header("Status Effect")]
    public bool hasStatusEffect = false;
    public StatusEffectType effectType;
    public float effectDuration;
    public float effectStrength;
}

/// <summary>Definition of a buildable tower and its upgrade path. Create via Assets > Create > Wintermaul.</summary>
[CreateAssetMenu(menuName = "Wintermaul/Tower Data", fileName = "NewTower")]
public class TowerData : ScriptableObject
{
    public string displayName = "Tower";
    public TowerAttackStyle attackStyle = TowerAttackStyle.Projectile;

    [Tooltip("Optional icon for the build button")]
    public Sprite icon;

    [Tooltip("Level 1 is the first entry; each further entry is an upgrade")]
    public TowerLevel[] levels = { new TowerLevel() };

    public int BaseCost => levels != null && levels.Length > 0 ? levels[0].cost : 0;
}
