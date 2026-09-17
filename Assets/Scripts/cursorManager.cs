using UnityEngine;

public class CursorManager : MonoBehaviour
{
    public Texture2D crosshairTexture;
    public Vector2 hotSpot = Vector2.zero; // NEW: the pixel within the texture that acts as the "click point" — for a crosshair, this should usually be the exact center of the image, not (0,0)

    void Start()
    {
        Cursor.SetCursor(crosshairTexture, hotSpot, CursorMode.Auto);
    }
}