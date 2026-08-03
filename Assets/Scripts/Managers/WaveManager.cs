using UnityEngine;

public class WaveManager : MonoBehaviour
{
    public static WaveManager Instance { get; private set; }

    [System.Serializable]
    public class Wave
    {
        public int enemyCount = 10;
        public float spawnInterval = 0.5f;
        public Enemy.EnemyType enemyType = Enemy.EnemyType.Basic;
    }

    [SerializeField]
    private Wave[] waves;

    private Transform spawnPoint;
    private int currentWave = 0;
    private float spawnTimer = 0f;
    private int enemiesSpawnedInWave = 0;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        // Default waves if none configured
        if (waves == null || waves.Length == 0)
        {
            SetupDefaultWaves();
        }
    }

    private void SetupDefaultWaves()
    {
        waves = new Wave[5];
        for (int i = 0; i < 5; i++)
        {
            waves[i] = new Wave
            {
                enemyCount = 5 + (i * 3),
                spawnInterval = 0.5f - (i * 0.05f),
                enemyType = i < 2 ? Enemy.EnemyType.Basic : Enemy.EnemyType.Strong
            };
        }
    }

    private void Update()
    {
        if (currentWave == 0 || currentWave > waves.Length) return;

        Wave wave = waves[currentWave - 1];

        if (enemiesSpawnedInWave < wave.enemyCount)
        {
            spawnTimer += Time.deltaTime;

            if (spawnTimer >= wave.spawnInterval)
            {
                SpawnEnemy(wave.enemyType);
                spawnTimer = 0f;
                enemiesSpawnedInWave++;
            }
        }
    }

    public void StartWave(int waveNumber)
    {
        if (waveNumber > waves.Length)
        {
            Debug.LogWarning("Wave number exceeds configured waves!");
            return;
        }

        currentWave = waveNumber;
        enemiesSpawnedInWave = 0;
        spawnTimer = 0f;
    }

    private void SpawnEnemy(Enemy.EnemyType type)
    {
        GameObject enemyObj = new GameObject("Enemy");
        Enemy enemy = enemyObj.AddComponent<Enemy>();
        enemy.SetEnemyType(type);
        
        enemyObj.transform.position = PathManager.Instance != null ? PathManager.Instance.GetSpawnPoint() : new Vector3(-90, 0.5f, 0);

        // Add collider for targeting
        BoxCollider enemyCollider = enemyObj.AddComponent<BoxCollider>();
        enemyCollider.size = new Vector3(0.8f, 0.8f, 0.8f);
        
        // Create visual using CreatePrimitive for reliability
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "EnemyVisual";
        visual.transform.SetParent(enemyObj.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = Vector3.one * 0.8f;
        
        // Remove collider from primitive
        DestroyImmediate(visual.GetComponent<BoxCollider>());
        
        // Set material color
        Renderer renderer = visual.GetComponent<Renderer>();
        if (renderer != null)
        {
            Color color = type == Enemy.EnemyType.Basic ? Color.green : Color.red;
            renderer.material.color = color;
        }
        
        visual.SetActive(true);
        enemyObj.SetActive(true);


        GameManager.Instance.RegisterEnemy(enemy);
    }

    public int GetTotalWaves() => waves.Length;
    public int GetCurrentWave() => currentWave;
}
