using UnityEngine;

// World-space arrow that circles the player and points at the nearest sun.
//
// Lives on its own GameObject with a SpriteRenderer, ideally at scene root. It writes its own
// world position and rotation every frame, so even if it's parented to the player, the ship's
// mouse-aim spin can't drag it around.
public class SunPointer : MonoBehaviour
{
    public Transform player;       // auto-found by the "Player" tag if left empty
    public float radius = 1.2f;    // distance from the player's centre, in world units
    public float hideWithin = 9f;  // hidden while the nearest sun is closer than this - roughly on screen. 0 = always show
    [Tooltip("0 for right-pointing art (minimap_star_arrow), -90 for up-pointing.")]
    public float angleOffset = 0f;
    public float spinSpeed = 180f; // degrees/sec the arrow rolls around its own shaft. 0 = no spin

    OrbitZone[] suns;
    float spin;
    SpriteRenderer sprite;

    void Start()
    {
        sprite = GetComponent<SpriteRenderer>();

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }

        // ponytail: scanned once, same as MinimapCompass - suns are placed in the scene and nothing
        // spawns them. If they ever spawn at runtime, rescan on a timer.
        suns = FindObjectsByType<OrbitZone>(FindObjectsSortMode.None);
    }

    // LateUpdate so it follows the player's final position for the frame.
    void LateUpdate()
    {
        Transform nearest = null;
        float bestSqr = float.MaxValue;
        if (player != null)
        {
            foreach (OrbitZone sun in suns)
            {
                if (sun == null || !sun.isActiveAndEnabled) continue;
                float sqr = ((Vector2)(sun.transform.position - player.position)).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; nearest = sun.transform; }
            }
        }

        // Hide by disabling the renderer, not the GameObject - this script lives on the same object,
        // and SetActive(false) would stop LateUpdate from ever bringing it back.
        bool show = nearest != null && bestSqr > hideWithin * hideWithin;
        if (sprite != null) sprite.enabled = show;
        if (!show) return;

        Vector2 dir = ((Vector2)(nearest.position - player.position)).normalized;
        transform.position = new Vector3(player.position.x + dir.x * radius, player.position.y + dir.y * radius, player.position.z);
        // Rolls around the shaft rather than a fixed Y axis - a Y spin would flip the tip away from the
        // sun half the time. Read right to left: turn the art to lie along +X, roll around +X, then aim.
        // Scaled time, so the spin slows with bash/kick slow-mo and stops on pause like the rest of the world.
        spin = Mathf.Repeat(spin + spinSpeed * Time.deltaTime, 360f);
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg)
                           * Quaternion.AngleAxis(spin, Vector3.right)
                           * Quaternion.Euler(0f, 0f, angleOffset);
    }
}
