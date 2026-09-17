using UnityEngine;

public class PivotBash : MonoBehaviour
{
    [Header("Bash Settings")]
    public float minBashSpeed = 8f;
    public float maxBashSpeed = 30f;
    public float bashDuration = 0.25f;
    public float invincibilityDuration = 0.4f;

    [Header("Reel-In Settings")]
    public float reelInDuration = 0.12f; // quick travel time to the pivot

    [Header("Aim Clamp")]
    public float maxPullDistance = 3f;

    [Header("Pivot Detection")]
    public LayerMask pivotLayer;
    public float pivotDetectRadius = 1.5f;
    public float playerRangeToPivot = 6f;

    [Header("Juice Settings")]
    public float hitStopDuration = 0.05f;

    [Header("Audio")]
    public AudioSource audioSource;       // drag an AudioSource here (or leave blank to auto-fetch)
    public AudioClip slowMoEnterClip;     // the sound to play the instant bash-aim slow-mo starts
    public AudioClip slowMoExitClip;      // NEW: the sound to play the instant bash-aim slow-mo ends

    [Header("References")]

    public PlayerController movementScript;
    public LineRenderer aimLine;

    public TrailRenderer bashTrail;

    public GameObject arrowIndicatorPrefab;
    private Transform arrowIndicator;

    [Header("Slow-Mo Settings")]
    public float slowMoScale = 0.1f; // 10% speed
    private float defaultFixedDeltaTime;
    private Rigidbody2D rb;
    private bool isAiming = false;
    private bool isBusy = false; // covers reeling-in AND bashing (blocks new input)
    public bool IsInvincible { get; private set; }
    public bool IsBusy => isBusy || isAiming; // CHANGED: exposed so BicycleKick can't fire mid-bash

    public BicycleKick bicycleKick; // CHANGED: assign in Inspector
    public Vector2 AimDirection => aimDirection;

    private Transform currentPivot;
    private Animator currentPivotAnimator;
    private Vector2 aimDirection;
    private float pullStrength01;

    [Header("Ricochet Settings")]
    public float baseLaunchSpeed = 20f; // Your normal slingshot speed
    public float bossRicochetMultiplier = 2.5f;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        if (aimLine != null) aimLine.enabled = false;

        if (bashTrail != null) bashTrail.emitting = false;

        if (arrowIndicatorPrefab != null)
        {
            GameObject arrowObj = Instantiate(arrowIndicatorPrefab);
            arrowIndicator = arrowObj.transform;
            arrowIndicator.gameObject.SetActive(false);
        }

        if (audioSource == null) audioSource = GetComponent<AudioSource>(); // NEW: fall back to a sibling AudioSource if none was assigned

        defaultFixedDeltaTime = Time.fixedDeltaTime;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(1) && !isBusy)
        {
            TryStartAiming();
        }

        if (isAiming)
        {
            UpdateAim();

            if (Input.GetMouseButtonUp(1))
            {
                StartReelIn();
            }
        }
    }

    void TryStartAiming()
    {
        if (bicycleKick != null && bicycleKick.IsKicking) return;
        Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Collider2D pivotHit = Physics2D.OverlapCircle(mouseWorldPos, pivotDetectRadius, pivotLayer);
        if (pivotHit == null) return;

        float distToPlayer = Vector2.Distance(transform.position, pivotHit.transform.position);
        if (distToPlayer > playerRangeToPivot) return;

        currentPivot = pivotHit.transform;
        currentPivotAnimator = currentPivot.GetComponent<Animator>();
        isAiming = true;

        Time.timeScale = slowMoScale;
        Time.fixedDeltaTime = defaultFixedDeltaTime * slowMoScale;

        PlaySlowMoEnter(); // NEW: fire the slow-mo cue the instant time scale drops

        if (aimLine != null)
        {
            aimLine.enabled = true;
            aimLine.positionCount = 2;
        }

        if (arrowIndicator != null)
        {
            arrowIndicator.position = currentPivot.position;
            arrowIndicator.gameObject.SetActive(true);
        }
        if (currentPivotAnimator != null)
            currentPivotAnimator.SetBool("isArrow", true);

        IBashablePivot targetedBashable = currentPivot.GetComponent<IBashablePivot>();
        if (targetedBashable != null)
        {
            targetedBashable.SetTargeted(true);
        }
    }

    // NEW: same helper pattern as BicycleKick — keeps both abilities' "enter slow-mo" cue consistent.
    // PlayOneShot ignores Time.timeScale, so this still plays at normal speed the instant the world slows down.
    void PlaySlowMoEnter()
    {
        if (audioSource != null && slowMoEnterClip != null)
        {
            audioSource.PlayOneShot(slowMoEnterClip);
        }
    }

    // NEW: mirrors PlaySlowMoEnter() — plays whenever aim-slow-mo ends, whether from a normal
    // release-to-launch (StartReelIn) or an aborted aim (CancelAiming).
    void PlaySlowMoExit()
    {
        if (audioSource != null && slowMoExitClip != null)
        {
            audioSource.PlayOneShot(slowMoExitClip);
        }
    }

    void UpdateAim()
    {
        // --- SAFETY CHECK 1 ---
        // If the arrow hit a wall and was destroyed while aiming, cancel the aim!
        if (currentPivot == null)
        {
            CancelAiming();
            return;
        }
        // ----------------------

        Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Vector2 pivotPos = currentPivot.position;

        Vector2 rawOffset = mouseWorldPos - pivotPos;
        Vector2 clampedOffset = Vector2.ClampMagnitude(rawOffset, maxPullDistance);
        Vector2 clampedMousePos = pivotPos + clampedOffset;

        Vector2 pullDirection = clampedOffset.normalized;
        aimDirection = -pullDirection;

        pullStrength01 = clampedOffset.magnitude / maxPullDistance;

        if (aimLine != null)
        {
            aimLine.SetPosition(0, pivotPos);
            aimLine.SetPosition(1, clampedMousePos);
        }

        float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;

        if (arrowIndicator != null)
        {
            arrowIndicator.position = pivotPos + aimDirection * 0.3f;
            arrowIndicator.rotation = Quaternion.Euler(0, 0, angle);
        }

        currentPivot.rotation = Quaternion.Euler(0, 0, angle + 180);
    }

    // --- NEW HELPER METHOD ---
    // Safely shuts down the UI and time scale if a target vanishes
    void CancelAiming()
    {
        isAiming = false;
        isBusy = false;

        if (aimLine != null) aimLine.enabled = false;
        if (arrowIndicator != null) arrowIndicator.gameObject.SetActive(false);

        Time.timeScale = 1f;
        Time.fixedDeltaTime = defaultFixedDeltaTime;

        PlaySlowMoExit(); // NEW: aim was aborted, but we're still exiting slow-mo — play the cue
    }
    // -------------------------

    void StartReelIn()
    {
        // --- SAFETY CHECK 2 ---
        // Prevents a crash if the arrow gets destroyed the exact frame you release the mouse
        if (currentPivot == null)
        {
            CancelAiming();
            return;
        }
        // ----------------------

        isAiming = false;
        isBusy = true;

        if (aimLine != null) aimLine.enabled = false;
        if (arrowIndicator != null) arrowIndicator.gameObject.SetActive(false);

        Time.timeScale = 1f;
        Time.fixedDeltaTime = defaultFixedDeltaTime;

        PlaySlowMoExit(); // NEW: fire the exit cue right as time scale snaps back to normal

        movementScript.enabled = false;

        StartCoroutine(ReelInThenLaunch());
    }

    System.Collections.IEnumerator ReelInThenLaunch()
    {
        Vector2 startPos = rb.position;
        Vector2 targetPos = (Vector2)currentPivot.position + aimDirection * 0.5f;
        float elapsed = 0f;

        while (elapsed < reelInDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / reelInDuration;
            rb.position = Vector2.Lerp(startPos, targetPos, t);
            yield return null;
        }
        rb.position = targetPos;

        LaunchBash();
    }

    void LaunchBash()
    {
        // 1. Calculate the standard speed based on how far the player pulled
        float bashSpeed = Mathf.Lerp(minBashSpeed, maxBashSpeed, pullStrength01);

        // --- NEW: BOSS RICOCHET MULTIPLIER ---
        // 2. Check if the target is a BossKickNode before applying velocity
        if (currentPivot != null && currentPivot.GetComponent<BossKickNode>() != null)
        {
            bashSpeed *= bossRicochetMultiplier;
            Debug.Log("SUPER CHARGED BOSS RICOCHET!");
        }
        // -------------------------------------

        // 3. Apply the final calculated speed to the player
        rb.linearVelocity = aimDirection * bashSpeed;
        if (bashTrail != null) bashTrail.emitting = true;

        if (currentPivotAnimator != null)
            currentPivotAnimator.SetBool("isArrow", false);

        // 4. Trigger the OnBashed logic (Cleaned up: Only calling this once now!)
        if (currentPivot != null)
        {
            IBashablePivot bashable = currentPivot.GetComponent<IBashablePivot>();
            if (bashable != null)
            {
                bashable.OnBashed(this);
            }
        }

        // 5. Trigger all the juice and timers
        StartCoroutine(HitStopRoutine());
        StartCoroutine(EndBashAfterDelay());
        StartCoroutine(InvincibilityWindow());
    }

    System.Collections.IEnumerator EndBashAfterDelay()
    {
        yield return new WaitForSeconds(bashDuration);
        isBusy = false;
        movementScript.enabled = true;
        if (bashTrail != null) bashTrail.emitting = false;
    }

    System.Collections.IEnumerator InvincibilityWindow()
    {
        IsInvincible = true;
        yield return new WaitForSeconds(invincibilityDuration);
        IsInvincible = false;
    }
    System.Collections.IEnumerator HitStopRoutine()
    {
        // 1. Instantly freeze the game
        Time.timeScale = 0f;

        // 2. Wait using real-world time so the freeze actually ends
        yield return new WaitForSecondsRealtime(hitStopDuration);

        // 3. Snap back to full speed
        Time.timeScale = 1f;
    }
}