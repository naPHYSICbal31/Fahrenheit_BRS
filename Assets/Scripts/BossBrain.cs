using UnityEngine;
using System.Collections;

public class BossBrain : MonoBehaviour
{
    public enum BossState { Active, Dizzied }
    
    [Header("Current State")]
    public BossState currentState = BossState.Active;

    [Header("Activation Settings")]
    public float activationRange = 15f; 
    private bool isAwake = false;

    [Header("Movement Tether")]
    public float tetherRadius = 12f; // How far from its spawn point it is allowed to wander
    private Vector2 spawnPosition;   // Remembers where the boss started

    [Header("Health & Stagger")]
    public int maxHealth = 500;
    private int currentHealth;

    [Header("Combat & Movement")]
    public Transform player;
    public float moveSpeed = 3f;
    public float wriggleIntensity = 2f; 
    public float wriggleSpeed = 5f; 
    public float attackCooldown = 2.5f; 
    private float attackTimer;
    
    private bool isAttacking = false;

    // --- NEW: ELEGANCE & POLISH ---
    [Header("Elegance & Polish")]
    public float breatheSpeed = 3f;
    public float breatheAmplitude = 0.05f;
    public float tiltMultiplier = 2.5f;     // How hard it leans when moving
    public float tiltSmoothness = 8f;       // How smoothly it returns to upright

    [Header("Audio (Assign in Inspector)")]
    public AudioSource bossAudio;
    public AudioClip chargeSound;

    [Header("Phase 2 (Enraged) Settings")]
    public bool isPhase2 = false;
    public AudioClip blastSound;
    public float phase2MoveSpeed = 5f;       
    public float phase2AttackCooldown = 1.2f;

    [Header("Attacks: Projectiles & Melee")]
    public GameObject arrowPrefab;
    public float arrowSpeed = 8f;
    public int arrowDamage = 1;
    public float hammerRadius = 3.5f;
    public int hammerDamage = 20;
    
    [Header("Attacks: Black Hole Execution")]
    public GameObject gravityWellPrefab;     
    public GameObject voidBlastPrefab;       // <-- ASSIGN YOUR FRIEND'S BLASTER PREFAB HERE
    public float blackHoleChargeTime = 3f;   
    public float laserBlastRadius = 4f;      
    public int laserDamage = 50;

    [Header("Telegraph Settings")]
    public float hammerWarningTime = 0.4f;
    public float arrowWarningTime = 0.5f;
    public Color telegraphColor = Color.white;
    private Vector3 originalScale;

    [Header("The Dizzy Phase")]
    public GameObject[] kickNodes; 
    public float dizzyDuration = 6f; 
    private float dizzyTimer = 0f;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;

    void Start()
    {
        originalScale = transform.localScale;
        currentHealth = maxHealth;
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        spawnPosition = transform.position;

        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player").transform;
        }

        attackTimer = attackCooldown; 
    }

    void Update()
    {
        if (player == null) return;

        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        if (distanceToPlayer <= activationRange)
        {
            isAwake = true;
        }
        else
        {
            isAwake = false;
            if (!isAttacking && rb != null) 
            {
                ReturnToSpawn();
            }
        }

        if (!isAwake) return;

        if (currentState == BossState.Active)
        {
            HandleActiveState();
        }
        else if (currentState == BossState.Dizzied)
        {
            HandleDizziedState();
        }

        ApplyElegance(); 
    }

    void HandleActiveState()
    {
        if (isAttacking) return; 

        float currentSpeed = isPhase2 ? phase2MoveSpeed : moveSpeed;
        float currentCooldown = isPhase2 ? phase2AttackCooldown : attackCooldown;

        if (player != null)
        {
            Vector2 targetPos = player.position;
            
            float distanceFromSpawn = Vector2.Distance(spawnPosition, transform.position);
            if (distanceFromSpawn > tetherRadius)
            {
                targetPos = spawnPosition;
            }

            Vector2 directionToTarget = (targetPos - (Vector2)transform.position).normalized;
            
            Vector2 directionToPlayer = (player.position - transform.position).normalized;
            FacePlayer(directionToPlayer);

            Vector2 wriggleOffset = new Vector2(-directionToTarget.y, directionToTarget.x) * Mathf.Sin(Time.time * wriggleSpeed) * wriggleIntensity;
            
            rb.linearVelocity = (directionToTarget * currentSpeed) + wriggleOffset;
        }

        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            ChooseRandomAttack();
            attackTimer = currentCooldown; 
        }
    }

    void FacePlayer(Vector2 directionToPlayer)
    {
        if (directionToPlayer.x > 0 && transform.localScale.x < 0)
        {
            transform.localScale = new Vector3(Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }
        else if (directionToPlayer.x < 0 && transform.localScale.x > 0)
        {
            transform.localScale = new Vector3(-Mathf.Abs(transform.localScale.x), transform.localScale.y, transform.localScale.z);
        }
    }

    void ApplyElegance()
    {
        if (rb == null) return;

        float targetAngle = -rb.linearVelocity.x * tiltMultiplier;
        Quaternion targetRotation = Quaternion.Euler(0, 0, targetAngle);
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * tiltSmoothness);

        if (!isAttacking)
        {
            float breathe = Mathf.Sin(Time.time * breatheSpeed) * breatheAmplitude;
            float facingDirection = transform.localScale.x > 0 ? 1f : -1f; 
            
            transform.localScale = new Vector3(
                (originalScale.x + breathe) * facingDirection, 
                originalScale.y + breathe, 
                originalScale.z
            );
        }
    }

    void ReturnToSpawn()
    {
        if (Vector2.Distance(transform.position, spawnPosition) > 0.5f)
        {
            Vector2 directionToSpawn = (spawnPosition - (Vector2)transform.position).normalized;
            rb.linearVelocity = directionToSpawn * moveSpeed;
        }
        else
        {
            rb.linearVelocity = Vector2.zero; 
        }
    }

    void ChooseRandomAttack()
    {
        int randomMove = Random.Range(0, 3); 

        if (randomMove == 0)
        {
            StartCoroutine(TelegraphHammer());
        }
        else if (randomMove == 1)
        {
            StartCoroutine(TelegraphArrows());
        }
        else 
        {
            StartCoroutine(ExecuteBlackHoleAttack());
        }
    }
    
    void ShootArrows()
    {
        if (arrowPrefab == null || player == null) return;

        Vector2 dirToPlayer = (player.position - transform.position).normalized;
        
        float[] spreadAngles = isPhase2 ? 
            new float[] { -30f, -15f, 0f, 15f, 30f } : 
            new float[] { -15f, 0f, 15f }; 

        foreach (float angle in spreadAngles)
        {
            Vector2 spreadDir = Quaternion.Euler(0, 0, angle) * dirToPlayer;
            GameObject arrow = Instantiate(arrowPrefab, transform.position, Quaternion.identity);

            EnemyArrow arrowScript = arrow.GetComponent<EnemyArrow>();
            if (arrowScript != null)
            {
                arrowScript.damage = arrowDamage;
            }

            Rigidbody2D arrowRb = arrow.GetComponent<Rigidbody2D>();
            if (arrowRb != null)
            {
                arrowRb.linearVelocity = spreadDir * arrowSpeed;
            }
        }
    }

    // --- REWRITTEN: BLACK HOLE WITH BLASTER PREFAB ---
    public IEnumerator ExecuteBlackHoleAttack()
    {
        isAttacking = true;
        rb.linearVelocity = Vector2.zero; 

        Vector3 targetPos = player.position; 
        GameObject blackHole = Instantiate(gravityWellPrefab, targetPos, Quaternion.identity);

        GravityWell gw = blackHole.GetComponent<GravityWell>();
        if (gw != null)
        {
            gw.SetCageMode();
        }

        // 1. Play Charge Sound
        if (bossAudio != null && chargeSound != null)
        {
            bossAudio.PlayOneShot(chargeSound);
        }

        // 2. Wait for the trap to arm
        float timer = 0;
        while(timer < blackHoleChargeTime)
        {
            timer += Time.deltaTime;
            yield return null; 
        }

        // 3. Spawn the Blaster Prefab
        if (voidBlastPrefab != null)
        {
            Instantiate(voidBlastPrefab, targetPos, Quaternion.identity);
        }

        // 4. Play Blast Sound & Shake
        if (bossAudio != null && blastSound != null)
        {
            bossAudio.PlayOneShot(blastSound);
        }
        
        if (CameraShake.Instance != null)
        {
            CameraShake.Instance.Shake(0.6f, 1f); 
        }

        // 5. Calculate Damage
        if (player != null)
        {
            float distanceToCenter = Vector2.Distance(player.position, targetPos);
            if (distanceToCenter <= laserBlastRadius) 
            {
                Debug.Log("PLAYER OBLITERATED BY VOID BLAST!");
                // TakeDamage(laserDamage); <-- Add your player damage call here
            }
        }

        yield return new WaitForSeconds(0.5f);

        Destroy(blackHole);
        isAttacking = false;
    }

    public IEnumerator TelegraphHammer()
    {
        isAttacking = true;
        rb.linearVelocity = Vector2.zero; 

        spriteRenderer.color = telegraphColor;
        transform.localScale = originalScale * 1.2f; 

        yield return new WaitForSeconds(hammerWarningTime);

        spriteRenderer.color = isPhase2 ? Color.yellow : Color.white; 
        transform.localScale = originalScale;

        Debug.Log("HAMMER SMASH!");
        if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.2f, 0.5f);

        isAttacking = false;
    }

    public IEnumerator TelegraphArrows()
    {
        isAttacking = true;
        rb.linearVelocity = Vector2.zero; 

        if (player == null) yield break;

        Vector2 dirToPlayer = (player.position - transform.position).normalized;
        float[] spreadAngles = isPhase2 ? 
            new float[] { -30f, -15f, 0f, 15f, 30f } : 
            new float[] { -15f, 0f, 15f }; 

        System.Collections.Generic.List<LineRenderer> lasers = new System.Collections.Generic.List<LineRenderer>();
        
        foreach (float angle in spreadAngles)
        {
            Vector2 spreadDir = Quaternion.Euler(0, 0, angle) * dirToPlayer;
            
            GameObject laserObj = new GameObject("LaserWarning");
            LineRenderer lr = laserObj.AddComponent<LineRenderer>();
            
            lr.startWidth = 0.05f;
            lr.endWidth = 0.05f;
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = new Color(1f, 0f, 0f, 0.4f); 
            lr.endColor = new Color(1f, 0f, 0f, 0f);     
            
            lr.positionCount = 2;
            lr.SetPosition(0, transform.position);
            lr.SetPosition(1, (Vector2)transform.position + (spreadDir * 20f)); 
            
            lasers.Add(lr);
        }

        yield return new WaitForSeconds(arrowWarningTime);

        foreach (LineRenderer lr in lasers)
        {
            Destroy(lr.gameObject);
        }

        ShootArrows(); 

        isAttacking = false;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, activationRange);
        
        if (Application.isPlaying)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(spawnPosition, tetherRadius);
        }
        else
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, tetherRadius);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("PlayerBullet") && currentState == BossState.Active)
        {
            TakeDamage(10); 
            Destroy(other.gameObject); 
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("PlayerBullet") && currentState == BossState.Active)
        {
            TakeDamage(10);
            Destroy(collision.gameObject);
        }
    }

    void TakeDamage(int damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            TriggerDizzyState();
        }
    }

    void TriggerDizzyState()
    {
        currentState = BossState.Dizzied;
        dizzyTimer = 0f;
        rb.linearVelocity = Vector2.zero;
        isAttacking = false; 

        spriteRenderer.color = Color.gray;

        foreach (GameObject node in kickNodes)
        {
            node.SetActive(true);
        }
    }

    void HandleDizziedState()
    {
        dizzyTimer += Time.deltaTime;
        if (dizzyTimer >= dizzyDuration)
        {
            currentHealth = maxHealth / 2; 
            currentState = BossState.Active;

            spriteRenderer.color = Color.white;

            foreach (GameObject node in kickNodes)
            {
                node.SetActive(false); 
            }
        }
    }

    public void TakeFinisherDamage(Vector2 hitDirection)
    {
        currentHealth = maxHealth; 
        isPhase2 = true;

        currentState = BossState.Active;
        spriteRenderer.color = Color.yellow; 
        isAttacking = false;
        
        foreach (GameObject node in kickNodes)
        {
            node.SetActive(false); 
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero; 
            rb.AddForce(hitDirection * 25f, ForceMode2D.Impulse); 
        }

        if (CameraShake.Instance != null)
        {
            CameraShake.Instance.Shake(0.3f, 0.8f);
        }
        
        Debug.Log("PHASE 2 INITIATED!");
    }
}