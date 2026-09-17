using UnityEngine;

// Attached at runtime (via AddComponent) to an enemy caught by the player's
// Solar Pull ability. Takes over movement entirely: drags the enemy straight
// toward a fixed target position at an accelerating rate, ignoring the usual
// enemy no-fly-zone clamp (the whole point here is to cross it), until it's
// close enough to count as "collided," at which point it triggers the
// enemy's normal death sequence via ISolarPullable.KillBySolarPull().
public class SolarPullVictim : MonoBehaviour
{
    [Header("Pull")]
    public float initialPullSpeed = 3f;
    public float pullAcceleration = 6f; // ramps up so it never stalls near the edge of the no-fly zone
    public float killDistance = 0.35f;  // how close to the target counts as "hit it"

    private Rigidbody2D rb;
    private ISolarPullable pullable;
    private float currentSpeed;
    private Vector2 targetPosition; // CHANGED: fixed at BeginPull time instead of reading OrbitZone.Instance every frame — that static always pointed at whichever sun last ran Awake(), not the one the player was actually orbiting when they pressed F.
    private bool hasTarget;

    // Called immediately after AddComponent by whatever spawned this (PlayerController).
    // CHANGED: now takes the sun position explicitly so this always pulls toward the sun
    // the player was orbiting at activation time, regardless of how many other OrbitZones exist.
    public void BeginPull(Rigidbody2D targetRb, ISolarPullable targetPullable, Vector2 sunPosition)
    {
        rb = targetRb;
        pullable = targetPullable;
        targetPosition = sunPosition;
        hasTarget = true;
        currentSpeed = initialPullSpeed;
        pullable.IsBeingPulled = true;
    }

    void FixedUpdate()
    {
        if (rb == null || !hasTarget) // CHANGED: no longer depends on OrbitZone.Instance being non-null
        {
            Destroy(this);
            return;
        }

        Vector2 toSun = targetPosition - rb.position; // CHANGED: uses the stored target instead of OrbitZone.Instance.transform.position
        float dist = toSun.magnitude;

        if (dist <= killDistance)
        {
            pullable?.KillBySolarPull();
            Destroy(this);
            return;
        }

        currentSpeed += pullAcceleration * Time.fixedDeltaTime;
        Vector2 dir = toSun / Mathf.Max(dist, 0.0001f);
        rb.MovePosition(rb.position + dir * currentSpeed * Time.fixedDeltaTime);
    }

    void OnDestroy()
    {
        // Harmless if the enemy is already being destroyed alongside us.
        if (pullable != null) pullable.IsBeingPulled = false;
    }
}