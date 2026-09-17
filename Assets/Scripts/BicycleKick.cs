using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))]
public class BicycleKick : MonoBehaviour
{
    [Header("Charge (no charge system yet — always ready)")]
    public bool isCharged = true;

    [Header("Input")]
    public KeyCode kickKey = KeyCode.E;
    public KeyCode spinLeftKey = KeyCode.A;  // hold while kicking to spin one way
    public KeyCode spinRightKey = KeyCode.D; // hold while kicking to spin the other way

    [Header("Slow-Mo")]
    public float slowMoScale = 0.15f;

    [Header("Audio")]
    public AudioSource audioSource;       // drag an AudioSource here (or leave blank to auto-fetch)
    public AudioClip slowMoEnterClip;     // the sound to play the instant kick-slow-mo starts
    public AudioClip slowMoExitClip;      // the sound to play the instant kick-slow-mo ends

    [Header("Mirror Edge")]
    public Transform mirrorEdge;          // the child object with the MirrorEdge-tagged trigger collider
    public LayerMask mirrorEdgeLayer;     // layer the mirrorEdge's own collider is on (raycast target)
    public string kickableTag = "Kickable";
    public string blasterTag = "Blaster";
    public string mirrorEdgeTag = "MirrorEdge"; // safety check so the ray only counts a real mirror-edge hit
    public float detectionRadius = 6f;    // now doubles as both the OverlapCircle radius AND the raycast max distance

    [Header("Warning Arrow")]
    public LineRenderer predictionLine;   // 3 points: bullet pos -> mirror impact point -> predicted post-reflection path
    public float reflectionPreviewLength = 4f; // how far past the mirror the predicted post-bounce segment is drawn

    [Header("Reflection Tuning")]
    public float reflectSpeedMultiplier = 2f; // how much faster a bullet/arrow travels after bouncing off the mirror — read by EnemyArrow at the moment it reflects

    [Header("Invincibility")]
    public float postKickInvincibility = 2f; // 2 seconds of invincibility after the freeze ends — IsInvincible is already true throughout the kick+freeze, this just extends how long it stays true once control returns

    [Header("Bounce Grace Period")]
    public float bounceGraceDuration = 1f; // how long to keep the mirror active after releasing E if a bullet is about to hit it
    public float freezeTimeScale = 2f; // world speed multiplier during the freeze — the player is fully locked, but the world (and the reflected Blaster) runs at this speed instead of 1x

    [Header("Debug")]
    public bool debugLogging = false; // logs why the prediction line does/doesn't show, and draws the candidate raycasts in the Scene view

    [Header("Cross-ability lockout")]
    public PivotBash pivotBash;
    public PlasmaTetherSystem plasmaTether; // NEW: lets the kick check "is the player currently holding a plasma bomb / cryo snowball" the same way it already checks PivotBash

    private PlayerController playerController;
    private float defaultFixedDeltaTime;
    private bool isKicking = false;
    private bool bulletIncoming = false; // true this frame if a raycast confirmed an arrow is about to cross the mirror edge
    public bool IsKicking => isKicking;
    public bool IsInvincible { get; private set; }

    void Start()
    {
        playerController = GetComponent<PlayerController>();
        defaultFixedDeltaTime = Time.fixedDeltaTime;

        if (audioSource == null) audioSource = GetComponent<AudioSource>(); // fall back to a sibling AudioSource if none was assigned

        if (predictionLine != null)
        {
            predictionLine.positionCount = 3; // also shows the predicted post-reflection path
            predictionLine.useWorldSpace = true;
            predictionLine.enabled = false;
        }
        if (mirrorEdge != null) mirrorEdge.gameObject.SetActive(false);

        if (debugLogging) Debug.Log($"[BicycleKick] Start() ran on {gameObject.name}. isCharged={isCharged}, kickKey={kickKey}, playerController found={playerController != null}, predictionLine assigned={predictionLine != null}, mirrorEdge assigned={mirrorEdge != null}");
    }

    private float debugHeartbeatTimer = 0f; // proves Update() is actually running at all, even with no key presses

    void Update()
    {
        if (debugLogging)
        {
            debugHeartbeatTimer += Time.unscaledDeltaTime;
            if (debugHeartbeatTimer >= 1f)
            {
                debugHeartbeatTimer = 0f;
                Debug.Log($"[BicycleKick] Heartbeat — Update() is running. isKicking={isKicking}, isCharged={isCharged}");
            }
        }

        bool bashBlocking = pivotBash != null && pivotBash.IsBusy;
        bool weaponBlocking = plasmaTether != null && plasmaTether.HasWeapon; // NEW: true while a plasma bomb or cryo snowball is out being dragged/aimed

        if (Input.GetKeyDown(kickKey))
        {
            if (debugLogging) Debug.Log($"[BicycleKick] {kickKey} pressed. isCharged={isCharged}, isKicking={isKicking}, bashBlocking={bashBlocking}, weaponBlocking={weaponBlocking}");

            if (isCharged && !isKicking && !bashBlocking && !weaponBlocking) // CHANGED: added !weaponBlocking
            {
                StartKick();
            }
            else if (debugLogging)
            {
                Debug.Log("[BicycleKick] Kick did NOT start — one of the conditions above was false.");
            }
        }

        if (isKicking)
        {
            bool isFrozen = playerController != null && playerController.isInputLocked;

            if (!isFrozen)
            {
                UpdateSpinDirection(); // A/D held while kicking flips which way the player spins
                UpdateWarningArrow();
            }

            if (Input.GetKeyUp(kickKey))
            {
                RequestEndKick();
            }
        }
    }

    void UpdateSpinDirection()
    {
        if (playerController == null) return;

        if (Input.GetKey(spinRightKey))
        {
            playerController.spinDirection = 1f;
        }
        else if (Input.GetKey(spinLeftKey))
        {
            playerController.spinDirection = -1f;
        }
    }

    void StartKick()
    {
        if (debugLogging) Debug.Log("[BicycleKick] StartKick() called — kick is now active.");

        isKicking = true;
        playerController.isSpinning = true;
        playerController.spinDirection = 1f; // reset to a default direction at the start of each kick
        if (mirrorEdge != null) mirrorEdge.gameObject.SetActive(true);

        Time.timeScale = slowMoScale;
        Time.fixedDeltaTime = defaultFixedDeltaTime * slowMoScale;

        PlaySlowMoEnter(); // fire the slow-mo cue the instant time scale drops

        IsInvincible = true;
    }

    void PlaySlowMoEnter()
    {
        if (audioSource != null && slowMoEnterClip != null)
        {
            audioSource.PlayOneShot(slowMoEnterClip);
        }
        else if (debugLogging)
        {
            Debug.LogWarning("[BicycleKick] PlaySlowMoEnter() skipped — audioSource or slowMoEnterClip is not assigned.");
        }
    }

    void RequestEndKick()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = defaultFixedDeltaTime;

        if (playerController != null) playerController.isSpinning = false; // stop rotating immediately on release too — no more spin during the grace window

        PlaySlowMoExit();

        if (bulletIncoming)
        {
            StartCoroutine(DelayedEndKick());
        }
        else
        {
            EndKick();
        }
    }

    IEnumerator DelayedEndKick()
    {
        if (playerController != null) playerController.isInputLocked = true;

        Time.timeScale = freezeTimeScale;
        Time.fixedDeltaTime = defaultFixedDeltaTime * freezeTimeScale;

        yield return new WaitForSecondsRealtime(bounceGraceDuration);

        Time.timeScale = 1f;
        Time.fixedDeltaTime = defaultFixedDeltaTime;

        if (playerController != null) playerController.isInputLocked = false;

        EndKick();
    }

    void EndKick()
    {
        isKicking = false;
        if (playerController != null) playerController.isSpinning = false; // no-op if RequestEndKick already cleared it, harmless safety net
        if (mirrorEdge != null) mirrorEdge.gameObject.SetActive(false);
        if (predictionLine != null) predictionLine.enabled = false;

        StartCoroutine(PostKickInvincibility());
    }

    void PlaySlowMoExit()
    {
        if (audioSource != null && slowMoExitClip != null)
        {
            audioSource.PlayOneShot(slowMoExitClip);
        }
        else if (debugLogging)
        {
            Debug.LogWarning("[BicycleKick] PlaySlowMoExit() skipped — audioSource or slowMoExitClip is not assigned.");
        }
    }

    IEnumerator PostKickInvincibility()
    {
        yield return new WaitForSecondsRealtime(postKickInvincibility);
        IsInvincible = false;
    }

    void UpdateWarningArrow()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, detectionRadius);

        if (debugLogging) Debug.Log($"[BicycleKick] OverlapCircle found {hits.Length} collider(s) total (pre-tag-filter)");

        Vector2 closestBulletPos = Vector2.zero;
        Vector2 closestHitPoint = Vector2.zero;
        Vector2 closestReflectedDir = Vector2.zero;
        float closestDist = float.MaxValue;
        bool found = false;

        Vector2 mirrorSideDir = playerController != null ? playerController.BackDirection : Vector2.zero;

        foreach (var hit in hits)
        {
            if (hit.CompareTag(kickableTag)) continue; // bashable ones don't get a warning either

            if (!hit.CompareTag(blasterTag)) continue;

            BlasterEnemy blaster = hit.GetComponent<BlasterEnemy>();
            if (blaster == null)
            {
                if (debugLogging) Debug.Log($"[BicycleKick] {hit.name} is tagged '{blasterTag}' but has no BlasterEnemy component — skipped", hit);
                continue;
            }
            if (blaster.IsReflected) continue;

            if (blaster.IsOrbiting) continue;

            Vector2 bulletDir = ((Vector2)transform.position - (Vector2)blaster.transform.position).normalized;
            if (bulletDir == Vector2.zero)
            {
                if (debugLogging) Debug.Log($"[BicycleKick] {hit.name} direction-to-player is zero — skipped", hit);
                continue;
            }

            if (playerController != null)
            {
                Vector2 dirToBlaster = -bulletDir;
                float sideDot = Vector2.Dot(mirrorSideDir, dirToBlaster);
                if (sideDot <= 0f)
                {
                    if (debugLogging) Debug.Log($"[BicycleKick] {hit.name} is on the opposite side of the player from mirrorEdge (dot={sideDot:F2}) — skipped", hit);
                    continue;
                }
            }

            RaycastHit2D rayHit = Physics2D.Raycast(blaster.transform.position, bulletDir, detectionRadius, mirrorEdgeLayer);

            if (debugLogging)
            {
                Color rayColor = rayHit.collider != null ? Color.green : Color.red;
                Debug.DrawRay(blaster.transform.position, bulletDir * detectionRadius, rayColor, 0f, false);
            }

            if (rayHit.collider == null)
            {
                if (debugLogging) Debug.Log($"[BicycleKick] {hit.name}'s path raycast hit nothing on mirrorEdgeLayer (mask={mirrorEdgeLayer.value}) — check the layer is set correctly on mirrorEdge's collider", hit);
                continue;
            }
            if (!rayHit.collider.CompareTag(mirrorEdgeTag))
            {
                if (debugLogging) Debug.Log($"[BicycleKick] {hit.name}'s ray hit {rayHit.collider.name} but it isn't tagged '{mirrorEdgeTag}' — skipped", hit);
                continue;
            }

            float dist = (rayHit.point - (Vector2)blaster.transform.position).sqrMagnitude;
            if (dist < closestDist)
            {
                closestDist = dist;
                closestBulletPos = blaster.transform.position;
                closestHitPoint = rayHit.point;

                closestReflectedDir = Vector2.Reflect(bulletDir, playerController.BackDirection);

                found = true;
            }
        }

        bulletIncoming = found;

        if (!found)
        {
            if (predictionLine != null) predictionLine.enabled = false;
            return;
        }

        if (debugLogging) Debug.Log($"[BicycleKick] Prediction line ENABLED — bullet at {closestBulletPos}, impact at {closestHitPoint}, predicted reflect dir {closestReflectedDir}");

        if (predictionLine != null)
        {
            predictionLine.enabled = true;
            predictionLine.SetPosition(0, closestBulletPos);
            predictionLine.SetPosition(1, closestHitPoint);
            predictionLine.SetPosition(2, closestHitPoint + closestReflectedDir * reflectionPreviewLength);
        }
        else if (debugLogging)
        {
            Debug.LogWarning("[BicycleKick] A valid mirror hit was found, but predictionLine is not assigned in the Inspector — nothing will visibly render!");
        }
    }
}