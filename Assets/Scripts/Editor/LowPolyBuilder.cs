using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds flat-shaded, vertex-coloured low-poly meshes from a few solid shapes (frustums, boxes, cones). Every face gets
/// its own vertices so edges stay crisp, and each face is wound to point away from its solid's centre, so shapes only need
/// their dimensions. Models are authored around the origin with the base at y = 0 and about one unit wide.
/// </summary>
public class LowPolyBuilder
{
    private readonly List<Vector3> verts = new List<Vector3>();
    private readonly List<Color> colors = new List<Color>();
    private readonly List<int> tris = new List<int>();

    public int TriangleCount => tris.Count / 3;

    /// <summary>
    /// A frustum with <paramref name="sides"/> sides from y0 (radii rx0, rz0) to y1 (radii rx1, rz1). A radius of 0 at the
    /// top makes a cone/pyramid. <paramref name="rotationDeg"/> spins it about the vertical axis.
    /// </summary>
    public void Frustum(int sides, float y0, float y1, float rx0, float rz0, float rx1, float rz1, Color color,
                        Vector3 offset = default, float rotationDeg = 0f, bool bottomCap = false)
    {
        var bottom = new Vector3[sides];
        var top = new Vector3[sides];
        for (int i = 0; i < sides; i++)
        {
            float a = (rotationDeg + 360f * i / sides) * Mathf.Deg2Rad;
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            bottom[i] = offset + new Vector3(c * rx0, y0, s * rz0);
            top[i] = offset + new Vector3(c * rx1, y1, s * rz1);
        }
        Vector3 center = offset + new Vector3(0, (y0 + y1) / 2f, 0);

        bool coneTop = rx1 <= 0f && rz1 <= 0f;
        for (int i = 0; i < sides; i++)
        {
            int j = (i + 1) % sides;
            if (coneTop) Poly(center, color, bottom[i], top[i], bottom[j]);
            else Poly(center, color, bottom[i], top[i], top[j], bottom[j]);
        }
        if (!coneTop) Poly(center, color, top);
        if (bottomCap) Poly(center, color, bottom);
    }

    /// <summary>An axis-aligned box (optionally spun about the vertical axis), <paramref name="center"/> at its middle.</summary>
    public void Box(Vector3 center, Vector3 size, Color color, float rotationDeg = 0f)
    {
        var q = Quaternion.Euler(0, rotationDeg, 0);
        var p = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            var corner = new Vector3((i & 1) == 0 ? -0.5f : 0.5f, (i & 2) == 0 ? -0.5f : 0.5f, (i & 4) == 0 ? -0.5f : 0.5f);
            p[i] = center + q * Vector3.Scale(corner, size);
        }
        // corner index bits: x = 1, y = 2, z = 4
        Poly(center, color, p[0], p[2], p[6], p[4]); // -x
        Poly(center, color, p[1], p[5], p[7], p[3]); // +x
        Poly(center, color, p[0], p[4], p[5], p[1]); // -y
        Poly(center, color, p[2], p[3], p[7], p[6]); // +y
        Poly(center, color, p[0], p[1], p[3], p[2]); // -z
        Poly(center, color, p[4], p[6], p[7], p[5]); // +z
    }

    /// <summary>One convex polygon, wound so its front faces away from <paramref name="solidCenter"/>.</summary>
    private void Poly(Vector3 solidCenter, Color color, params Vector3[] p)
    {
        Vector3 centroid = Vector3.zero;
        foreach (var v in p) centroid += v;
        centroid /= p.Length;

        // Unity front faces are clockwise: the front normal is cross(b - a, c - a)
        Vector3 normal = Vector3.Cross(p[1] - p[0], p[2] - p[0]);
        bool flip = Vector3.Dot(normal, centroid - solidCenter) < 0f;

        int start = verts.Count;
        for (int i = 0; i < p.Length; i++) { verts.Add(p[i]); colors.Add(color); }
        for (int i = 1; i < p.Length - 1; i++)
        {
            if (flip) { tris.Add(start); tris.Add(start + i + 1); tris.Add(start + i); }
            else { tris.Add(start); tris.Add(start + i); tris.Add(start + i + 1); }
        }
    }

    public Mesh ToMesh(string name)
    {
        var mesh = new Mesh { name = name };
        mesh.SetVertices(verts);
        mesh.SetColors(colors);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals(); // per-face vertices, so normals are flat
        mesh.RecalculateBounds();
        return mesh;
    }
}
