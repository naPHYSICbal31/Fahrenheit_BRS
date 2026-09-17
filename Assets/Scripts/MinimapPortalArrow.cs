using UnityEngine;
using UnityEngine.UI;

// Put this on a UI arrow Image inside the minimap (child of MinimapBackground).
// Auto-finds the player and the portal, stays hidden until the portal is open,
// then points from the player toward the portal every frame. Persists for the level.
public class MinimapPortalArrow : MonoBehaviour
{
    public Transform player;      // optional - auto-found by "Player" tag if left empty
    public Transform portal;      // optional - auto-found from OverdriveBar.portal if left empty
    public float angleOffset = 0f; // arrow art points RIGHT by default -> 0. Set -90 if it points up.

    private RectTransform rect;
    private Image img;

    void Start()
    {
        rect = GetComponent<RectTransform>();
        img = GetComponent<Image>();

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
        if (portal == null)
        {
            OverdriveBar bar = FindFirstObjectByType<OverdriveBar>();
            if (bar != null && bar.portal != null) portal = bar.portal.transform;
        }

        Debug.Log($"[MinimapArrow] Start: player={(player != null ? "resolved" : "NULL")}, portal={(portal != null ? "resolved" : "NULL")}");
    }

    void Update()
    {
        bool portalOpen = portal != null && portal.gameObject.activeInHierarchy;

        if (img != null) img.enabled = portalOpen;

        if (!portalOpen || player == null) return;

        Vector2 dir = (Vector2)(portal.position - player.position);
        if (dir.sqrMagnitude < 0.0001f) return;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        rect.rotation = Quaternion.Euler(0, 0, angle + angleOffset);
    }
}
