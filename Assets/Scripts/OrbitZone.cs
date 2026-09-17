using UnityEngine;

[RequireComponent(typeof(CircleCollider2D))]
public class OrbitZone : MonoBehaviour
{
    [Header("Orbit")]
    public float degreesPerSecond = 70f;
    public bool clockwise = false;
    public float settleSpeed = 4f;
    public float orbitRadius = 2.5f;

    [Header("Steering")]
    public float radiusInputSpeed = 2f;
    public float minRadius = 1.2f;
    public float maxRadius = 4f;

    [Header("Capture Easing")]
    [Tooltip("How quickly angular speed ramps from the player's actual incoming speed up to degreesPerSecond, in degrees/sec^2. Lower = smoother swing-in, higher = snappier catch.")]
    public float angularAccel = 720f;

    [Header("Enemy No-Fly Zone")]
    [Tooltip("Enemies (and enemy spawns) are kept out to minRadius + this padding — slightly beyond the player's own orbit boundary.")]
    public float enemyExclusionPadding = 0.75f;

    [Header("Release")]
    public float releaseBoost = 1.4f;
    public KeyCode releaseKey = KeyCode.Space;

    [Header("Refs")]
    public string playerTag = "Player";

    // NEW: this zone's own low-alpha preview ring, shown at EffectiveOrbitRadius whenever
    // nothing is currently captured here. Moved here from PlayerController so every OrbitZone
    // (every sun) gets its own independent preview instead of only whichever one happened to be
    // the last OrbitZone.Instance set.
    [Header("Idle Preview Ring")]
    public Color idleLineColor = Color.white;
    [Range(0f, 1f)] public float idleLineAlpha = 0.25f;
    public float idleLineWidth = 0.05f;
    [Range(16, 128)] public int idleLineSegments = 64;

    Rigidbody2D capturedRb;
    PlayerController capturedPc;
    PivotBash capturedBash;
    float angle;
    float currentRadius;
    float currentAngularSpeed;
    Vector3 lastIdleCenter;
    CircleCollider2D captureTrigger;
    LineRenderer idleLine; // NEW
    float lastIdleRadius = -1f; // NEW: cache so the ring only rebuilds its points when the radius actually changes

    public static OrbitZone Instance { get; private set; }

    // Single source of truth for "what radius does this zone actually orbit/capture at".
    // orbitRadius is clamped into [minRadius, maxRadius] (falling back to minRadius if left at 0)
    // so the capture trigger, the FixedUpdate settle target, and the idle preview ring all read
    // the exact same number instead of three separate raw reads of orbitRadius that could drift
    // apart whenever orbitRadius sits outside the min/max range.
    public float EffectiveOrbitRadius =>
        Mathf.Clamp(orbitRadius > 0f ? orbitRadius : minRadius, minRadius, maxRadius);

    void Awake()
    {
        Instance = this; // NOTE: still only tracks the most recently-Awake'd zone - fine for anything that only ever needs "a" sun (e.g. EnforceSunBoundary), but each zone's own idle ring below no longer depends on this at all, which is exactly what makes multiple suns work correctly now. Do NOT use this for Solar Pull's target — use PlayerController.currentOrbitZone instead, which tracks the actual captured sun.
        captureTrigger = GetComponent<CircleCollider2D>();
        SyncCaptureTriggerRadius();
        SetupIdleLine();
    }

    // Creates this zone's own dim preview ring at runtime, same approach PlayerController used
    // to use for its single shared idle ring.
    void SetupIdleLine()
    {
        GameObject idleLineObj = new GameObject("OrbitIdlePreview");
        idleLineObj.transform.SetParent(transform, false);
        idleLine = idleLineObj.AddComponent<LineRenderer>();
        idleLine.useWorldSpace = true;
        idleLine.loop = true;
        idleLine.positionCount = idleLineSegments;
        idleLine.startWidth = idleLineWidth;
        idleLine.endWidth = idleLineWidth;
        idleLine.material = new Material(Shader.Find("Sprites/Default"));
        SetIdleLineColor();
        idleLine.sortingOrder = -1; // sit low so it doesn't fight ship/UI sorting - adjust if it needs to match a specific sorting layer
        idleLine.enabled = false; // Update() below turns it on once EffectiveOrbitRadius is known
    }

    void SetIdleLineColor()
    {
        if (idleLine == null) return;
        Color c = idleLineColor;
        c.a = idleLineAlpha;
        idleLine.startColor = c;
        idleLine.endColor = c;
    }

    // Redraws (only when the radius actually changed) and shows/hides this zone's idle ring.
    // Hidden only while THIS zone currently has the player captured - the player's own bright
    // "attached" ring (PlayerController.orbitLine) takes over visually at that point. Every OTHER
    // sun's idle ring keeps showing independently the whole time, since each OrbitZone owns its own.
    void UpdateIdleVisual()
    {
        if (idleLine == null) return;

        if (capturedRb != null)
        {
            idleLine.enabled = false;
            return;
        }

        float radius = EffectiveOrbitRadius;
        Vector3 center = transform.position;

        if (!Mathf.Approximately(radius, lastIdleRadius) || center != lastIdleCenter)
        {
            DrawIdleCircle(radius);
            lastIdleRadius = radius;
            lastIdleCenter = center;
        }

        idleLine.enabled = true;
    }

    void DrawIdleCircle(float radius)
    {
        idleLine.positionCount = idleLineSegments;
        for (int i = 0; i < idleLineSegments; i++)
        {
            float a = (float)i / idleLineSegments * 360f * Mathf.Deg2Rad;
            Vector3 point = new Vector3(
                transform.position.x + Mathf.Cos(a) * radius,
                transform.position.y + Mathf.Sin(a) * radius,
                0f
            );
            idleLine.SetPosition(i, point);
        }
    }

    // Keeps the CircleCollider2D's actual radius equal to EffectiveOrbitRadius (accounting for
    // scale), both at runtime and live in the Editor via OnValidate below - so "touching the
    // orbit radius", "where you settle", and "what the ring shows" are always the same boundary.
    void OnValidate()
    {
        if (TryGetComponent(out CircleCollider2D col))
        {
            SyncCaptureTriggerRadius(col);
        }
        if (idleLine != null)
        {
            SetIdleLineColor(); // let alpha/color tweaks in the Inspector show immediately in Play mode
        }
    }

    void SyncCaptureTriggerRadius(CircleCollider2D col = null)
    {
        col = col != null ? col : captureTrigger;
        if (col == null) return;

        float scale = Mathf.Max(0.0001f, transform.lossyScale.x); // assumes uniform scale, same assumption OnDrawGizmosSelected already makes below
        col.radius = EffectiveOrbitRadius / scale;
    }

    public static bool TryGetBoundary(out Vector2 center, out float radius)
    {
        if (Instance == null)
        {
            center = default;
            radius = 0f;
            return false;
        }

        center = Instance.transform.position;
        radius = Instance.minRadius;
        return true;
    }

    public static Vector2 ClampOutsideBoundary(Vector2 position)
    {
        if (!TryGetBoundary(out Vector2 center, out float radius)) return position;
        if (radius <= 0f) return position;

        Vector2 offset = position - center;
        float dist = offset.magnitude;

        if (dist >= radius || dist <= 0.0001f) return position;

        Vector2 outwardDir = offset / dist;
        return center + outwardDir * radius;
    }

    public static bool TryGetEnemyExclusionZone(out Vector2 center, out float radius)
    {
        if (Instance == null)
        {
            center = default;
            radius = 0f;
            return false;
        }

        center = Instance.transform.position;
        radius = Instance.minRadius + Instance.enemyExclusionPadding;
        return true;
    }

    public static Vector2 ClampOutsideEnemyZone(Vector2 position)
    {
        if (!TryGetEnemyExclusionZone(out Vector2 center, out float radius)) return position;
        if (radius <= 0f) return position;

        Vector2 offset = position - center;
        float dist = offset.magnitude;

        if (dist >= radius || dist <= 0.0001f) return position;

        Vector2 outwardDir = offset / dist;
        return center + outwardDir * radius;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (capturedRb != null || !other.CompareTag(playerTag)) return;

        var pc = other.GetComponent<PlayerController>();
        var bash = other.GetComponent<PivotBash>();

        if (pc == null) return;
        if (bash != null && bash.IsBusy) return;

        capturedRb = other.GetComponent<Rigidbody2D>();
        capturedPc = pc;
        capturedBash = bash;

        Vector2 offset = (Vector2)other.transform.position - (Vector2)transform.position;
        angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;

        currentRadius = Mathf.Clamp(offset.magnitude, minRadius, maxRadius);

        // Derive the player's actual angular speed at the instant of capture from the
        // tangential component of their real incoming velocity, instead of snapping straight
        // to degreesPerSecond. FixedUpdate below then eases currentAngularSpeed from this
        // starting value up to the target speed over time (angularAccel).
        Vector2 radialDir = offset.sqrMagnitude > 0.0001f ? offset.normalized : Vector2.right;
        Vector2 tangentDir = new Vector2(-radialDir.y, radialDir.x); // 90° CCW from radial, matches the atan2/cos/sin convention used below
        float tangentialSpeed = Vector2.Dot(capturedRb.linearVelocity, tangentDir);
        float initialAngularSpeedRad = currentRadius > 0.0001f ? tangentialSpeed / currentRadius : 0f;
        currentAngularSpeed = initialAngularSpeedRad * Mathf.Rad2Deg;

        capturedPc.isOrbiting = true;
        capturedPc.currentOrbitZone = this; // NEW: remember which specific sun captured the player, so Solar Pull (and anything else that needs "the sun I'm at") targets THIS zone, not the static Instance
        capturedPc.SetOrbitVisual(transform.position, currentRadius);
    }

    void Update()
    {
        UpdateIdleVisual(); // CHANGED: now runs every frame regardless of capture state, so this zone's own dim ring stays current for every OrbitZone, not just whichever one was OrbitZone.Instance

        if (capturedRb == null) return;

        if (capturedBash != null && capturedBash.IsBusy)
        {
            Release(false);
            return;
        }

        if (Input.GetKeyDown(releaseKey)) Release(true);

        if (Input.GetKeyDown(KeyCode.R)) clockwise = !clockwise;
    }

    void FixedUpdate()
    {
        if (capturedRb == null) return;

        capturedRb.linearVelocity = Vector2.zero;

        float targetAngularSpeed = degreesPerSecond * (clockwise ? -1f : 1f);
        currentAngularSpeed = Mathf.MoveTowards(currentAngularSpeed, targetAngularSpeed, angularAccel * Time.fixedDeltaTime);
        angle += currentAngularSpeed * Time.fixedDeltaTime;

        float target = EffectiveOrbitRadius;

        currentRadius = Mathf.Lerp(
            currentRadius,
            target,
            Time.fixedDeltaTime * settleSpeed
        );

        float r = angle * Mathf.Deg2Rad;

        Vector2 pos = (Vector2)transform.position
                    + new Vector2(Mathf.Cos(r), Mathf.Sin(r)) * currentRadius;

        capturedRb.MovePosition(pos);

        capturedPc.SetOrbitVisual(transform.position, currentRadius);
    }

    void Release(bool withBoost)
    {
        if (capturedRb == null) return;

        capturedPc.isOrbiting = false;
        capturedPc.currentOrbitZone = null; // NEW: clear it so a stray F-press with no sun (isOrbiting should already gate this, but this keeps the two fields honest together) can't target a stale zone
        capturedPc.ClearOrbitVisual();

        if (withBoost)
        {
            Vector2 outwardDir =
                ((Vector2)capturedRb.position - (Vector2)transform.position).normalized;

            capturedPc.DashInDirection(outwardDir, true);
        }

        capturedRb = null;
        capturedPc = null;
        capturedBash = null;
    }

    void OnDrawGizmosSelected()
    {
        var col = GetComponent<CircleCollider2D>();

        if (col)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(
                transform.position,
                col.radius * transform.lossyScale.x
            );
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, minRadius);
        Gizmos.DrawWireSphere(transform.position, maxRadius);

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(
            transform.position,
            minRadius + enemyExclusionPadding
        );


    }
}