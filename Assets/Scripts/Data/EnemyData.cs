using UnityEngine;

/// <summary>Definition of a creep type. Create via Assets > Create > Wintermaul.</summary>
[CreateAssetMenu(menuName = "Wintermaul/Enemy Data", fileName = "NewEnemy")]
public class EnemyData : ScriptableObject
{
    public string displayName = "Creep";
    public float health = 20f;
    [Tooltip("Units (grid cells) per second")]
    public float speed = 5f;
    [Tooltip("WC3 armor value; damage reduction = 0.06a / (1 + 0.06a)")]
    public float armor = 0f;
    public int goldReward = 10;
    public float visualScale = 0.8f;

    [TextArea]
    [Tooltip("Set by the WC3 importer: fields that the map did not define and were assumed")]
    public string importNotes;

    /// <summary>Fraction of incoming damage removed by armor (WC3 formula; negative armor is ignored).</summary>
    public float DamageReduction
    {
        get
        {
            float a = Mathf.Max(0f, armor);
            return 0.06f * a / (1f + 0.06f * a);
        }
    }
}
