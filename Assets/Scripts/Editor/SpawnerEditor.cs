using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Spawner))]
public class SpawnerEditor : Editor
{
    private void OnSceneGUI()
    {
        Spawner spawner = (Spawner)target;
        if (spawner.waypoints == null) return;

        for (int i = 0; i < spawner.waypoints.Length; i++)
        {
            EditorGUI.BeginChangeCheck();
            
            // Draw a label above the handle
            string label = (i == 0) ? "Spawn Location" : (i == spawner.waypoints.Length - 1) ? "Final Exit" : "Corner " + i;
            Handles.Label(spawner.waypoints[i] + Vector3.up * 2f, label);

            // Draw the interactive position handle
            Vector3 newPos = Handles.PositionHandle(spawner.waypoints[i], Quaternion.identity);
            
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(spawner, "Move Spawner Waypoint");
                spawner.waypoints[i] = newPos;
                
                // Repaint to update the Scene view immediately
                SceneView.RepaintAll();
            }
        }
    }
}
