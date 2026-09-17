using UnityEngine;
using UnityEditor;
using System.IO;

public class MakeSmileSprite
{
    [MenuItem("Tools/Generate Smile Sprite")]
    static void Generate()
    {
        int   size     = 256;
        float R        = size * 0.32f;   // arc radius — bigger = wider, flatter smile
        float halfThick= size * 0.055f;  // stroke thickness
        float halfSpan = 1.05f;          // ~60° each side of bottom; more = longer smile
        float aa       = 1.5f;           // antialias width in pixels

        float cx = size / 2f;
        float cy = size / 2f + R * 0.75f;   // push circle centre up so arc sits centred
        Vector2 c = new Vector2(cx, cy);

        // endpoints, for rounded caps
        Vector2 endL = c + new Vector2(Mathf.Cos(-Mathf.PI/2 - halfSpan),
                                       Mathf.Sin(-Mathf.PI/2 - halfSpan)) * R;
        Vector2 endR = c + new Vector2(Mathf.Cos(-Mathf.PI/2 + halfSpan),
                                       Mathf.Sin(-Mathf.PI/2 + halfSpan)) * R;

        Color ink = new Color32(0x2A, 0x25, 0x50, 255);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
            Vector2 d = p - c;

            float angDeg = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            float diff   = Mathf.DeltaAngle(-90f, angDeg) * Mathf.Deg2Rad;

            float dist;
            if (Mathf.Abs(diff) <= halfSpan)
                dist = Mathf.Abs(d.magnitude - R);          // on the arc
            else
                dist = Mathf.Min(Vector2.Distance(p, endL), // past the ends → round cap
                                 Vector2.Distance(p, endR));

            float a = Mathf.Clamp01((halfThick - dist) / aa);
            tex.SetPixel(x, y, new Color(ink.r, ink.g, ink.b, a));
        }

        tex.Apply();
        File.WriteAllBytes(Application.dataPath + "/Smile.png", tex.EncodeToPNG());
        AssetDatabase.Refresh();
        Debug.Log("Created Assets/Smile.png");
    }
}