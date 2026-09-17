using System.Collections;
using Unity.VisualScripting;
using UnityEngine;
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(TrailRenderer))]
public class PlayerController : MonoBehaviour
{
    public enum PlayerState { Phase1_Shooter, Phase2_Pinball }
    [Header("Game State")]
    public PlayerState currentState = PlayerState.Phase1_Shooter;
    [Header("Movement (WASD)")]
    public float moveSpeed = 8f;
    private Vector2 moveInput;
    private Rigidbody2D rb;
    [Header("Aiming (Mouse)")]
    private Camera mainCam;
    private Vector2 mousePosition;

    [Header("Rotation Settings")]
    public float spriteAngleOffset = -90f;
    public float rotationSensitivity = 720f; // degrees/sec — this IS your "slight lag" knob, lower = laggier

    [Header("Dash (Spacebar)")]
    public float dashSpeed = 24f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;
    private bool isDashing;
    private bool canDash = true;
    private TrailRenderer trail;
    [HideInInspector] public bool isBashAiming = false;
    [HideInInspector] public Vector2 bashAimDirection;

    [Header("Bicycle Kick (Spin)")]
    [HideInInspector] public bool isSpinning = false;
    public float spinSpeed = 540f; // deg/sec — real time, unaffected by slow-mo
    [HideInInspector] public bool isInputLocked = false; // set by BicycleKick during the post-release bounce grace window — freezes movement/aim/dash but NOT rotation, so the spin and mirror bounce still play out
    [HideInInspector] public float spinDirection = 1f; // NEW: set by BicycleKick from A/D while kicking — 1 = one way, -1 = reversed
    [HideInInspector] public bool isOrbiting = false; // set by OrbitZone — movement is driven by the orbit, but aiming/shooting stay live
    [HideInInspector] public OrbitZone currentOrbitZone; // NEW: the specific sun currently orbiting/captured — set/cleared by that OrbitZone itself. Used instead of OrbitZone.Instance (which is just "last Awake'd") anywhere we need THIS sun's position, e.g. Solar Pull.

    [Header("Orbit Path Visual")]
    public Color orbitLineColor = Color.white;
    [Range(0f, 1f)] public float idleOrbitLineAlpha = 0.25f; // opacity of the ring while not yet orbiting - a low-opacity preview of the capture radius
    [Range(0f, 1f)] public float activeOrbitLineAlpha = 1f;  // opacity once actually orbiting
    public float orbitLineWidth = 0.05f;
    [Range(16, 128)] public int orbitLineSegments = 64;
    private LineRenderer orbitLine;
    private Vector2 orbitCenter;
    private float orbitRadius;

    // Caches what the idle ring last drew, so UpdateIdleOrbitVisual() only redraws the
    // circle when something actually changed instead of rebuilding 64 points every frame.
    private Vector2 lastDrawnCenter;
    private float lastDrawnRadius = -1f;
    private float lastDrawnAlpha = -1f;

    [Header("Orbit Facing")]
    [Tooltip("Total arc in degrees the nose is allowed to swing away from the outward-facing direction while orbiting, following the mouse but clamped to this range (half each side).")]
    public float orbitAimClampRange = 45f;

    [Header("Solar Pull (Overdrive Ability)")]
    [Tooltip("Only usable while orbiting the sun.")]
    public KeyCode solarPullKey = KeyCode.F;
    public float solarPullRadius = 6f;
    [Range(0f, 1f)] public float solarPullCostFraction = 0.5f; // "half of the overdrive bar"
    public LayerMask solarPullEnemyMask = ~0; // narrow this to your Enemy layer in the Inspector if you have one
    public OverdriveBar overdriveBar;
    public GameObject solarPullVfxPrefab; // optional — instantiated at the player on activation

    public Vector2 FrontDirection => transform.up;
    public Vector2 BackDirection => -transform.up;
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        trail = GetComponent<TrailRenderer>();
        mainCam = Camera.main;
        trail.emitting = false;

        rb.constraints |= RigidbodyConstraints2D.FreezeRotation; // CHANGED: prevents physics (collisions, bumps) from ever rotating the Rigidbody — rotation is now 100% script-controlled

        SetupOrbitLine();
    }

    private void SetupOrbitLine()
    {
        // Created at runtime so you don't need to wire up a child GameObject by hand.
        GameObject orbitLineObj = new GameObject("OrbitPathVisual");
        orbitLine = orbitLineObj.AddComponent<LineRenderer>();
        orbitLine.useWorldSpace = true;
        orbitLine.loop = true;
        orbitLine.positionCount = orbitLineSegments;
        orbitLine.startWidth = orbitLineWidth;
        orbitLine.endWidth = orbitLineWidth;
        orbitLine.material = new Material(Shader.Find("Sprites/Default"));
        orbitLine.startColor = orbitLineColor;
        orbitLine.endColor = orbitLineColor;

        // Match the ship's own sorting layer so the ring lives in the same
        // rendering context, then sit one order behind it so the ship draws on top.
        SpriteRenderer shipRenderer = GetComponentInChildren<SpriteRenderer>();
        if (shipRenderer != null)
        {
            orbitLine.sortingLayerID = shipRenderer.sortingLayerID;
            orbitLine.sortingOrder = shipRenderer.sortingOrder - 1;
        }
        else
        {
            orbitLine.sortingOrder = 10;
        }

        orbitLine.enabled = false;
    }

    // Shows a dim preview ring at the OrbitZone's own EffectiveOrbitRadius whenever the
    // player isn't currently orbiting, so the attach point is visible ahead of time and always
    // matches exactly where the capture trigger and settle position actually are.
    private void UpdateIdleOrbitVisual()
    {
        if (OrbitZone.Instance == null)
        {
            if (orbitLine != null) orbitLine.enabled = false;
            return;
        }

        Vector2 center = OrbitZone.Instance.transform.position;
        float radius = OrbitZone.Instance.EffectiveOrbitRadius; // CHANGED: was a raw `orbitRadius > 0f ? orbitRadius : minRadius` read done here in PlayerController - now delegates entirely to OrbitZone's single clamped value, so it can never drift from the trigger/settle radius

        if (center == lastDrawnCenter && Mathf.Approximately(radius, lastDrawnRadius) && Mathf.Approximately(idleOrbitLineAlpha, lastDrawnAlpha))
        {
            return; // nothing changed since the last idle draw - skip rebuilding the circle
        }

        orbitCenter = center;
        orbitRadius = radius;
        ApplyOrbitLineAlpha(idleOrbitLineAlpha);
        DrawOrbitCircle();
        if (orbitLine != null) orbitLine.enabled = true;

        lastDrawnCenter = center;
        lastDrawnRadius = radius;
        lastDrawnAlpha = idleOrbitLineAlpha;
    }

    // Sets the ring's color to orbitLineColor with the given alpha.
    private void ApplyOrbitLineAlpha(float alpha)
    {
        if (orbitLine == null) return;
        Color c = orbitLineColor;
        c.a = alpha;
        orbitLine.startColor = c;
        orbitLine.endColor = c;
    }

    // Call this from OrbitZone the moment the player starts orbiting, passing the
    // star's position as the center and the orbit radius being used.
    public void SetOrbitVisual(Vector2 center, float radius)
    {
        orbitCenter = center;
        orbitRadius = radius;
        ApplyOrbitLineAlpha(activeOrbitLineAlpha);
        DrawOrbitCircle();
        if (orbitLine != null) orbitLine.enabled = true;

        lastDrawnRadius = -1f; // invalidate the idle cache so it's guaranteed to redraw once orbiting ends
    }

    // Call this from OrbitZone when the player stops orbiting.
    public void ClearOrbitVisual()
    {
        lastDrawnRadius = -1f;
    }

    private void DrawOrbitCircle()
    {
        if (orbitLine == null) return;
        orbitLine.positionCount = orbitLineSegments;
        for (int i = 0; i < orbitLineSegments; i++)
        {
            float angle = (float)i / orbitLineSegments * 360f * Mathf.Deg2Rad;
            Vector3 point = new Vector3(
                orbitCenter.x + Mathf.Cos(angle) * orbitRadius,
                orbitCenter.y + Mathf.Sin(angle) * orbitRadius,
                0f
            );
            orbitLine.SetPosition(i, point);
        }
    }

    void Update()
    {
        if (currentState != PlayerState.Phase1_Shooter) return;

        if (!isOrbiting) UpdateIdleOrbitVisual();

        if (isDashing) return;
        if (isInputLocked) return; // NEW: freeze movement/aim/dash input during the bicycle-kick bounce grace period

        // Kept current regardless of state — orbit facing now reads the mouse too.
        mousePosition = mainCam.ScreenToWorldPoint(Input.mousePosition);

        if (isOrbiting)
        {
            // NEW: Solar Pull can only be triggered while orbiting.
            if (Input.GetKeyDown(solarPullKey))
            {
                TrySolarPull();
            }

            // WASD does nothing while orbiting: OrbitZone drives position on its own,
            // and facing below follows the mouse (clamped) instead of A/D.
            moveInput = Vector2.zero;
            return;
        }

        if (isSpinning) // NEW: while bicycle-kicking, WASD movement, mouse-snap rotation, and dashing are all locked out —
                        // the only input that does anything is A/D, which BicycleKick.UpdateSpinDirection() reads directly
                        // to flip spin direction. moveInput is zeroed here so FixedUpdate's velocity assignment goes to zero.
        {
            moveInput = Vector2.zero;
            return;
        }

        moveInput.x = Input.GetAxisRaw("Horizontal");
        moveInput.y = Input.GetAxisRaw("Vertical");
        moveInput = moveInput.normalized;

        if (Input.GetMouseButtonDown(1))
        {
            SnapPlayerRotation();
        }
        if (Input.GetKeyDown(KeyCode.Space) && canDash)
        {
            StartCoroutine(DashRoutine());
        }
    }

    // Spends half the overdrive bar, then grabs every ISolarPullable enemy within
    // solarPullRadius of the player and hands each one off to a SolarPullVictim,
    // which drags it into whichever sun the player is CURRENTLY orbiting.
    // CHANGED: previously handed no destination to SolarPullVictim, which fell back
    // to OrbitZone.Instance (the last-Awake'd sun) — broke with multiple suns, since
    // Instance never changed to match which one you were actually attached to. Now we
    // read currentOrbitZone (set by OrbitZone itself on capture/release) and pass its
    // position explicitly.
    private void TrySolarPull()
    {
        if (overdriveBar == null) return;
        if (currentOrbitZone == null) return; // safety — F is only read while isOrbiting, but guards against stale state

        int cost = Mathf.RoundToInt(overdriveBar.maxCharge * solarPullCostFraction);
        if (!overdriveBar.TrySpend(cost)) return;

        Vector2 sunPos = currentOrbitZone.transform.position;

        if (solarPullVfxPrefab != null)
        {
            Instantiate(solarPullVfxPrefab, transform.position, Quaternion.identity);
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, solarPullRadius, solarPullEnemyMask);
        foreach (Collider2D hit in hits)
        {
            var pullable = hit.GetComponent<ISolarPullable>();
            if (pullable == null || pullable.IsBeingPulled) continue;

            Rigidbody2D enemyRb = pullable.Rb2D;
            if (enemyRb == null) continue;

            SolarPullVictim victim = hit.gameObject.AddComponent<SolarPullVictim>();
            victim.BeginPull(enemyRb, pullable, sunPos); // CHANGED: now passes the target sun position explicitly
        }
    }

    void FixedUpdate()
    {
        if (currentState != PlayerState.Phase1_Shooter) return;

        UpdatePlayerRotation();

        if (!isDashing && !isInputLocked && !isOrbiting)   // ← this one
        {
            rb.linearVelocity = moveInput * moveSpeed;
        }

        EnforceSunBoundary();
    }

    // Keeps the ship from ever crossing inside the sun's protected radius — including mid-dash.
    // While orbiting, OrbitZone already clamps currentRadius between minRadius/maxRadius itself,
    // so this is a no-op there and only matters during free flight and dashing.
    private void EnforceSunBoundary()
    {
        if (isOrbiting) return;
        if (!OrbitZone.TryGetBoundary(out Vector2 sunPos, out float boundaryRadius)) return;
        if (boundaryRadius <= 0f) return;

        Vector2 offset = rb.position - sunPos;
        float dist = offset.magnitude;
        if (dist >= boundaryRadius || dist <= 0.0001f) return;

        Vector2 outwardDir = offset / dist;
        rb.position = sunPos + outwardDir * boundaryRadius;

        // Cancel whatever inward velocity pushed us here (e.g. a dash aimed at the sun) so the
        // ship stops dead at the boundary instead of fighting the clamp every fixed step.
        float inwardSpeed = Vector2.Dot(rb.linearVelocity, -outwardDir);
        if (inwardSpeed > 0f)
        {
            rb.linearVelocity += outwardDir * inwardSpeed;
        }
    }

    private float GetTargetAngle()
    {
        Vector2 lookDirection;
        if (isBashAiming)
        {
            lookDirection = bashAimDirection;
        }
        else
        {
            lookDirection = mousePosition - rb.position;
        }

        if (lookDirection.sqrMagnitude > 0.001f)
        {
            float angle = Mathf.Atan2(lookDirection.y, lookDirection.x) * Mathf.Rad2Deg;
            return angle + spriteAngleOffset;
        }

        return rb.rotation;
    }

    private void UpdatePlayerRotation()
    {
        // CHANGED: previously rotation intentionally ignored isInputLocked so the spin could keep
        // playing out through the bounce-grace window. Now BicycleKick stops the spin itself the
        // instant E is released, and this window is meant to be a genuine freeze — so rotation now
        // respects the lock too.
        if (isInputLocked) return;

        if (isSpinning) // CHANGED: bicycle kick overrides mouse-look, fixed angular velocity, real-time
        {
            float spunAngle = rb.rotation + spinSpeed * spinDirection * Time.fixedUnscaledDeltaTime; // CHANGED: multiplied by spinDirection so A/D can reverse it
            rb.MoveRotation(spunAngle);
            return;
        }

        if (isOrbiting) // Facing follows the mouse, clamped to stay within orbitAimClampRange of straight-outward
        {
            Vector2 outward = rb.position - orbitCenter;
            if (outward.sqrMagnitude > 0.0001f)
            {
                float outwardAngle = Mathf.Atan2(outward.y, outward.x) * Mathf.Rad2Deg + spriteAngleOffset;

                float desiredAngle = outwardAngle;
                Vector2 lookDirection = mousePosition - rb.position;
                if (lookDirection.sqrMagnitude > 0.001f)
                {
                    desiredAngle = Mathf.Atan2(lookDirection.y, lookDirection.x) * Mathf.Rad2Deg + spriteAngleOffset;
                }

                float halfRange = orbitAimClampRange * 0.5f;
                float deltaFromOutward = Mathf.DeltaAngle(outwardAngle, desiredAngle);
                float clampedDelta = Mathf.Clamp(deltaFromOutward, -halfRange, halfRange);
                float targetOrbitAngle = outwardAngle + clampedDelta;

                float newOrbitAngle = Mathf.MoveTowardsAngle(rb.rotation, targetOrbitAngle, rotationSensitivity * Time.fixedDeltaTime);
                rb.MoveRotation(newOrbitAngle);
            }
            return;
        }

        float targetAngle = GetTargetAngle();
        float newAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, rotationSensitivity * Time.fixedDeltaTime);
        rb.MoveRotation(newAngle);
    }

    private void SnapPlayerRotation()
    {
        rb.MoveRotation(GetTargetAngle()); // CHANGED: consistent with the fix above
    }

    // Called by OrbitZone when releasing with a boost — reuses the normal dash mechanic
    // (trail, dashSpeed) but forces the direction to whatever the ship is facing.
    // ignoreCooldown is true for orbit releases: this is a distinct action from the
    // ground dash and shouldn't get silently swallowed by an unrelated cooldown, which
    // was the bug — pressing Space would detach you with zero boost if canDash was false.
    public void DashInDirection(Vector2 direction, bool ignoreCooldown = false)
    {
        if (!ignoreCooldown && !canDash) return;
        StartCoroutine(DashRoutine(direction));
    }

    private IEnumerator DashRoutine(Vector2? forcedDirection = null)
    {
        canDash = false;
        isDashing = true;
        trail.emitting = true;
        Vector2 dashDirection = forcedDirection ?? (moveInput != Vector2.zero ? moveInput : (Vector2)transform.up);
        rb.linearVelocity = dashDirection.normalized * dashSpeed;
        yield return new WaitForSeconds(dashDuration);
        isDashing = false;
        trail.emitting = false;
        yield return new WaitForSeconds(dashCooldown);
        canDash = true;
    }

    // NEW: draws the Solar Pull radius in the Scene view whenever this GameObject is selected,
    // so you can see exactly which enemies would get caught before pressing F. Orange when idle,
    // switches to a brighter/filled look while actively orbiting (i.e. while F would actually work)
    // so it's obvious at a glance whether the ability is currently usable.
    void OnDrawGizmosSelected()
    {
        Gizmos.color = isOrbiting ? new Color(1f, 0.55f, 0f, 1f) : new Color(1f, 0.55f, 0f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, solarPullRadius);
    }
}