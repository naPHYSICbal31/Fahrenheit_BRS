using UnityEngine;

// Keeps the minimap north-up and spins the player arrow instead of the map.
//
// Position is copied from the player; rotation is forced to identity. PlayerController
// rotates the player's ROOT transform to face the mouse, so a minimap camera parented
// to the player inherits that rotation and the whole map spins. Owning rotation here
// means that bug can't come back if someone re-parents the camera - though sitting at
// scene root is still tidier.
//
// Because the camera is always centred on the player, the arrow never moves. It only rotates.
public class MinimapCompass : MonoBehaviour
{
    public Transform player;         // auto-found by the "Player" tag if left empty
    public Transform minimapCamera;  // defaults to this object's own transform
    public Transform playerArrow;    // the UI arrow inside MinimapBackground

    [Tooltip("90 for right-pointing arrow art, 0 for up-pointing.")]
    public float arrowAngleOffset = 90f;

    [Header("Nearest Star")]
    public RectTransform starArrow;   // UI arrow under MinimapBorder (outside the Mask), anchored middle-center
    // Where the arrow's centre sits, as a fraction of the border's half-size. 0.976 is the stroke
    // centreline of minimap_ring (245px of its 251px half-slice), so the arrow is half inside, half outside.
    [Range(0f, 1f)] public float starArrowRim = 0.976f;
    [Tooltip("0 for right-pointing arrow art, -90 for up-pointing. Not the same as Arrow Angle Offset - that one works from the player's rotation, this one from a direction.")]
    public float starArrowAngleOffset = 0f;

    Camera cam;
    OrbitZone[] stars;

    void Start()
    {
        if (minimapCamera == null) minimapCamera = transform;
        cam = minimapCamera.GetComponent<Camera>();

        // ponytail: scanned once - every star is placed in the scene and nothing spawns one.
        // If stars ever spawn at runtime, rescan here on a timer instead.
        stars = FindObjectsByType<OrbitZone>(FindObjectsSortMode.None);

        if (player == null)
        {
            GameObject p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) player = p.transform;
        }
    }

    // LateUpdate so this runs after the player has been moved and rotated for the frame.
    void LateUpdate()
    {
        if (player == null) return;

        if (minimapCamera != null)
        {
            Vector3 pos = minimapCamera.position;
            minimapCamera.position = new Vector3(player.position.x, player.position.y, pos.z); // keep the camera's own Z
            minimapCamera.rotation = Quaternion.identity;
        }

        if (playerArrow != null)
        {
            playerArrow.localRotation = Quaternion.Euler(0f, 0f, player.eulerAngles.z + arrowAngleOffset);
        }

        if (starArrow != null) PointAtNearestStar();
    }

    // Pins an arrow to the minimap rim pointing at the closest star, and hides it once that
    // star is on the map itself. The map is north-up, so world direction IS map direction.
    void PointAtNearestStar()
    {
        Transform nearest = null;
        float bestSqr = float.MaxValue;
        foreach (OrbitZone star in stars)
        {
            if (star == null || !star.isActiveAndEnabled) continue;
            float sqr = ((Vector2)(star.transform.position - player.position)).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; nearest = star.transform; }
        }

        // Square render texture + circular mask: the map shows orthographicSize world units in every direction.
        float viewRadius = cam != null ? cam.orthographicSize : 0f;
        bool offMap = nearest != null && bestSqr > viewRadius * viewRadius;
        starArrow.gameObject.SetActive(offMap);
        if (!offMap) return;

        Vector2 dir = ((Vector2)(nearest.position - player.position)).normalized;
        // Per axis, not Min(width, height): the border rect isn't quite square and the ring stretches
        // with it, so scaling each axis keeps the arrow on the stroke at every angle.
        Rect rim = ((RectTransform)starArrow.parent).rect;
        starArrow.anchoredPosition = new Vector2(dir.x * rim.width, dir.y * rim.height) * (0.5f * starArrowRim);
        starArrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + starArrowAngleOffset);
    }
}
