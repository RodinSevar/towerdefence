using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

public class MapLayoutImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (assetPath.Contains("MapLayout"))
        {
            TextureImporter importer = (TextureImporter)assetImporter;
            
            // Force Unity to NOT resize the 196x196 image to 256x256!
            importer.npotScale = TextureImporterNPOTScale.None;
            
            // Ensure exact pixel colors without blur or compression
            importer.isReadable = true;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.sRGBTexture = false; // Linear space for exact color matching
        }
    }
}
#endif
