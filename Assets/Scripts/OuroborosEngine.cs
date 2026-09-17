using UnityEngine;
using UnityEngine.UI; 
using System.Collections;

public class OuroborosEngine : MonoBehaviour
{
    public enum BossPhase { Searing, Transitioning, AbsoluteZero, Frozen ,ReverseTether}
    
    [Header("Boss Core Settings")]
    public BossPhase currentPhase = BossPhase.Searing;
    public int armorPlates = 3; 
    public float requiredRamSpeed = 25f;

    [Header("Phase Visuals")]
    public GameObject searingPhaseVFX; 
    public GameObject absoluteZeroPhaseVFX;

    [Header("Cinematic Ending References")]
    public Image whiteFlashScreen; 
    public Transform playerTransform; 

    [Header("Independent Aura Settings (Temperature Math)")]
    public TemperatureBar playerTempBar; 
    public float auraRadius = 20f;       
    public float maxTempChangeRate = 15f; 

    [Header("Star-Killer Attacks (Phase 1)")]
    public GameObject bossArrowPrefab; 
    public Transform arrowSpawnPoint;  
    public float timeBetweenShots = 7f; 
    public float arrowSpeed = 20f;
    public float spreadAngle = 20f; // Degrees between the 3 arrows

    [Header("Aggro Settings")]
    public Transform player; // Drag your player here in the Inspector
    public float attackRadius = 15f; // How close the player must be for the boss to fire

    [Header("Phase 1: Outer Shell")]
    public int plasmaHitsToBreak = 3;
    private int currentPlasmaHits = 0;
    public GameObject outerShellVisuals; // Drag the outer graphics here
    
    private Rigidbody2D playerRb;
    private Vector2 entrancePosition; 

    void Start()
    {
        if (playerTransform != null)
        {
            playerRb = playerTransform.GetComponent<Rigidbody2D>();
            entrancePosition = playerTransform.position;
        }
        
        ApplyPhaseVisuals(BossPhase.Searing);
        StartCoroutine(StarKillerRoutine());
    }

    void Update()
    {
        // Skip temperature changes during cinematic transitions or when frozen
        if (currentPhase == BossPhase.Transitioning || currentPhase == BossPhase.Frozen) return;
        if (playerTempBar == null || playerTransform == null) return;

        float distance = Vector2.Distance(transform.position, playerTransform.position);

        if (distance <= auraRadius)
        {
            float proximityIntensity = 1f - (distance / auraRadius);
            float tempChangeThisFrame = maxTempChangeRate * proximityIntensity * Time.deltaTime;

            if (currentPhase == BossPhase.Searing)
            {
                playerTempBar.AdjustTemperature(tempChangeThisFrame); 
            }
            else if (currentPhase == BossPhase.AbsoluteZero)
            {
                playerTempBar.AdjustTemperature(-tempChangeThisFrame); 
            }
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (currentPhase == BossPhase.Transitioning) return;

        // RULE 2: Absolute Zero -> Flash Freeze with Cryo
        if (currentPhase == BossPhase.AbsoluteZero && collision.gameObject.GetComponent<CryoSnowball>() != null)
        {
            currentPhase = BossPhase.Frozen;
            ApplyPhaseVisuals(BossPhase.Frozen);
            Debug.Log("CORE FROZEN! RAM AT MAX SPEED!");
        }

        // RULE 3: Frozen -> Ram to Shatter
        else if (currentPhase == BossPhase.Frozen && collision.gameObject.CompareTag("Player"))
        {
            if (playerRb != null && playerRb.linearVelocity.magnitude >= requiredRamSpeed)
            {
                StartCoroutine(CinematicDriftEnding());
            }
        }
    }

    // --- PHASE MANAGEMENT ---
    void ApplyPhaseVisuals(BossPhase phase)
    {
        if (searingPhaseVFX != null) searingPhaseVFX.SetActive(phase == BossPhase.Searing);
        if (absoluteZeroPhaseVFX != null) absoluteZeroPhaseVFX.SetActive(phase == BossPhase.AbsoluteZero);
    }

    // --- STAR-KILLER ATTACK ---
    // --- STAR-KILLER ATTACK (3-WAY SPREAD) ---
    // --- STAR-KILLER ATTACK (3-WAY SPREAD) ---
    // --- STAR-KILLER ATTACK (3-WAY SPREAD) ---
    IEnumerator StarKillerRoutine()
    {
        while (currentPhase == BossPhase.Searing)
        {
            yield return new WaitForSeconds(timeBetweenShots);

            // 1. Only execute the attack if the player exists AND is inside the attack radius
            if (player != null && Vector2.Distance(transform.position, player.position) <= attackRadius)
            {
                GameObject[] activeSuns = GameObject.FindGameObjectsWithTag("Sun");

                // 2. Picks one sun at a time
                if (activeSuns.Length > 0)
                {
                    Transform targetSun = activeSuns[Random.Range(0, activeSuns.Length)].transform;
                    
                    Vector3 aimTarget = targetSun.position; 
                    Collider2D trueCollider = targetSun.GetComponentInChildren<Collider2D>();
                    if (trueCollider != null)
                    {
                        aimTarget = trueCollider.bounds.center; 
                    }

                    Vector2 baseDirection = (aimTarget - arrowSpawnPoint.position).normalized;
                    float baseAngle = Mathf.Atan2(baseDirection.y, baseDirection.x) * Mathf.Rad2Deg;

                    for (int i = -1; i <= 1; i++)
                    {
                        float currentArrowAngle = baseAngle + (i * spreadAngle);
                        
                        Vector2 fireDirection = new Vector2(
                            Mathf.Cos(currentArrowAngle * Mathf.Deg2Rad), 
                            Mathf.Sin(currentArrowAngle * Mathf.Deg2Rad)
                        );

                        GameObject arrow = Instantiate(bossArrowPrefab, arrowSpawnPoint.position, Quaternion.identity);
                        Rigidbody2D arrowRb = arrow.GetComponent<Rigidbody2D>();
                        
                        if (arrowRb != null)
                        {
                            arrowRb.linearVelocity = fireDirection * arrowSpeed; 
                        }
                    }
                }
            }
        }
    }
    // --- DAMAGE DETECTION ---
    // --- DAMAGE DETECTION ---
    public void TakePlasmaHit()
    {
        // The bomb will call this directly when it explodes nearby!
        if (currentPhase == BossPhase.Searing)
        {
            currentPlasmaHits++;
            Debug.Log("Boss caught in plasma blast! Count: " + currentPlasmaHits);

            if (currentPlasmaHits >= plasmaHitsToBreak)
            {
                ShatterOuterShell();
            }
        }
    }
    void ShatterOuterShell()
    {
        Debug.Log("3 Plasma hits! Destroying the Phase 1 Boss...");
        
        // (Later, you will use this exact spot to Instantiate your BossCore prefab)
        
        // Destroy this entire boss object, ending Phase 1 completely
        Destroy(gameObject);
    }

    // --- VISUALIZE THE RADIUS IN THE EDITOR ---
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRadius);
    }
    // --- THE DEJA VU TRANSITION ---
    IEnumerator DejaVuTransition()
    {
        currentPhase = BossPhase.Transitioning;
        
        Time.timeScale = 0.05f; 
        yield return new WaitForSecondsRealtime(1.0f);

        if (playerRb != null)
        {
            playerRb.linearVelocity = Vector2.zero;
            playerTransform.position = entrancePosition;
            playerTransform.SendMessage("ForceCleanUp", SendMessageOptions.DontRequireReceiver);
        }

        ApplyPhaseVisuals(BossPhase.AbsoluteZero);
        currentPhase = BossPhase.AbsoluteZero;

        Time.timeScale = 1f;
    }

    // --- THE WHITE FLASH & DRIFT ENDING ---
    IEnumerator CinematicDriftEnding()
    {
        currentPhase = BossPhase.Transitioning; 
        
        Time.timeScale = 0.1f; 
        if (whiteFlashScreen != null)
        {
            whiteFlashScreen.gameObject.SetActive(true);
            Color flashColor = whiteFlashScreen.color;
            flashColor.a = 1f; 
            whiteFlashScreen.color = flashColor;
        }

        yield return new WaitForSecondsRealtime(0.5f);

        GetComponent<SpriteRenderer>().enabled = false;
        GetComponent<Collider2D>().enabled = false;
        ApplyPhaseVisuals(BossPhase.Frozen); 
        
        if (playerTransform != null)
        {
            playerTransform.SendMessage("ForceCleanUp", SendMessageOptions.DontRequireReceiver);
            
            MonoBehaviour[] playerScripts = playerTransform.GetComponents<MonoBehaviour>();
            foreach(var script in playerScripts)
            {
                if (script != playerRb) script.enabled = false; 
            }
            
            playerRb.linearVelocity = new Vector2(0, 5f); 
            playerRb.linearDamping = 0f; 
        }

        Time.timeScale = 1f;
        float fadeDuration = 3f;
        float elapsed = 0f;
        
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            if (whiteFlashScreen != null)
            {
                Color c = whiteFlashScreen.color;
                c.a = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
                whiteFlashScreen.color = c;
            }
            yield return null;
        }

        Debug.Log("Game Complete. The player is drifting through the cosmos.");
    }
}