using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds the playable terrain from the imported map (<see cref="TerrainMapData"/>): ground with blended tile textures,
/// cliff walls at real level changes, ramps, and water. Also loads the logic grid (blocked / unbuildable cells and ground
/// heights) and generates the minimap background.
///
/// Geometry rule: the mesh is made of 1x1 cells (the pathing cells). A cell belongs to its nearest tile corner and takes
/// its height from that corner, smoothed only with neighbouring corners on the SAME cliff level, so terraces are smooth and
/// level changes become vertical cliff walls. Tiles touching a ramp corner ignore levels and slope smoothly.
///
/// Ground texture blending: every vertex carries weights for the 7 ground tile types (interpolated from the surrounding
/// corners); the TerrainBlend shader mixes the textures per pixel.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class TerrainBuilder : Singleton<TerrainBuilder>
{
    [SerializeField] private TerrainMapData data;
    [SerializeField] private Material groundMaterial;
    [SerializeField] private Material wallMaterial;
    [SerializeField] private Material waterMaterial;

    [Header("Atmosphere")]
    [SerializeField] private bool applyAtmosphere = true;
    [SerializeField] private Color ambientColor = new Color(0.52f, 0.60f, 0.72f);

    private const float WallEpsilon = 1e-4f;
    private const float ShipX = -4.11f, ShipZ = -81.86f; // the map's pre-placed transport ship, at the exit

    private bool built;

    /// <summary>The ground + cliff mesh (also the collider).</summary>
    public Mesh GroundMesh { get; private set; }

    /// <summary>One pixel per cell: blocked, unbuildable, buildable, water. Used as the minimap background.</summary>
    public Texture2D MinimapTexture { get; private set; }

    private void Start()
    {
        EnsureBuilt();
    }

    /// <summary>Builds everything once; safe to call from anywhere after Awake.</summary>
    public void EnsureBuilt()
    {
        if (built) return;
        if (data == null) { Debug.LogError("TerrainBuilder has no TerrainMapData. Run Tools > Build Terrain.", this); return; }
        built = true;

        int cw = data.cellsX, ch = data.cellsZ;
        float halfX = cw / 2f, halfZ = ch / 2f;

        // ---- per-cell vertex heights and ground weights ----
        var vertexHeight = new float[cw * ch * 4];
        var weights = new float[cw * ch * 4 * 7];   // [cell][vertex BL,BR,TL,TR][tile type]
        var cellCenter = new float[cw * ch];
        var waterCell = new bool[cw * ch];

        for (int cz = 0; cz < ch; cz++)
            for (int cx = 0; cx < cw; cx++)
                ComputeCell(cx, cz, vertexHeight, weights, cellCenter, waterCell);

        GridManager.Instance.ApplyTerrain(data, cellCenter);

        // ---- ground + walls mesh ----
        var verts = new List<Vector3>(cw * ch * 5);
        var colors = new List<Color>(cw * ch * 5);
        var uvs = new List<Vector4>(cw * ch * 5);
        var groundTris = new List<int>(cw * ch * 6);
        var wallTris = new List<int>(cw * ch);
        int groundVertexCount;

        for (int cz = 0; cz < ch; cz++)
        {
            for (int cx = 0; cx < cw; cx++)
            {
                float x0 = cx - halfX, z0 = cz - halfZ;
                int cell = cz * cw + cx;
                int v = verts.Count;
                for (int j = 0; j < 2; j++)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        int k = j * 2 + i; // BL, BR, TL, TR
                        verts.Add(new Vector3(x0 + i, vertexHeight[cell * 4 + k], z0 + j));
                        int w = (cell * 4 + k) * 7;
                        colors.Add(new Color(weights[w], weights[w + 1], weights[w + 2], weights[w + 3]));
                        uvs.Add(new Vector4(weights[w + 4], weights[w + 5], weights[w + 6], 0f));
                    }
                }
                groundTris.Add(v); groundTris.Add(v + 2); groundTris.Add(v + 1);
                groundTris.Add(v + 1); groundTris.Add(v + 2); groundTris.Add(v + 3);
            }
        }
        groundVertexCount = verts.Count;

        // Walls where the height differs across a cell edge
        for (int cz = 0; cz < ch; cz++)
        {
            for (int cx = 0; cx < cw; cx++)
            {
                int cell = cz * cw + cx;
                float x0 = cx - halfX, z0 = cz - halfZ;
                float bl = vertexHeight[cell * 4], br = vertexHeight[cell * 4 + 1], tl = vertexHeight[cell * 4 + 2], tr = vertexHeight[cell * 4 + 3];

                if (cx > 0)
                {
                    int nb = cell - 1;
                    float nbBR = vertexHeight[nb * 4 + 1], nbTR = vertexHeight[nb * 4 + 3];
                    if (Mathf.Abs(bl - nbBR) > WallEpsilon || Mathf.Abs(tl - nbTR) > WallEpsilon)
                    {
                        int cliff = CliffFor(cell, nb, cx, cz, cx - 1, cz, bl + tl > nbBR + nbTR);
                        AddWall(verts, colors, uvs, wallTris, cliff,
                            new Vector3(x0, nbBR, z0), new Vector3(x0, bl, z0),
                            new Vector3(x0, nbTR, z0 + 1), new Vector3(x0, tl, z0 + 1), Vector3.right);
                    }
                }
                if (cz > 0)
                {
                    int nb = cell - cw;
                    float nbTL = vertexHeight[nb * 4 + 2], nbTR = vertexHeight[nb * 4 + 3];
                    if (Mathf.Abs(bl - nbTL) > WallEpsilon || Mathf.Abs(br - nbTR) > WallEpsilon)
                    {
                        int cliff = CliffFor(cell, nb, cx, cz, cx, cz - 1, bl + br > nbTL + nbTR);
                        AddWall(verts, colors, uvs, wallTris, cliff,
                            new Vector3(x0 + 1, nbTR, z0), new Vector3(x0 + 1, br, z0),
                            new Vector3(x0, nbTL, z0), new Vector3(x0, bl, z0), Vector3.forward);
                    }
                }
            }
        }

        var mesh = new Mesh { name = "Terrain", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(verts);
        mesh.SetColors(colors);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(groundTris, 0);
        mesh.SetTriangles(wallTris, 1);
        mesh.RecalculateNormals();
        SmoothGroundNormals(mesh, groundVertexCount);
        mesh.RecalculateBounds();
        GroundMesh = mesh;

        GetComponent<MeshFilter>().sharedMesh = mesh;
        GetComponent<MeshRenderer>().sharedMaterials = new[] { groundMaterial, wallMaterial };
        GetComponent<MeshCollider>().sharedMesh = mesh;

        BuildWater(waterCell, halfX, halfZ);
        BuildMinimapTexture(waterCell);
        BuildShip();
        if (applyAtmosphere) ApplyAtmosphere();
    }

    // ------------------------------------------------------------------------------------------------ heights / weights

    private void ComputeCell(int cx, int cz, float[] vertexHeight, float[] weights, float[] cellCenter, bool[] waterCell)
    {
        int tx = cx >> 1, tz = cz >> 1, qx = cx & 1, qz = cz & 1;

        int own = data.CornerIndex(tx + qx, tz + qz);
        int xNb = data.CornerIndex(tx + 1 - qx, tz + qz);
        int zNb = data.CornerIndex(tx + qx, tz + 1 - qz);
        int c00 = data.CornerIndex(tx, tz), c10 = data.CornerIndex(tx + 1, tz);
        int c01 = data.CornerIndex(tx, tz + 1), c11 = data.CornerIndex(tx + 1, tz + 1);

        bool rampTile = HasRamp(c00) || HasRamp(c10) || HasRamp(c01) || HasRamp(c11);

        float hOwn = data.cornerHeight[own];
        float hx = rampTile || data.cornerLayer[own] == data.cornerLayer[xNb] ? (hOwn + data.cornerHeight[xNb]) * 0.5f : hOwn;
        float hz = rampTile || data.cornerLayer[own] == data.cornerLayer[zNb] ? (hOwn + data.cornerHeight[zNb]) * 0.5f : hOwn;

        float sum = 0f; int n = 0;
        foreach (int c in new[] { c00, c10, c01, c11 })
        {
            if (rampTile || data.cornerLayer[c] == data.cornerLayer[own]) { sum += data.cornerHeight[c]; n++; }
        }
        float hc = sum / n;

        int cell = cz * data.cellsX + cx;
        for (int j = 0; j < 2; j++)
        {
            for (int i = 0; i < 2; i++)
            {
                int k = j * 2 + i;
                bool isOwn = i == qx && j == qz;
                bool isXMid = j == qz && i != qx;
                bool isZMid = i == qx && j != qz;

                vertexHeight[cell * 4 + k] = isOwn ? hOwn : isXMid ? hx : isZMid ? hz : hc;

                // Ground tile weights: the corner itself, the midpoint of two corners, or the tile centre
                int w = (cell * 4 + k) * 7;
                if (isOwn) Add(weights, w, data.cornerGround[own], 1f);
                else if (isXMid) { Add(weights, w, data.cornerGround[own], 0.5f); Add(weights, w, data.cornerGround[xNb], 0.5f); }
                else if (isZMid) { Add(weights, w, data.cornerGround[own], 0.5f); Add(weights, w, data.cornerGround[zNb], 0.5f); }
                else
                {
                    Add(weights, w, data.cornerGround[c00], 0.25f); Add(weights, w, data.cornerGround[c10], 0.25f);
                    Add(weights, w, data.cornerGround[c01], 0.25f); Add(weights, w, data.cornerGround[c11], 0.25f);
                }
            }
        }

        cellCenter[cell] = (vertexHeight[cell * 4] + vertexHeight[cell * 4 + 1] + vertexHeight[cell * 4 + 2] + vertexHeight[cell * 4 + 3]) * 0.25f;
        waterCell[cell] = (data.cornerFlags[own] & TerrainMapData.FlagWater) != 0;
    }

    private bool HasRamp(int corner) => (data.cornerFlags[corner] & TerrainMapData.FlagRamp) != 0;

    private static void Add(float[] weights, int offset, int tileType, float amount)
    {
        if (tileType < 7) weights[offset + tileType] += amount;
    }

    // ------------------------------------------------------------------------------------------------ walls

    /// <summary>Which cliff texture a wall uses: that of the higher side's owning corner (falls back to the snow cliff).</summary>
    private int CliffFor(int cell, int neighbour, int cx, int cz, int nx, int nz, bool cellIsHigher)
    {
        int hx = cellIsHigher ? cx : nx, hz = cellIsHigher ? cz : nz;
        int corner = data.CornerIndex((hx >> 1) + (hx & 1), (hz >> 1) + (hz & 1));
        int cliff = data.cornerCliff[corner];
        return cliff < data.cliffTileIds.Length ? cliff : Mathf.Min(1, data.cliffTileIds.Length - 1);
    }

    private static void AddWall(List<Vector3> verts, List<Color> colors, List<Vector4> uvs, List<int> tris, int cliff,
        Vector3 b1, Vector3 t1, Vector3 b2, Vector3 t2, Vector3 towardCurrent)
    {
        float currentMinusNeighbour = (t1.y - b1.y) + (t2.y - b2.y);
        bool endsAgree = (t1.y - b1.y) * (t2.y - b2.y) >= 0f;

        if (currentMinusNeighbour > 0f && endsAgree) AddWallFace(verts, colors, uvs, tris, cliff, b1, t1, b2, t2, -towardCurrent);
        else if (currentMinusNeighbour < 0f && endsAgree) AddWallFace(verts, colors, uvs, tris, cliff, b1, t1, b2, t2, towardCurrent);
        else
        {
            // Twisted edge: draw both faces
            AddWallFace(verts, colors, uvs, tris, cliff, b1, t1, b2, t2, towardCurrent);
            AddWallFace(verts, colors, uvs, tris, cliff, b1, t1, b2, t2, -towardCurrent);
        }
    }

    /// <summary>Adds the quad with its front face toward <paramref name="outward"/> (Unity front faces are clockwise).</summary>
    private static void AddWallFace(List<Vector3> verts, List<Color> colors, List<Vector4> uvs, List<int> tris, int cliff,
        Vector3 b1, Vector3 t1, Vector3 b2, Vector3 t2, Vector3 outward)
    {
        int v = verts.Count;
        verts.Add(b1); verts.Add(t1); verts.Add(b2); verts.Add(t2);
        var weight = new Color(cliff == 0 ? 1f : 0f, cliff == 1 ? 1f : 0f, 0f, 0f);
        for (int i = 0; i < 4; i++) { colors.Add(weight); uvs.Add(Vector4.zero); }

        bool windingA = Vector3.Dot(Vector3.Cross(t1 - b1, b2 - b1), outward) >= 0f;
        if (windingA)
        {
            tris.Add(v); tris.Add(v + 1); tris.Add(v + 2);
            tris.Add(v + 2); tris.Add(v + 1); tris.Add(v + 3);
        }
        else
        {
            tris.Add(v); tris.Add(v + 2); tris.Add(v + 1);
            tris.Add(v + 2); tris.Add(v + 3); tris.Add(v + 1);
        }
    }

    /// <summary>
    /// The ground is drawn as separate 1x1 quads; averaging normals of vertices that share a position (and height) makes the
    /// lighting smooth across cells. Walls keep their flat face normals.
    /// </summary>
    private static void SmoothGroundNormals(Mesh mesh, int groundVertexCount)
    {
        Vector3[] vertices = mesh.vertices;
        Vector3[] normals = mesh.normals;
        var accumulated = new Dictionary<long, Vector3>(groundVertexCount / 2);

        long Key(Vector3 p) =>
            ((long)Mathf.RoundToInt(p.x * 4) + 2048) << 40 | ((long)Mathf.RoundToInt(p.z * 4) + 2048) << 20 | ((long)Mathf.RoundToInt(p.y * 200) + 100000);

        for (int i = 0; i < groundVertexCount; i++)
        {
            long key = Key(vertices[i]);
            accumulated.TryGetValue(key, out Vector3 sum);
            accumulated[key] = sum + normals[i];
        }
        for (int i = 0; i < groundVertexCount; i++)
            normals[i] = accumulated[Key(vertices[i])].normalized;
        mesh.normals = normals;
    }

    // ------------------------------------------------------------------------------------------------ water, minimap, ship

    private void BuildWater(bool[] waterCell, float halfX, float halfZ)
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        for (int cz = 0; cz < data.cellsZ; cz++)
        {
            for (int cx = 0; cx < data.cellsX; cx++)
            {
                if (!waterCell[cz * data.cellsX + cx]) continue;
                int own = data.CornerIndex((cx >> 1) + (cx & 1), (cz >> 1) + (cz & 1));
                float y = data.cornerWater[own];
                float x0 = cx - halfX, z0 = cz - halfZ;
                int v = verts.Count;
                verts.Add(new Vector3(x0, y, z0)); verts.Add(new Vector3(x0 + 1, y, z0));
                verts.Add(new Vector3(x0, y, z0 + 1)); verts.Add(new Vector3(x0 + 1, y, z0 + 1));
                for (int i = 0; i < 4; i++) uvs.Add(new Vector2(verts[v + i].x, verts[v + i].z));
                tris.Add(v); tris.Add(v + 2); tris.Add(v + 1);
                tris.Add(v + 1); tris.Add(v + 2); tris.Add(v + 3);
            }
        }
        if (verts.Count == 0) return;

        var water = new GameObject("Water");
        water.transform.SetParent(transform, false);
        var mesh = new Mesh { name = "Water", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        water.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = water.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = waterMaterial;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private void BuildMinimapTexture(bool[] waterCell)
    {
        var tex = new Texture2D(data.cellsX, data.cellsZ, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color32[data.cellsX * data.cellsZ];
        var blocked = new Color32(24, 34, 56, 255);
        var noBuild = new Color32(70, 130, 200, 255);
        var buildable = new Color32(215, 232, 248, 255);
        var deep = new Color32(80, 60, 170, 255);
        for (int i = 0; i < pixels.Length; i++)
        {
            byte flags = data.cellPathing[i];
            pixels[i] = waterCell[i] ? deep : (flags & TerrainMapData.PathBlocked) != 0 ? blocked : (flags & TerrainMapData.PathNoBuild) != 0 ? noBuild : buildable;
        }
        tex.SetPixels32(pixels);
        tex.Apply(false);
        MinimapTexture = tex;
    }

    /// <summary>The map places a transport ship at the exit ("Load"), where creeps board. A simple stand-in until real models exist.</summary>
    private void BuildShip()
    {
        float y = data.cornerWater[0];
        var root = new GameObject("Ship");
        root.transform.SetParent(transform, false);
        root.transform.position = new Vector3(ShipX, y, ShipZ);

        GameObject Part(PrimitiveType type, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = MaterialCache.Get(renderer.sharedMaterial, color);
            return go;
        }
        Part(PrimitiveType.Cube, new Vector3(0, 0.4f, 0), new Vector3(9f, 1.2f, 3.2f), new Color(0.24f, 0.17f, 0.13f));
        Part(PrimitiveType.Cube, new Vector3(-2.2f, 1.4f, 0), new Vector3(2.6f, 1.2f, 2.2f), new Color(0.55f, 0.50f, 0.42f));
        Part(PrimitiveType.Cylinder, new Vector3(1.2f, 2.4f, 0), new Vector3(0.18f, 2.4f, 0.18f), new Color(0.32f, 0.26f, 0.20f));
        Part(PrimitiveType.Cube, new Vector3(1.2f, 3.0f, 0), new Vector3(0.1f, 2.2f, 2.4f), new Color(0.85f, 0.87f, 0.9f));
    }

    private void ApplyAtmosphere()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor;
    }
}
