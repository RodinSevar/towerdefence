using UnityEngine;
using UnityEditor;
using System.IO;

public class WPMImporter
{
    [MenuItem("Tools/Import WC3 Pathing Map (.wpm)")]
    public static void ImportWPM()
    {
        string path = "mpq_files/war3map.wpm";
        if (!File.Exists(path))
        {
            Debug.LogError("Could not find war3map.wpm in the mpq_files folder!");
            return;
        }

        byte[] fileData = File.ReadAllBytes(path);
        
        // Header Format:
        // 4 bytes: MP3W (Magic String)
        // 4 bytes: version (Usually 0)
        // 4 bytes: width (in 32x32 pathing cells)
        // 4 bytes: height (in 32x32 pathing cells)
        
        if (fileData.Length < 16)
        {
            Debug.LogError("File is too small to be a valid WPM file.");
            return;
        }

        string magic = System.Text.Encoding.ASCII.GetString(fileData, 0, 4);
        if (magic != "MP3W" && magic != "W3PM")
        {
            Debug.LogError("Invalid WPM magic string: " + magic + ". Are you sure this is a Warcraft 3 Pathing Map?");
            return;
        }

        int width = System.BitConverter.ToInt32(fileData, 8);
        int height = System.BitConverter.ToInt32(fileData, 12);

        Debug.Log($"Parsing WC3 Pathing Map: {width}x{height} nodes.");

        // Unity grid uses 64x64 WC3 units (which is 2x2 WC3 pathing cells).
        // Therefore, our output PNG should be exactly half the dimensions of the WPM file!
        int unityWidth = width / 2;
        int unityHeight = height / 2;

        Texture2D tex = new Texture2D(unityWidth, unityHeight, TextureFormat.RGB24, false);

        int dataOffset = 16;
        for (int y = 0; y < unityHeight; y++)
        {
            for (int x = 0; x < unityWidth; x++)
            {
                // Read the 2x2 block of pathing cells from the WPM binary
                int wx = x * 2;
                int wy = y * 2;
                
                byte b00 = fileData[dataOffset + (wy * width) + wx];
                byte b10 = fileData[dataOffset + (wy * width) + (wx + 1)];
                byte b01 = fileData[dataOffset + ((wy + 1) * width) + wx];
                byte b11 = fileData[dataOffset + ((wy + 1) * width) + (wx + 1)];

                // Combine the bitmasks of the 4 cells
                byte combined = (byte)(b00 | b10 | b01 | b11);

                // Warcraft 3 Pathing Bitmasks:
                // 0x02 = Unwalkable
                // 0x08 = Unbuildable
                // 0x40 / 0x80 = Often used for unbuildable borders or blight
                bool unwalkable = (combined & 0x02) != 0;
                bool unbuildable = (combined & 0x08) != 0 || (combined & 0x40) != 0 || (combined & 0x80) != 0;

                Color c = Color.black; // Default: Fully Walkable & Buildable (The Maze Lanes)
                
                if (unwalkable)
                {
                    c = Color.white; // Unwalkable -> Turn into a Cliff Wall
                }
                else if (unbuildable)
                {
                    c = Color.blue; // Walkable but NOT buildable -> Turn into a Ramp / Safe Zone
                }

                tex.SetPixel(x, y, c);
            }
        }
        
        tex.Apply();

        string outPath = "Assets/Resources/MapLayout.png";
        if (!Directory.Exists("Assets/Resources")) Directory.CreateDirectory("Assets/Resources");
        
        File.WriteAllBytes(outPath, tex.EncodeToPNG());
        AssetDatabase.Refresh();

        // Automatically configure the Unity Texture Import settings
        TextureImporter importer = AssetImporter.GetAtPath(outPath) as TextureImporter;
        if (importer != null)
        {
            importer.isReadable = true;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        
        Debug.Log($"Successfully converted war3map.wpm into MapLayout.png! Output size is {unityWidth}x{unityHeight}. You can now run the game to see the map!");
    }
}
