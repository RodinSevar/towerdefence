using UnityEngine;

/// <summary>
/// A creep spawn point (one of the map's spawn regions) and the route its creeps follow. Routes are generated from the
/// map's triggers by Tools > Import WC3 Routes; see <see cref="RouteStep"/>.
/// </summary>
public class Spawner : MonoBehaviour
{
    public Color playerColor = Color.white;

    [Tooltip("Creeps per wave spawned here; -1 = the wave's count (the map uses a fixed 1 at one spawn)")]
    public int amountOverride = -1;

    [Tooltip("Name of the map's unit group this spawn belongs to (informational)")]
    public string routeName;

    public RouteStep[] steps;

    /// <summary>The spawn point followed by each step's target: the path system validates these legs.</summary>
    public Vector3[] waypoints { get; private set; }

    private void Awake()
    {
        BuildWaypoints();
    }

    private void BuildWaypoints()
    {
        int n = steps != null ? steps.Length : 0;
        waypoints = new Vector3[n + 1];
        waypoints[0] = transform.position;
        for (int i = 0; i < n; i++) waypoints[i + 1] = steps[i].target;
    }

    private void Start()
    {
        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.RegisterSpawner(this);
        }
    }

    private void OnDestroy()
    {
        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.UnregisterSpawner(this);
        }
    }

    private void OnDrawGizmos()
    {
        if (steps == null || steps.Length == 0) return;

        Gizmos.color = playerColor;
        Vector3 previous = transform.position;
        Gizmos.DrawSphere(previous, 0.5f);
        foreach (var step in steps)
        {
            Gizmos.DrawLine(previous, step.target);
            Gizmos.DrawWireSphere(step.target, 0.5f);

            var center = new Vector3((step.regionMin.x + step.regionMax.x) * 0.5f, step.target.y, (step.regionMin.y + step.regionMax.y) * 0.5f);
            var size = new Vector3(step.regionMax.x - step.regionMin.x, 0.1f, step.regionMax.y - step.regionMin.y);
            Gizmos.DrawWireCube(center, size);
            previous = step.target;
        }
    }
}
