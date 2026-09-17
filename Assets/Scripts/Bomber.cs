using UnityEngine;

public class MageEnemy : MonoBehaviour, IPlayerAware, IHasDeathVfx, IKillable, ISolarPullable
{
    [Header("References")]
    public Transform player;
    public Transform firePoint;
    public GameObject orbPrefab;
    public Transform gunPivot; // the T-shape gun child object — rotates independently of the body
    public Rigidbody2D rb2D; // assign in Inspector, or auto-fetched in Awake — needed so terrain colliders actually block movement

    [Header("Positioning (Orbit Around Player)")]
    public float minOrbitRadius = 5f;
    public float maxOrbitRadius = 8f;
    public float moveSpeed = 2.5f;
    public float repositionIntervalMin = 2f;
    public float repositionIntervalMax = 5f;

    [Header("Combat Settings")]
    public int damage = 1;
    public float attackRange = 9f;
    public float attackIntervalMin = 2f;
    public float attackIntervalMax = 4f;
    public float orbSpeed = 4f; // deliberately slower than arrows — gives the player a real window to bash it

    [Header("Aim Inaccuracy")]
    public float aimErrorRadius = 1f;

    [Header("Body Rotation")]
    public float bodyRotationThreshold = 20f; // gun is allowed to lead the body by up to this many degrees before the body turns to catch up
    public float bodyRotationSpeed = 90f;     // degrees/sec the body turns when it needs to catch up to the player

    [Header("Gun Aiming")]
    public float gunRotationSpeed = 180f;   // degrees/sec the gun eases toward its target angle

    [Header("Death")]
    public GameObject shardPrefab; // drag the overdrive Shard prefab here in the Inspector
    public float wiggleAmplitude = 8f;      // degrees of wobble around the aim direction
    public float wiggleFrequency = 1.5f;    // how fast the wiggle oscillates

    [Header("Death Feedback")]
    public GameObject deathVfxPrefab; // spawned in Die() below, and exposed via IHasDeathVfx for other systems (e.g. EnemyOrbBullet's splash)

    [Header("Death Sound")]
    public AudioClip[] deathSounds; // drag one or more death sound clips here - one is picked at random when this enemy dies
    [Range(0f, 1f)] public float deathSoundVolume = 1f;

    [HideInInspector] public bool isBeingPulled = false; // set by SolarPullVictim while the sun's pull ability has this enemy — normal movement/aim/shooting are frozen entirely until it dies

    private float attackTimer;
    private float nextAttackTime;

    private float repositionTimer;
    private float nextRepositionTime;
    private Vector2 currentTargetOffset;

    private float wiggleSeed;

    private bool isDead; // guards against Die() firing twice (e.g. bullet + solar pull the same frame)

    void Awake()
    {
        if (rb2D == null)
        {
            rb2D = GetComponent<Rigidbody2D>();
        }
    }

    void Start()
    {
        nextAttackTime = Random.Range(attackIntervalMin, attackIntervalMax);
        attackTimer = Random.Range(0f, nextAttackTime);

        PickNewTargetOffset();
        nextRepositionTime = Random.Range(repositionIntervalMin, repositionIntervalMax);

        // randomize each mage's wiggle phase so a room full of them doesn't wobble in unison
        wiggleSeed = Random.Range(0f, 100f);
    }

    // --- IHasDeathVfx ---
    public GameObject DeathVfxPrefab => deathVfxPrefab;

    // --- ISolarPullable ---
    public Rigidbody2D Rb2D => rb2D;
    public bool IsBeingPulled { get => isBeingPulled; set => isBeingPulled = value; }

    // NEW: Solar Pull kills skip the shard drop — everything else (VFX, sound, kill tracking) is the same.
    public void KillBySolarPull() => Die(false);

    void Update()
    {
        if (player == null) return;
        if (isBeingPulled) return; // SolarPullVictim owns movement/position entirely while caught — don't fight it

        HandleMovement();
        RotateBodyTowardsPlayer();
        AimGun();
        HandleShooting();
    }

    void HandleMovement()
    {
        repositionTimer += Time.deltaTime;
        if (repositionTimer >= nextRepositionTime)
        {
            repositionTimer = 0f;
            nextRepositionTime = Random.Range(repositionIntervalMin, repositionIntervalMax);
            PickNewTargetOffset();
        }

        Vector2 desiredPos = (Vector2)player.position + currentTargetOffset;
        desiredPos = OrbitZone.ClampOutsideEnemyZone(desiredPos); // never let the standing spot land inside the sun's no-fly zone

        if (rb2D != null)
        {
            Vector2 newPos = Vector2.MoveTowards(rb2D.position, desiredPos, moveSpeed * Time.deltaTime);
            newPos = OrbitZone.ClampOutsideEnemyZone(newPos); // also clamp the actual step, in case the straight-line path clips through
            rb2D.MovePosition(newPos);
        }
        else
        {
            Vector2 newPos = Vector2.MoveTowards(transform.position, desiredPos, moveSpeed * Time.deltaTime);
            transform.position = OrbitZone.ClampOutsideEnemyZone(newPos);
        }
    }

    void PickNewTargetOffset()
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float distance = Random.Range(minOrbitRadius, maxOrbitRadius);
        currentTargetOffset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * distance;
    }

    void RotateBodyTowardsPlayer()
    {
        Vector2 direction = (Vector2)player.position - (Vector2)transform.position;
        if (direction.sqrMagnitude < 0.001f) return;

        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        float currentBodyAngle = transform.eulerAngles.z;
        float angleDiff = Mathf.Abs(Mathf.DeltaAngle(currentBodyAngle, targetAngle));

        // The gun can lead the body by up to bodyRotationThreshold degrees on its own.
        // Only once the player has drifted further than that does the body itself turn to catch up.
        if (angleDiff > bodyRotationThreshold)
        {
            float newAngle = Mathf.MoveTowardsAngle(currentBodyAngle, targetAngle, bodyRotationSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0, 0, newAngle);
        }
    }

    void AimGun()
    {
        if (gunPivot == null) return;

        Vector2 direction = (Vector2)player.position - (Vector2)gunPivot.position;
        if (direction.sqrMagnitude < 0.001f) return;

        float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        // smooth oscillation, offset per-instance via wiggleSeed so it doesn't look synced
        float wiggle = Mathf.Sin((Time.time + wiggleSeed) * wiggleFrequency) * wiggleAmplitude;
        float targetAngle = baseAngle + wiggle;

        float currentAngle = gunPivot.eulerAngles.z;
        float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, gunRotationSpeed * Time.deltaTime);

        gunPivot.rotation = Quaternion.Euler(0, 0, newAngle);
    }

    void HandleShooting()
    {
        float distToPlayer = Vector2.Distance(transform.position, player.position);
        if (distToPlayer > attackRange) return;

        attackTimer += Time.deltaTime;
        if (attackTimer >= nextAttackTime)
        {
            attackTimer = 0f;
            nextAttackTime = Random.Range(attackIntervalMin, attackIntervalMax);
            Shoot();
        }
    }

    void Shoot()
    {
        if (orbPrefab == null || firePoint == null) return;

        Vector2 aimErrorOffset = Random.insideUnitCircle * aimErrorRadius;
        Vector2 targetPoint = (Vector2)player.position + aimErrorOffset;

        Vector2 direction = (targetPoint - (Vector2)firePoint.position).normalized;
        GameObject orbObj = Instantiate(orbPrefab, firePoint.position, firePoint.rotation);

        EnemyOrbBullet orbScript = orbObj.GetComponent<EnemyOrbBullet>();
        if (orbScript != null)
        {
            orbScript.damage = damage;
            orbScript.SetDirection(direction, orbSpeed);
        }
    }

    // Picks a random clip from deathSounds (if any assigned) and plays it at this enemy's
    // position. Uses AudioSource.PlayClipAtPoint so the sound finishes playing even though this
    // gameObject is destroyed the same frame.
    void PlayDeathSound()
    {
        if (deathSounds == null || deathSounds.Length == 0) return;

        AudioClip clip = deathSounds[Random.Range(0, deathSounds.Length)];
        if (clip != null)
        {
            AudioSource.PlayClipAtPoint(clip, transform.position, deathSoundVolume);
        }
    }

    // --- IKillable ---
    // Extracted death sequence so anything that kills this mage (bullet, solar pull, future AoE, etc.)
    // gets the same VFX/sound treatment. Drops a shard by default; Solar Pull kills go through
    // KillBySolarPull() above instead, which calls this with dropShard = false.
    public void Die() => Die(true);

    private void Die(bool dropShard)
    {
        if (isDead) return;
        isDead = true;

        if (deathVfxPrefab != null)
        {
            Instantiate(deathVfxPrefab, transform.position, Quaternion.identity);
        }

        PlayDeathSound();

        if (dropShard)
        {
            bool droppedHealthShard = EnemyKillTracker.Instance != null && EnemyKillTracker.Instance.RegisterKill(transform.position);
            if (!droppedHealthShard && shardPrefab != null)
            {
                Instantiate(shardPrefab, transform.position, Quaternion.identity);
            }
        }

        Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("PlayerBullet")) // same convention as BowmanEnemy
        {
            Die();
            Destroy(other.gameObject);
        }
    }

    // --- IPlayerAware ---
    // Called by EnemySpawner right after Instantiate() so this spawned copy
    // actually has a player reference (the prefab asset can't store one).
    public void SetPlayer(Transform playerTransform)
    {
        player = playerTransform;
    }
}