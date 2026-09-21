using UnityEngine;

/// <summary>Definition of a creep type. Create via Assets > Create > Wintermaul.</summary>
[CreateAssetMenu(menuName = "Wintermaul/Enemy Data", fileName = "NewEnemy")]
public class EnemyData : ScriptableObject
{
    public string displayName = "Creep";
    public float health = 20f;
    public float speed = 5f;
    public int goldReward = 10;
    public float visualScale = 0.8f;
}
