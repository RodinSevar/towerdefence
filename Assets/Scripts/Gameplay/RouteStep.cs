using UnityEngine;

/// <summary>
/// One leg of a creep's route, modelled on the original map: creeps are ordered to move to <see cref="target"/> (the
/// centre of an "order" region); when they enter <see cref="regionMin"/>..<see cref="regionMax"/> (the trigger region)
/// the next order is issued. The last step's region is the exit: entering it costs a life.
/// Coordinates are Unity world units (x, z); the map's world units divided by 64.
/// </summary>
[System.Serializable]
public struct RouteStep
{
    public string name;
    public Vector3 target;
    public Vector2 regionMin;
    public Vector2 regionMax;
    public bool isExit;

    public bool Contains(Vector3 p)
    {
        return p.x >= regionMin.x && p.x <= regionMax.x && p.z >= regionMin.y && p.z <= regionMax.y;
    }
}
