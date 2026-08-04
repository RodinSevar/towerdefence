using UnityEngine;
using System.Collections.Generic;

public class Spawner : MonoBehaviour
{
    public Vector3[] waypoints;
    public Color playerColor = Color.white;

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
        if (waypoints == null || waypoints.Length == 0) return;

        Gizmos.color = playerColor;
        for (int i = 0; i < waypoints.Length; i++)
        {
            Gizmos.DrawSphere(waypoints[i], 0.5f);
            if (i < waypoints.Length - 1)
            {
                Gizmos.DrawLine(waypoints[i], waypoints[i + 1]);
            }
        }
    }
}
