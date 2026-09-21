using UnityEngine;

/// <summary>The ordered list of waves for a game. Create via Assets > Create > Wintermaul.</summary>
[CreateAssetMenu(menuName = "Wintermaul/Wave Set", fileName = "NewWaveSet")]
public class WaveSet : ScriptableObject
{
    [System.Serializable]
    public class Wave
    {
        public EnemyData enemy;
        public int enemyCount = 10;
    }

    public Wave[] waves;
}
