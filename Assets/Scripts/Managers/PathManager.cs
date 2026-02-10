using UnityEngine;

public class PathManager : MonoBehaviour
{
    public static PathManager Instance { get; private set; }

    [SerializeField]
    private Vector3[] waypoints;

    [SerializeField]
    private bool drawDebugPath = true;

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
        if (waypoints == null || waypoints.Length == 0)
        {
            CreateDefaultPath();
        }
    }

    private void CreateDefaultPath()
    {
        // Simple straight path from left to right
        waypoints = new Vector3[]
        {
            new Vector3(-18, 0.5f, 0),
            new Vector3(-12, 0.5f, 0),
            new Vector3(-6, 0.5f, 0),
            new Vector3(0, 0.5f, 0),
            new Vector3(6, 0.5f, 0),
            new Vector3(12, 0.5f, 0),
            new Vector3(18, 0.5f, 0)
        };
    }

    private void OnDrawGizmos()
    {
        if (!drawDebugPath || waypoints == null || waypoints.Length < 2) return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            Gizmos.DrawLine(waypoints[i], waypoints[i + 1]);
            Gizmos.DrawSphere(waypoints[i], 0.3f);
        }
        Gizmos.DrawSphere(waypoints[waypoints.Length - 1], 0.3f);
    }

    public Vector3 GetWaypoint(int index)
    {
        if (index >= 0 && index < waypoints.Length)
            return waypoints[index];
        return waypoints[waypoints.Length - 1];
    }

    public int GetWaypointCount() => waypoints.Length;

    public float GetTotalPathDistance()
    {
        float distance = 0f;
        for (int i = 0; i < waypoints.Length - 1; i++)
        {
            distance += Vector3.Distance(waypoints[i], waypoints[i + 1]);
        }
        return distance;
    }
}
