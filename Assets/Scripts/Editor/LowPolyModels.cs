using UnityEngine;

/// <summary>The low-poly models, each described in code. See <see cref="LowPolyBuilder"/>.</summary>
public static class LowPolyModels
{
    private static readonly Color Stone = new Color(0.55f, 0.56f, 0.60f, 0);
    private static readonly Color StoneDark = new Color(0.36f, 0.37f, 0.42f, 0);
    private static readonly Color StoneLight = new Color(0.68f, 0.69f, 0.73f, 0);
    private static readonly Color Roof = new Color(0.85f, 0.85f, 0.90f, 1); // alpha 1: takes the tower level's colour;
    private static readonly Color Dark = new Color(0.10f, 0.10f, 0.13f, 0);
    private static readonly Color Gold = new Color(0.85f, 0.70f, 0.25f, 0);

    /// <summary>A round stone guard tower with battlements and a pointed roof. About 1 unit wide, 1.6 tall.</summary>
    public static Mesh GuardTower()
    {
        var b = new LowPolyBuilder();

        // plinth and shaft (octagons)
        b.Frustum(8, 0.00f, 0.16f, 0.50f, 0.50f, 0.42f, 0.42f, StoneDark, rotationDeg: 22.5f);
        b.Frustum(8, 0.16f, 0.88f, 0.36f, 0.36f, 0.30f, 0.30f, Stone, rotationDeg: 22.5f);

        // flared parapet
        b.Frustum(8, 0.88f, 1.02f, 0.30f, 0.30f, 0.42f, 0.42f, StoneLight, rotationDeg: 22.5f);

        // four merlons on the parapet
        for (int i = 0; i < 4; i++)
        {
            float a = (45f + 90f * i) * Mathf.Deg2Rad;
            var pos = new Vector3(Mathf.Cos(a) * 0.34f, 1.10f, Mathf.Sin(a) * 0.34f);
            b.Box(pos, new Vector3(0.14f, 0.16f, 0.14f), StoneLight, -45f - 90f * i);
        }

        // roof: short drum and a cone
        b.Frustum(8, 1.02f, 1.06f, 0.24f, 0.24f, 0.24f, 0.24f, StoneDark, rotationDeg: 22.5f);
        b.Frustum(8, 1.06f, 1.55f, 0.30f, 0.30f, 0f, 0f, Roof, rotationDeg: 22.5f);
        b.Frustum(4, 1.55f, 1.62f, 0.03f, 0.03f, 0.0f, 0.0f, Gold, rotationDeg: 45f);

        // door and two arrow slits (dark boxes on the +z face)
        b.Box(new Vector3(0, 0.20f, 0.36f), new Vector3(0.14f, 0.20f, 0.06f), Dark);
        b.Box(new Vector3(0, 0.62f, 0.32f), new Vector3(0.04f, 0.16f, 0.06f), Dark);
        b.Box(new Vector3(0.33f, 0.62f, 0.0f), new Vector3(0.06f, 0.16f, 0.04f), Dark);

        Debug.Log($"LowPolyModels: GuardTower {b.TriangleCount} triangles");
        return b.ToMesh("GuardTower");
    }
}
