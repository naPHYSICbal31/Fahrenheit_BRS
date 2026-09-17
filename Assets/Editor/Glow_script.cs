using UnityEngine;
using UnityEditor;
using System.IO;

public class MakeGlowTexture
{
    [MenuItem("Tools/Generate Glow Sprite")]
    static void Generate()
    {
        int size = 512;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Vector2 c = new Vector2(size / 2f, size / 2f);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x, y), c) / (size / 2f);
            float a = Mathf.Clamp01(1f - d);
            a = Mathf.Pow(a, 2.2f);              // falloff curve
            tex.SetPixel(x, y, new Color(1, 1, 1, a));
        }

        tex.Apply();
        File.WriteAllBytes(Application.dataPath + "/Glow.png", tex.EncodeToPNG());
        AssetDatabase.Refresh();
        Debug.Log("Created Assets/Glow.png");
    }
}