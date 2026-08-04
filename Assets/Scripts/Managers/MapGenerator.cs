using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class MapGenerator : MonoBehaviour
{
    [SerializeField] private Texture2D mapTexture;
    [SerializeField] private float heightMultiplier = 2.0f;
    [SerializeField] private Material terrainMaterial;

    public void GenerateMap()
    {
        if (mapTexture == null)
        {
            mapTexture = CreateDefaultMap();
        }

        int width = mapTexture.width;
        int height = mapTexture.height;
        float cellSize = GridManager.Instance.GetCellSize();
        
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        // We center the map at (0,0)
        float offsetX = (width * cellSize) / 2f;
        float offsetZ = (height * cellSize) / 2f;

        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                Color pixel = mapTexture.GetPixel(x, z);
                bool isCliff = IsColorMatch(pixel, Color.white);

                Vector2Int cellCoord = new Vector2Int(x - width/2, z - height/2);
                
                if (isCliff)
                {
                    GridManager.Instance.OccupyCell(cellCoord);
                }
                
                // Calculate the 4 corners of this cell
                float startX = (x * cellSize) - offsetX;
                float startZ = (z * cellSize) - offsetZ;
                float endX = startX + cellSize;
                float endZ = startZ + cellSize;

                float yBL, yBR, yTL, yTR;
                GetCellCorners(x, z, out yBL, out yBR, out yTL, out yTR);
                
                if (IsColorMatch(pixel, Color.blue))
                {
                    GridManager.Instance.MarkUnbuildable(cellCoord); // Ramps can't be built on
                }

                // Set height in GridManager for center point
                float centerHeight = (yBL + yBR + yTL + yTR) / 4f;
                GridManager.Instance.SetCellHeight(cellCoord, centerHeight);

                // Add vertices for top face
                int vIndex = vertices.Count;
                vertices.Add(new Vector3(startX, yBL, startZ)); // BL
                vertices.Add(new Vector3(endX, yBR, startZ));   // BR
                vertices.Add(new Vector3(startX, yTL, endZ));   // TL
                vertices.Add(new Vector3(endX, yTR, endZ));     // TR

                // Use a single UV point for the whole face to sample a specific color from our texture palette
                Vector2 uvColor = GetUVForHeight(centerHeight);
                uvs.Add(uvColor);
                uvs.Add(uvColor);
                uvs.Add(uvColor);
                uvs.Add(uvColor);

                // Triangles (Clockwise)
                triangles.Add(vIndex);
                triangles.Add(vIndex + 2);
                triangles.Add(vIndex + 1);
                
                triangles.Add(vIndex + 1);
                triangles.Add(vIndex + 2);
                triangles.Add(vIndex + 3);

                // Draw walls by checking left and down
                if (x > 0)
                {
                    float left_yBL, left_yBR, left_yTL, left_yTR;
                    GetCellCorners(x - 1, z, out left_yBL, out left_yBR, out left_yTL, out left_yTR);

                    // If the heights don't match, draw a connecting wall
                    if (yBL != left_yBR || yTL != left_yTR)
                    {
                        DrawWall(vertices, triangles, uvs, 
                            new Vector3(startX, left_yBR, startZ), new Vector3(startX, yBL, startZ), 
                            new Vector3(startX, left_yTR, endZ), new Vector3(startX, yTL, endZ),
                            false);
                    }
                }
                
                if (z > 0)
                {
                    float down_yBL, down_yBR, down_yTL, down_yTR;
                    GetCellCorners(x, z - 1, out down_yBL, out down_yBR, out down_yTL, out down_yTR);

                    if (yBR != down_yTR || yBL != down_yTL)
                    {
                        DrawWall(vertices, triangles, uvs, 
                            new Vector3(endX, down_yTR, startZ), new Vector3(endX, yBR, startZ), 
                            new Vector3(startX, down_yTL, startZ), new Vector3(startX, yBL, startZ),
                            true);
                    }
                }
            }
        }

        Mesh mesh = new Mesh();
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();

        GetComponent<MeshFilter>().mesh = mesh;
        GetComponent<MeshCollider>().sharedMesh = mesh;
        
        // Create a 3-color palette texture and use the default 3D material to ensure proper depth sorting and lighting
        GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Plane);
        Material defaultMat = dummy.GetComponent<MeshRenderer>().sharedMaterial;
        Destroy(dummy);
        
        Material mat = new Material(defaultMat);
        
        Texture2D palette = new Texture2D(3, 1);
        palette.SetPixel(0, 0, new Color(0.1f, 0.3f, 0.1f)); // Dark Green (Low)
        palette.SetPixel(1, 0, new Color(0.5f, 0.25f, 0f)); // Brown (Walls/Ramps)
        palette.SetPixel(2, 0, new Color(0.4f, 0.4f, 0.4f)); // Gray (High)
        palette.filterMode = FilterMode.Point; // Crisp solid colors
        palette.Apply();

        if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", palette);
        if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", palette);
        
        GetComponent<MeshRenderer>().material = mat;
    }

    private Vector2 GetUVForHeight(float y)
    {
        if (y < 0.1f) return new Vector2(0.16f, 0.5f); // Point to left pixel (Green)
        if (y > heightMultiplier - 0.1f) return new Vector2(0.83f, 0.5f); // Point to right pixel (Gray)
        return new Vector2(0.5f, 0.5f); // Point to center pixel (Brown)
    }

    private bool IsColorMatch(Color pixel, Color target)
    {
        if (target == Color.black) return pixel.r < 0.2f && pixel.g < 0.2f && pixel.b < 0.2f;
        if (target == Color.white) return pixel.r > 0.8f && pixel.g > 0.8f && pixel.b > 0.8f;
        if (target == Color.blue) return pixel.b > pixel.r + 0.2f && pixel.b > pixel.g + 0.2f;
        // Assume gray if it's not extreme white/black and not heavily blue
        return pixel.r > 0.2f && pixel.r < 0.8f && pixel.g > 0.2f && pixel.g < 0.8f && Mathf.Abs(pixel.r - pixel.b) < 0.2f;
    }

    private void GetCellCorners(int x, int z, out float yBL, out float yBR, out float yTL, out float yTR)
    {
        Color pixel = (x >= 0 && x < mapTexture.width && z >= 0 && z < mapTexture.height) ? mapTexture.GetPixel(x, z) : Color.black;
        float y = 0f;
        if (IsColorMatch(pixel, Color.gray) || IsColorMatch(pixel, Color.white)) y = heightMultiplier;
        else if (IsColorMatch(pixel, Color.blue)) y = 0.5f * heightMultiplier;

        yBL = y; yBR = y; yTL = y; yTR = y;
        
        if (IsColorMatch(pixel, Color.blue))
        {
            Color left = x > 0 ? mapTexture.GetPixel(x-1, z) : Color.black;
            Color right = x < mapTexture.width-1 ? mapTexture.GetPixel(x+1, z) : Color.black;
            Color down = z > 0 ? mapTexture.GetPixel(x, z-1) : Color.black;
            Color up = z < mapTexture.height-1 ? mapTexture.GetPixel(x, z+1) : Color.black;

            if (IsColorMatch(left, Color.black) && IsColorMatch(right, Color.gray)) { yBL = 0; yTL = 0; yBR = heightMultiplier; yTR = heightMultiplier; }
            else if (IsColorMatch(left, Color.gray) && IsColorMatch(right, Color.black)) { yBL = heightMultiplier; yTL = heightMultiplier; yBR = 0; yTR = 0; }
            else if (IsColorMatch(down, Color.black) && IsColorMatch(up, Color.gray)) { yBL = 0; yBR = 0; yTL = heightMultiplier; yTR = heightMultiplier; }
            else if (IsColorMatch(down, Color.gray) && IsColorMatch(up, Color.black)) { yBL = heightMultiplier; yBR = heightMultiplier; yTL = 0; yTR = 0; }
        }
    }

    private void DrawWall(List<Vector3> vertices, List<int> triangles, List<Vector2> uvs, 
        Vector3 b1, Vector3 t1, Vector3 b2, Vector3 t2, bool flip)
    {
        int v = vertices.Count;
        vertices.Add(b1);
        vertices.Add(t1);
        vertices.Add(b2);
        vertices.Add(t2);

        // All walls are brown, so point UVs to the middle pixel
        Vector2 uvColor = new Vector2(0.5f, 0.5f);
        uvs.Add(uvColor);
        uvs.Add(uvColor);
        uvs.Add(uvColor);
        uvs.Add(uvColor);

        if (!flip)
        {
            // Forward facing triangles
            triangles.Add(v);
            triangles.Add(v + 1);
            triangles.Add(v + 2);

            triangles.Add(v + 2);
            triangles.Add(v + 1);
            triangles.Add(v + 3);
        }
        else
        {
            // Backward facing triangles
            triangles.Add(v);
            triangles.Add(v + 2);
            triangles.Add(v + 1);

            triangles.Add(v + 2);
            triangles.Add(v + 3);
            triangles.Add(v + 1);
        }
    }

    private Texture2D CreateDefaultMap()
    {
        int size = 196;
        Texture2D tex = new Texture2D(size, size);
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                // Default to High Ground (Gray)
                Color c = Color.gray;
                
                // Create a massive central canyon/field for mazing (Low Ground)
                if (x > 20 && x < 176 && y > 20 && y < 176)
                {
                    c = Color.black; // Low ground for building mazes
                    
                    // Add a central plateau for premium tower placement
                    if (x > 85 && x < 111 && y > 85 && y < 111)
                    {
                        c = Color.gray; // High ground in center
                        
                        // Cliffs around the entire plateau
                        if (x == 86 || x == 110 || y == 86 || y == 110)
                        {
                            c = Color.white;
                        }
                    }
                }
                
                // Add cliffs to the outer edges of the canyon
                if ((x == 20 || x == 176) && y > 20 && y < 176) c = Color.white;
                if ((y == 20 || y == 176) && x > 20 && x < 176) c = Color.white;
                
                // Add entrance and exit ramps to the canyon (Left and Right)
                if (x == 20 && y > 85 && y < 111) c = Color.blue; // Entrance Ramp
                if (x == 176 && y > 85 && y < 111) c = Color.blue; // Exit Ramp

                tex.SetPixel(x, y, c);
            }
        }
        tex.Apply();

        // Save it to the Resources folder so the user can edit it later!
        try
        {
            if (!System.IO.Directory.Exists("Assets/Resources"))
            {
                System.IO.Directory.CreateDirectory("Assets/Resources");
            }
            byte[] bytes = tex.EncodeToPNG();
            System.IO.File.WriteAllBytes("Assets/Resources/MapLayout.png", bytes);
            Debug.Log("Generated Wintermaul map and saved to Assets/Resources/MapLayout.png");
        }
        catch (System.Exception e)
        {
            Debug.LogError("Could not save map image: " + e.Message);
        }

        return tex;
    }
}
