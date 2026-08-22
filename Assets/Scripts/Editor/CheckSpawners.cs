using UnityEditor;
using UnityEngine;
public static class CheckSpawners {
    public static void Check() {
        var spawners = Object.FindObjectsByType<Spawner>(FindObjectsSortMode.None);
        Debug.Log("Spawners in scene: " + spawners.Length);
        if (spawners.Length > 0) {
            Debug.Log("First spawner name: " + spawners[0].name + ", active: " + spawners[0].gameObject.activeInHierarchy + ", waypoints: " + (spawners[0].waypoints != null ? spawners[0].waypoints.Length.ToString() : "null"));
        }
    }
}
