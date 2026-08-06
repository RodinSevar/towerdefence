using UnityEngine;
using UnityEditor;
using System.IO;

public class TestPixels
{
    public static void Run()
    {
        byte[] bytes = File.ReadAllBytes("Assets/Resources/MapLayout.png");
        Texture2D tex = new Texture2D(2, 2);
        tex.LoadImage(bytes);
        
        int black = 0;
        int white = 0;
        int blue = 0;
        int other = 0;
        
        Color32[] pixels = tex.GetPixels32();
        foreach (var p in pixels)
        {
            if (p.r == 0 && p.g == 0 && p.b == 0) black++;
            else if (p.r == 255 && p.g == 255 && p.b == 255) white++;
            else if (p.r == 0 && p.g == 0 && p.b == 255) blue++;
            else other++;
        }
        
        Debug.Log($"Black: {black}, White: {white}, Blue: {blue}, Other: {other}");
    }
}
